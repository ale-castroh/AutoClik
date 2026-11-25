param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# 2.2: acción Tecla y Rueda del autoclicker, y la mini ventana flotante.
# Las teclas solo se envían con la diana delante (AutoClik nunca las manda a sí mismo); la mini
# ventana se prueba con la rueda (con AutoClik oculto no se sabe qué ventana quedaría delante).
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data11"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
function At($x, $y) { [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point $x, $y; Start-Sleep -Milliseconds 80 }

$out = Join-Path $env:TEMP "diana_p11.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "20" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--theme=dark" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$h = $proc.MainWindowHandle
$root = $AE::FromHandle($h)
function Find($id) { $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function Sel($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function SetText($id, $v) { (Find $id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($v) }
function Text($id) { (Find $id).Current.Name }
function Front { [R]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [R]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero); [R]::SetForegroundWindow($h) | Out-Null; Start-Sleep -Milliseconds 300 }

# 1) Acción Tecla: elegir la tecla A con el selector (como los atajos)
Front
Sel "KeyActionRadio"; Start-Sleep -Milliseconds 200
Tog "ActionKeyButton"; Start-Sleep -Milliseconds 200
[R]::keybd_event(0x41, 0x1E, 0, [UIntPtr]::Zero); [R]::keybd_event(0x41, 0x1E, 2, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 300
"1) Tecla elegida: '$(Text 'ActionKeyButton')' (esperado 'A')"

# 2) 5 veces cada 100 ms. Con AutoClik delante espera; al dar el foco a la diana, pulsa.
Sel "RepeatCountRadio"; SetText "RepeatCountBox" "5"; SetText "IntervalBox" "100"
Tog "StartButton"; Start-Sleep -Milliseconds 600
$waiting = Text 'StatusText'
At 300 300; [R]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [R]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
$sw = [Diagnostics.Stopwatch]::StartNew()
while ((Text 'StatusText') -ne 'Completado' -and $sw.ElapsedMilliseconds -lt 4000) { Start-Sleep -Milliseconds 50 }
$stuck = ([R]::GetAsyncKeyState(0x41) -band 0x8000) -ne 0
"2) Con AutoClik delante: '$waiting' (esperado 'Cambia a la ventana de destino'); fin: '$(Text 'StatusText')' contador=$(Text 'CountText') tecla pegada=$stuck (esperado Completado, 5, False)"

# 3) Acción Rueda: abajo 2 pasos, 3 veces, en un punto fijo sobre la diana
Front
Sel "WheelActionRadio"; Sel "WheelDownRadio"; SetText "WheelStepsBox" "2"; SetText "RepeatCountBox" "3"
Sel "FixedPositionRadio"; SetText "XBox" "330"; SetText "YBox" "330"
Start-Sleep -Milliseconds 200
Tog "StartButton"
$sw = [Diagnostics.Stopwatch]::StartNew()
while ((Text 'StatusText') -ne 'Completado' -and $sw.ElapsedMilliseconds -lt 4000) { Start-Sleep -Milliseconds 50 }
"3) Rueda: '$(Text 'StatusText')' contador=$(Text 'CountText') (esperado Completado, 3; la diana suma -6 pasos)"

# 4) Mini ventana: rueda sin fin, ocultar AutoClik (minimizar a la bandeja) -> aparece; su Detener para
Sel "RepeatForeverRadio"; SetText "IntervalBox" "250"
Tog "StartButton"; Start-Sleep -Milliseconds 300
$root.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([Windows.Automation.WindowVisualState]::Minimized)
Start-Sleep -Milliseconds 900
$hud = $AE::RootElement.FindFirst($TS::Children, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, "AutoClik mini")))
$hudTitle = if ($hud) { $hud.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "TitleText"))).Current.Name } else { '' }
"4a) Oculto y clicando: mini ventana visible=$($null -ne $hud) estado='$hudTitle' (esperado True, 'Clicando')"
if ($hud) {
    $hud.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "StopButton"))).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 700
    $hud2 = $AE::RootElement.FindFirst($TS::Children, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, "AutoClik mini")))
    "4b) Tras su Detener: mini ventana visible=$($null -ne $hud2) (esperado False: ya no clica)"
}

Stop-Process -Id $proc.Id -ErrorAction SilentlyContinue  # está oculto en la bandeja (datos de prueba)
$bench.WaitForExit(); Get-Content $out
"   (esperado en la diana: teclas A A A A A; rueda: -6 pasos o algo menos si la 4 llegó a girar)"
"5) error.log creado: " + (Test-Path (Join-Path $data "error.log")) + " (esperado False)"
[System.Windows.Forms.Cursor]::Position = $original
