# Resolves the Godot game process for this repo and writes it into launch.json
# so coreclr attach can run without showing process picker UI.

param(
    [string]$WorkspaceRoot = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$LaunchJsonPath = (Resolve-Path "$PSScriptRoot\..\.vscode\launch.json" -ErrorAction SilentlyContinue).Path,
    [string]$ConfigName = "Godot: Run + Auto Attach (No Picker)",
    [int]$TimeoutSeconds = 15
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
        } |
        Sort-Object ProcessId -Descending

    return $candidates | Select-Object -First 1
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$target = $null
while (-not $target -and (Get-Date) -lt $deadline) {
    $target = Find-GodotProcess -ProjectPathNorm $projectPathNorm
    if (-not $target) {
        Start-Sleep -Milliseconds 300
    }
}

if (-not $target) {
    throw "Could not locate Godot process for '$projectPath' within ${TimeoutSeconds}s."
}

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

Write-Host "[ResolveGodotPid] Config '$ConfigName' now targets PID $($target.ProcessId)."
