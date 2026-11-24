param([string]$AppPath, [string]$BenchPath, [string]$Dir)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data6"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
function At($x, $y) { [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point $x, $y; Start-Sleep -Milliseconds 60 }
function Down($b) { [R]::mouse_event($(if ($b -eq 'R') { 8 } else { 2 }),0,0,0,[UIntPtr]::Zero) }
function Up($b) { [R]::mouse_event($(if ($b -eq 'R') { 16 } else { 4 }),0,0,0,[UIntPtr]::Zero) }
function Shot($name, $x, $y, $w, $h) { $b = New-Object Drawing.Bitmap $w, $h; [Drawing.Graphics]::FromImage($b).CopyFromScreen($x, $y, 0, 0, $b.Size); $b.Save((Join-Path $Dir $name)) }

$out = Join-Path $env:TEMP "diana_p6.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "16" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--record-injected", "--theme=dark" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Find($id) { $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Sel($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function State { "estado='" + (Find "StatusText").Current.Name + "' clics=" + (Find "CountText").Current.Name + " resumen='" + (Find "PointsSummaryText").Current.Name + "'" }

Sel "PointsPositionRadio"; Start-Sleep -Milliseconds 200
(Find "RecordButton").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 700
$bar = $AE::RootElement.FindFirst($TS::Children, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, "Grabando")))
"1) Barra de grabación visible: " + ($null -ne $bar) + "   ventana principal oculta: " + (-not $root.Current.IsOffscreen -eq $false)
if ($null -eq $bar) { [System.Windows.Forms.Cursor]::Position = $original; $proc.Kill(); return }

# Grabar: izquierdo, (300 ms) derecho, (500 ms) izquierdo mantenido 450 ms
At 300 300; Down 'L'; Start-Sleep -Milliseconds 40; Up 'L'
Start-Sleep -Milliseconds 260
At 450 300; Down 'R'; Start-Sleep -Milliseconds 40; Up 'R'
Start-Sleep -Milliseconds 460
At 380 450; Down 'L'; Start-Sleep -Milliseconds 450; Up 'L'
Start-Sleep -Milliseconds 300
Shot "p6_bar.png" 0 0 1400 120
[R]::keybd_event(0x1B,0,0,[UIntPtr]::Zero); [R]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 800
"2) Tras Esc: " + (State)

# Reproducir 2 vueltas a x2
Sel "RepeatCountRadio"; (Find "RepeatCountBox").GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("2"); Sel "Speed2Radio"
Start-Sleep -Milliseconds 200
At 1000 800
$sw = [Diagnostics.Stopwatch]::StartNew()
(Find "StartButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
while ((Find "StatusText").Current.Name -ne "Completado" -and $sw.ElapsedMilliseconds -lt 5000) { Start-Sleep -Milliseconds 20 }
"3) Reproducción x2, 2 vueltas: " + (State) + " en $($sw.ElapsedMilliseconds) ms (esperado ~1,2 s)"
$bench.WaitForExit(); Get-Content $out
$proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(4000) | Out-Null
$json = Get-Content "$data\settings.json" -Raw | ConvertFrom-Json
$pts = $json.Profiles.Predeterminado.Points
"4) Guardado: " + $pts.Count + " puntos, velocidad=" + $json.Profiles.Predeterminado.Speed
foreach ($p in $pts) { "   ({0},{1}) {2} {3} espera={4} mantener={5}" -f $p.X, $p.Y, $p.Button, $p.Kind, $p.DelayMs, $p.HoldMs }
[System.Windows.Forms.Cursor]::Position = $original
