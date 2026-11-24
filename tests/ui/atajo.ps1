param([string]$AppPath)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class U {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
}
"@
[U]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
function Key($vk, $up) { [U]::keybd_event($vk, 0, $(if ($up) { 2 } else { 0 }), [UIntPtr]::Zero); Start-Sleep -Milliseconds 30 }
function CtrlF8 { Key 0x11 $false; Key 0x77 $false; Key 0x77 $true; Key 0x11 $true; Start-Sleep -Milliseconds 300 }
function F6 { Key 0x75 $false; Key 0x75 $true; Start-Sleep -Milliseconds 300 }
$AE = [Windows.Automation.AutomationElement]
$proc = Start-Process $AppPath -ArgumentList "--data=$env:TEMP\autoclicker-tests\data" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
$hwnd = $proc.MainWindowHandle
Start-Sleep -Milliseconds 800
[U]::SetForegroundWindow($hwnd) | Out-Null
$root = $AE::FromHandle($hwnd)
function Find($id) { $root.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function State { "estado='{0}' tecla='{1}' pista='{2}'" -f (Find "StatusText").Current.Name, (Find "HotkeyButton").Current.Name, (Find "HotkeyHintText").Current.Name }
(Find "SettingsButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 400
function ToggleKey { (Find "HotkeyButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 200 }

$r = New-Object U+RECT; [U]::DwmGetWindowAttribute($hwnd, 9, [ref]$r, 16) | Out-Null
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point ([int](($r.L + $r.R) / 2)), ($r.T + 120)

ToggleKey;                          "1 Capturando:      " + (State)
Key 0x1B $false; Key 0x1B $true; Start-Sleep -Milliseconds 200
                                    "2 Esc cancela:     " + (State)
ToggleKey; CtrlF8; $k = (Find "HotkeyButton").Current.Name; (Find "SettingsButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 400; "3 Asignar Ctrl+F8: tecla=$k  " + (State)
F6;                                 "4 F6 ya no hace:   " + (State)
CtrlF8;                             "5 Ctrl+F8 inicia:  " + (State)
CtrlF8;                             "6 Ctrl+F8 detiene: " + (State)
[System.Windows.Forms.Cursor]::Position = $original
$proc.Kill()


