# Brings the Cursor main window to the foreground (retries for windows that open after Godot).
# Called from EnsureGodotEditor.bat after starting the Godot editor.

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@

function Get-CursorMainWindow {
  Get-Process -Name 'Cursor' -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
    Sort-Object StartTime -Descending |
    Select-Object -First 1
}

Start-Sleep -Seconds 1
for ($i = 0; $i -lt 10; $i++) {
  $p = Get-CursorMainWindow
  if ($p) {
    [void][W32]::ShowWindow($p.MainWindowHandle, 9)
    [void][W32]::SetForegroundWindow($p.MainWindowHandle)
  }
  Start-Sleep -Milliseconds 400
}
