param([string]$AppPath, [string]$BenchPath)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class U {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
}
"@
[U]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]

$proc = Start-Process $AppPath -ArgumentList "--data=$env:TEMP\autoclicker-tests\data" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
$hwnd = $proc.MainWindowHandle
Start-Sleep -Milliseconds 800
$root = $AE::FromHandle($hwnd)
function Find($id) { $root.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Select-Radio($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function Set-Field($id, $v) { (Find $id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($v) }
function State { "estado='{0}' contador={1}" -f (Find "StatusText").Current.Name, (Find "CountText").Current.Name }
$toggle = (Find "StartButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)

function Run-Target($seconds) {
  $out = Join-Path $env:TEMP "diana.txt"
  $b = Start-Process $BenchPath -ArgumentList "target", $seconds -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
  Start-Sleep -Milliseconds 800
  [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 320, 320
  return @{ P = $b; Out = $out }
}

# 1) Repetir 15 veces a 50 ms
Set-Field "IntervalBox" "50"; Select-Radio "RepeatCountRadio"; Set-Field "RepeatCountBox" "15"
$t = Run-Target 3
$toggle.Toggle(); Start-Sleep -Milliseconds 1500
"1) 15 veces:      " + (State)
$t.P.WaitForExit(); Get-Content $t.Out

# 2) Mantener pulsado 150 ms, 3 veces
Select-Radio "HoldKindRadio"; Set-Field "HoldBox" "150"; Set-Field "RepeatCountBox" "3"
$t = Run-Target 3
$sw = [Diagnostics.Stopwatch]::StartNew(); $toggle.Toggle()
while ((Find "StatusText").Current.Name -ne "Completado" -and $sw.ElapsedMilliseconds -lt 3000) { Start-Sleep -Milliseconds 20 }
"2) Mantener x3:   " + (State) + " en $($sw.ElapsedMilliseconds) ms (esperado ~450+)"
$t.P.WaitForExit(); Get-Content $t.Out

# 3) Atajo F6 (cursor sobre la app: no clica nada fuera)
Select-Radio "SingleKindRadio"; Select-Radio "RepeatForeverRadio"
$r = New-Object U+RECT; [U]::DwmGetWindowAttribute($hwnd, 9, [ref]$r, 16) | Out-Null
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point ([int](($r.L + $r.R) / 2)), ($r.T + 120)
[U]::keybd_event(0x75, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30; [U]::keybd_event(0x75, 0, 2, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 500
"3) Tras F6:       " + (State) + " repetir-sin-fin=" + (Find "RepeatForeverRadio").GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected + " panel=" + (Find "SettingsPanel").Current.IsEnabled
[U]::keybd_event(0x75, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30; [U]::keybd_event(0x75, 0, 2, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 300
"   Tras otro F6:  " + (State)

# 4) Minimizar -> bandeja
$root.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([Windows.Automation.WindowVisualState]::Minimized)
Start-Sleep -Milliseconds 400
"4) Ventana visible tras minimizar: " + [U]::IsWindowVisible($hwnd) + "   proceso vivo: " + (-not $proc.HasExited)

[System.Windows.Forms.Cursor]::Position = $original
$proc.Kill()


