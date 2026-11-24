param([string]$AppPath, [string]$BenchPath, [string]$ShotDir)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class M {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
}
"@
[M]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]
function TopWindow($name) { $AE::RootElement.FindFirst([Windows.Automation.TreeScope]::Children, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, $name))) }
function ClickAt($x, $y) { [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point $x, $y; Start-Sleep -Milliseconds 120; [M]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 40; [M]::mouse_event(4,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 250 }
function Shot($name, $x, $y, $w, $h) { $b = New-Object Drawing.Bitmap $w, $h; [Drawing.Graphics]::FromImage($b).CopyFromScreen($x, $y, 0, 0, $b.Size); $b.Save((Join-Path $ShotDir $name)) }
function Abort($msg) { "ABORTADO: $msg"; [System.Windows.Forms.Cursor]::Position = $original; Get-Process Autoclicker, Autoclicker.Bench -ErrorAction SilentlyContinue | Stop-Process -Force; exit }

# Diana (8 s) en 200..440
$out = Join-Path $env:TEMP "diana_multi.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "14" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
$proc = Start-Process $AppPath -ArgumentList "--theme=dark", "--data=$env:TEMP\autoclicker-tests\data" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Find($id) { $root.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Status { "estado='{0}' contador={1} resumen='{2}'" -f (Find "StatusText").Current.Name, (Find "CountText").Current.Name, (Find "PointsSummaryText").Current.Name }

(Find "PointsPositionRadio").GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 200
# Sin puntos no debe arrancar
(Find "StartButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 300
"0) Sin puntos:   " + (Status)

(Find "AddPointsButton").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 900
if ($null -eq (TopWindow "Elegir posición")) { Abort "el selector no se abrió" }
ClickAt 260 260; ClickAt 380 260; ClickAt 320 380
Shot "multi_picking.png" 150 150 400 330
[M]::keybd_event(0x1B,0,0,[UIntPtr]::Zero); [M]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 700
"1) Tras añadir:  " + (Status)

# Editar el punto 2: botón derecho
ClickAt 380 260
Start-Sleep -Milliseconds 500
$editor = TopWindow "Editar punto"
if ($null -eq $editor) { $ws = $AE::RootElement.FindAll([Windows.Automation.TreeScope]::Children, (New-Object Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id))) | ForEach-Object { $_.Current.Name }; Abort ("el editor no se abrió; ventanas: " + ($ws -join ", ")) }
$right = $editor.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "ButtonRight")))
$right.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 300
Shot "multi_editor.png" 150 150 760 420
[M]::keybd_event(0x1B,0,0,[UIntPtr]::Zero); [M]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 400
"2) Editor cerrado: " + ($null -eq (TopWindow "Editar punto"))

# 4 vueltas a 80 ms
(Find "IntervalBox").GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("120")
(Find "RepeatCountRadio").GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
(Find "RepeatCountBox").GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("4")
Start-Sleep -Milliseconds 200
$r = New-Object M+RECT; [M]::DwmGetWindowAttribute($proc.MainWindowHandle, 9, [ref]$r, 16) | Out-Null
Shot "multi_main.png" $r.L $r.T ($r.R - $r.L) ($r.B - $r.T)
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 900, 700
(Find "StartButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 400
Shot "multi_running.png" 150 150 400 330
Start-Sleep -Milliseconds 1200
"3) Tras 4 vueltas: " + (Status) + "   (esperado Completado, 12)"
$bench.WaitForExit(); Get-Content $out
[System.Windows.Forms.Cursor]::Position = $original
$proc.Kill()
