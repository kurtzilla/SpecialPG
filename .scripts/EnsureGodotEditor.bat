@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem =============================================================================
rem  Start Godot editor for this repo if no matching process is running.
rem  Used by VS Code/Cursor "folder open" task — does not launch Cursor.
rem
rem  Detection: prefer gdvm default C# editor basename (matches editor + console
rem  exe for that version). If gdvm is unavailable, fall back to any Godot_*.
rem  Assumes you do not run multiple Godot solutions concurrently.
rem =============================================================================

for %%I in ("%~dp0..") do set "REPO=%%~fI"
set "GODOT_PROJECT=%REPO%\src\Godot"

set "GSTEM="
set "GDVM_GODOT_EXE="
for /f "delims=" %%i in ('gdvm show --csharp 2^>nul') do for %%A in ("%%i") do set "GSTEM=%%~nA"
for /f "delims=" %%i in ('gdvm show --csharp 2^>nul') do set "GDVM_GODOT_EXE=%%i"

if defined GSTEM (
  tasklist | findstr /I /C:"!GSTEM!" >nul 2>&1
) else (
  tasklist | findstr /I "Godot_" >nul 2>&1
)

if errorlevel 1 (
  echo [EnsureGodotEditor] No matching Godot process; starting editor...
  rem Prefer non-console editor app from gdvm; fallback to "godot" on PATH.
  if defined GDVM_GODOT_EXE if exist "!GDVM_GODOT_EXE!" (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '!GDVM_GODOT_EXE!' -ArgumentList '--path','%GODOT_PROJECT%','--editor' | Out-Null"
  ) else (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'godot' -ArgumentList '--path','%GODOT_PROJECT%','--editor' | Out-Null"
  )
  rem Godot / DebugAttachService may steal focus; retry bringing Cursor forward.
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0FocusCursor.ps1"
) else (
  echo [EnsureGodotEditor] Godot already running; skipping editor start.
)

endlocal
