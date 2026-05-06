# Resolves the Godot game process for this repo and writes it into launch.json
# so coreclr attach can run without showing process picker UI.

param(
    [string]$WorkspaceRoot = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$LaunchJsonPath = (Resolve-Path "$PSScriptRoot\..\.vscode\launch.json" -ErrorAction SilentlyContinue).Path,
    [string]$ConfigName = "Godot: Run + Auto Attach (No Picker)",
    [int]$TimeoutSeconds = 5,
    [int]$SettleMilliseconds = 3000
)

if (-not $LaunchJsonPath) {
    $LaunchJsonPath = Join-Path $WorkspaceRoot ".vscode\launch.json"
}

$projectJoined = Join-Path $WorkspaceRoot "src\Godot"
$projectPath = if (Test-Path $projectJoined) { (Resolve-Path $projectJoined).Path } else { $projectJoined }
$projectPathNorm = $projectPath.ToLowerInvariant().Replace('/', '\')
$hintPath = Join-Path $WorkspaceRoot ".vscode\godot-detached.pid"

function Get-HintedGodotProcess {
    param(
        [int]$ProcessId,
        [string]$ProjectPathNorm,
        [int]$CommandLineWaitMilliseconds = 3500
    )

    # WMI often returns empty CommandLine for a few hundred ms after Start-Process — without retries
    # the hint is skipped and Find-GodotProcess picks a different Godot (e.g. older editor).
    $deadline = (Get-Date).AddMilliseconds($CommandLineWaitMilliseconds)
    while ($true) {
        $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
        if (-not $proc) {
            return $null
        }
        if ($proc.Name -notlike "Godot*.exe") {
            return $null
        }

        if ($proc.CommandLine) {
            $cmd = $proc.CommandLine.ToLowerInvariant()
            if (-not $cmd.Contains("--path")) {
                return $null
            }
            if (-not $cmd.Contains($ProjectPathNorm)) {
                Write-Host "[ResolveGodotPid] Hint PID $ProcessId command line does not match project path; ignoring hint."
                return $null
            }
            return $proc
        }

        if ((Get-Date) -ge $deadline) {
            Write-Host ("[ResolveGodotPid] Hint PID {0}: WMI command line still empty after {1}ms; trusting detached launch PID (Godot*.exe)." -f $ProcessId, $CommandLineWaitMilliseconds)
            return $proc
        }

        Start-Sleep -Milliseconds 120
    }
}

function Find-GodotProcess {
    param([string]$ProjectPathNorm)

    $candidates = Get-CimInstance Win32_Process -Filter "Name LIKE 'Godot%.exe'" |
        Where-Object {
            $_.CommandLine -and
            $_.CommandLine.ToLowerInvariant().Contains("--path") -and
            $_.CommandLine.ToLowerInvariant().Contains($ProjectPathNorm)
        }

    if (-not $candidates) {
        return $null
    }

    # Prefer runtime/game process for CoreCLR attach. Editor/helper processes can
    # produce configurationDone handshake failures in some sessions.
    $ranked = $candidates | ForEach-Object {
        $cmd = $_.CommandLine.ToLowerInvariant()
        $score = 0

        if ($cmd.Contains("--editor")) { $score -= 1000 } else { $score += 80 }
        if ($cmd.Contains("--headless")) { $score += 10 }
        if ($cmd.Contains("--path")) { $score += 15 }
        if ($cmd.Contains("debugattachservice")) { $score -= 500 }
        if ($cmd.Contains("godottools")) { $score -= 300 }

        [PSCustomObject]@{
            Score = $score
            Process = $_
            CommandLine = $_.CommandLine
        }
    } | Sort-Object -Property @{ Expression = { $_.Score }; Descending = $true }, @{ Expression = { $_.Process.ProcessId }; Descending = $true }

    return $ranked | Select-Object -First 1
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$target = $null
$targetMeta = $null

if (Test-Path $hintPath) {
    try {
        $hintPid = [int]((Get-Content -Raw -Path $hintPath).Trim())
        $hintProc = Get-HintedGodotProcess -ProcessId $hintPid -ProjectPathNorm $projectPathNorm
        if ($hintProc) {
            $target = $hintProc
            $cmdLower = if ($hintProc.CommandLine) { $hintProc.CommandLine.ToLowerInvariant() } else { "" }
            $hintScore = if ($cmdLower -and $cmdLower.Contains("--editor")) { -920 } else { 80 }
            $targetMeta = [PSCustomObject]@{
                Score       = $hintScore
                Process     = $hintProc
                CommandLine = $hintProc.CommandLine
            }
            Write-Host "[ResolveGodotPid] Using detached-launch PID $($hintProc.ProcessId) (hint from RunGodotDetached.ps1)."
        }
    }
    catch {
        Write-Host "[ResolveGodotPid] Hint file present but invalid; falling back to scan. ($_)"
    }
    if (-not $target -and (Test-Path $hintPath)) {
        Remove-Item $hintPath -Force -ErrorAction SilentlyContinue
    }
}

while (-not $target -and (Get-Date) -lt $deadline) {
    $rankedTarget = Find-GodotProcess -ProjectPathNorm $projectPathNorm
    $target = if ($rankedTarget) { $rankedTarget.Process } else { $null }
    $targetMeta = $rankedTarget
    if (-not $target) {
        Start-Sleep -Milliseconds 300
    }
}

if (-not $target) {
    throw "Could not locate Godot process for '$projectPath' within ${TimeoutSeconds}s."
}

# Give the selected process a short settle window before attach.
Start-Sleep -Milliseconds $SettleMilliseconds

if (-not (Test-Path $LaunchJsonPath)) {
    throw "launch.json not found: $LaunchJsonPath"
}

# Do not use ConvertTo-Json here — it rewrites the whole file (spacing, property order) and some
# Cursor/C# attach flows appear to keep a stale merged launch config when JSON is wholesale replaced.
$content = [System.IO.File]::ReadAllText($LaunchJsonPath)
if ($content.IndexOf($ConfigName, [System.StringComparison]::Ordinal) -lt 0) {
    throw "launch.json does not contain configuration name '$ConfigName'."
}

$digitPidMatches = [regex]::Matches($content, '"processId"\s*:\s*\d+')
if ($digitPidMatches.Count -ne 1) {
    throw "launch.json must contain exactly one numeric processId for no-picker attach (found $($digitPidMatches.Count))."
}

$newPid = [int]$target.ProcessId
$m = [regex]::Match($content, '("processId"\s*:\s*)\d+')
if (-not $m.Success) {
    throw "launch.json: could not match numeric processId pattern."
}

$newContent = $content.Substring(0, $m.Index) + $m.Groups[1].Value + $newPid.ToString() + $content.Substring($m.Index + $m.Length)
[System.IO.File]::WriteAllText($LaunchJsonPath, $newContent)

if ($targetMeta) {
    $cl = if ($targetMeta.CommandLine) { $targetMeta.CommandLine.ToLowerInvariant() } else { "" }
    $why = if (-not $cl) { "wmi-cmdline-pending" } elseif ($cl.Contains("--editor")) { "editor-fallback" } else { "runtime-preferred" }
    Write-Host "[ResolveGodotPid] Config '$ConfigName' now targets PID $($target.ProcessId) ($why, score=$($targetMeta.Score))."
} else {
    Write-Host "[ResolveGodotPid] Config '$ConfigName' now targets PID $($target.ProcessId)."
}
