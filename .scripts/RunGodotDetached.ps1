# Starts Godot for this repo (same as F5 prelaunch) and records PID so ResolveGodotPid
# attaches to this process—not another editor instance (avoids coreclr configurationDone / 0x80070057).

param(
    [string]$WorkspaceRoot = (Resolve-Path "$PSScriptRoot\..").Path
)

$projectPath = (Resolve-Path (Join-Path $WorkspaceRoot "src\Godot")).Path
$hintPath = Join-Path $WorkspaceRoot ".vscode\godot-detached.pid"
$vscodeDir = Split-Path $hintPath -Parent
if (-not (Test-Path $vscodeDir)) {
    New-Item -ItemType Directory -Path $vscodeDir -Force | Out-Null
}

$p = Start-Process -FilePath "godot" -ArgumentList "--path", $projectPath -PassThru
if (-not $p) {
    throw "Start-Process godot failed."
}

$p.Id | Set-Content -Path $hintPath -Encoding ascii -NoNewline
Write-Host "[RunGodotDetached] Started godot PID $($p.Id); hint -> $hintPath"
