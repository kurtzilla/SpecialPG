@echo off
setlocal EnableExtensions

rem ============================================================================
rem  Ensure Godot editor is running for this project, then open Cursor.
rem ============================================================================

for %%I in ("%~dp0..") do set "REPO=%%~fI"
set "CURSOR_EXE=%LOCALAPPDATA%\Programs\cursor\Cursor.exe"

call "%~dp0EnsureGodotEditor.bat"

if exist "%CURSOR_EXE%" (
  start "" "%CURSOR_EXE%" "%REPO%"
) else (
  start "" cursor "%REPO%"
)

endlocal
