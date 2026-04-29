# Resolves the Godot game process for this repo and writes it into launch.json
# so coreclr attach can run without showing process picker UI.

param(
    [string]$WorkspaceRoot = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$LaunchJsonPath = (Resolve-Path "$PSScriptRoot\..\.vscode\launch.json" -ErrorAction SilentlyContinue).Path,
    [string]$ConfigName = "Godot: Run + Auto Attach (No Picker)",
    [int]$TimeoutSeconds = 15,
    [int]$SettleMilliseconds = 800
)

if (-not $LaunchJsonPath) {
    $LaunchJsonPath = Join-Path $WorkspaceRoot ".vscode\launch.json"
}

$projectPath = Join-Path $WorkspaceRoot "src\Godot"
$projectPathNorm = $projectPath.ToLowerInvariant().Replace('/', '\')

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

$launch = Get-Content -Raw -Path $LaunchJsonPath | ConvertFrom-Json
$cfg = $launch.configurations | Where-Object { $_.name -eq $ConfigName } | Select-Object -First 1
if (-not $cfg) {
    throw "Configuration '$ConfigName' was not found in launch.json."
}

$cfg.processId = [int]$target.ProcessId

$jsonOut = $launch | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($LaunchJsonPath, $jsonOut + [Environment]::NewLine)

if ($targetMeta) {
    $why = if ($targetMeta.CommandLine.ToLowerInvariant().Contains("--editor")) { "editor-fallback" } else { "runtime-preferred" }
    Write-Host "[ResolveGodotPid] Config '$ConfigName' now targets PID $($target.ProcessId) ($why, score=$($targetMeta.Score))."
} else {
    Write-Host "[ResolveGodotPid] Config '$ConfigName' now targets PID $($target.ProcessId)."
}
