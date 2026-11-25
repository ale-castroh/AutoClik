param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Clics de RATÓN REALES (no UI Automation) en cada sección. El resto de pruebas pulsa los controles
# "por dentro" y no detectaría algo invisible que se trague los clics (pasó en 1.4.0-2.0.0: el
# contenedor de Ajustes quedaba encima de Clics).
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  public static uint PidAt(int x, int y) { var p = new POINT { X = x, Y = y }; uint pid; GetWindowThreadProcessId(GetAncestor(WindowFromPoint(p), 2), out pid); return pid; }
}
"@
[R]::SetProcessDPIAware() | Out-Null
# Seguridad: con otro AutoClik abierto (el del usuario) los clics reales podrían caer en su ventana.
if (Get-Process AutoClik -ErrorAction SilentlyContinue) { "OMITIDA: hay otro AutoClik abierto (ciérralo, también de la bandeja)"; return }
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data9"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
# Una macro de dos pasos (clic y tecla A) para abrir el editor; nunca se reproduce
New-Item -ItemType Directory -Force (Join-Path $data "macros") | Out-Null
[IO.File]::WriteAllText((Join-Path $data "macros\Dos pasos.json"),
  '{ "Format": "AutoClik macro", "Version": 1, "Name": "Dos pasos", "Events": "0,1,260,260,0,0;40000,2,260,260,0,0;300000,5,30,0,65,0;360000,6,30,0,65,0" }')
$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--theme=dark" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Find($id) { $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Click($id) { ClickEl (Find $id) $id }
function ClickEl($el, $id) {
    $r = $el.Current.BoundingRectangle
    $x = [int]($r.X + $r.Width / 2); $y = [int]($r.Y + $r.Height / 2)
    # Nunca clicar si debajo no está la ventana de esta prueba
    if ([R]::PidAt($x, $y) -ne $proc.Id) { "   ($($id): debajo hay otra ventana, clic omitido)"; return }
    [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point $x, $y
    Start-Sleep -Milliseconds 100
    [R]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40; [R]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
}
function Sel($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState }

# Clics
Click "DoubleKindRadio"; Click "RepeatCountRadio"; Click "UnitSRadio"; Click "FixedPositionRadio"
"1) Clics: Doble=$(Sel 'DoubleKindRadio') Veces=$(Sel 'RepeatCountRadio') s=$(Sel 'UnitSRadio') Fija=$(Sel 'FixedPositionRadio') (esperado True x4)"
Click "CursorPositionRadio"; Click "SingleKindRadio"; Click "RepeatForeverRadio"; Click "UnitMsRadio"
Click "StartButton"; $on = Tog 'StartButton'
Click "StartButton"; $off = Tog 'StartButton'
"2) Iniciar=$on Detener=$off (esperado On / Off)"

# Macros: sus opciones también deben responder al ratón
Click "NavMacrosButton"
Click "MacroSpeed2"; Click "MacroModeMouse"; Click "MacroCountRadio"
"2b) Macros: sección=$(Tog 'NavMacrosButton') x2=$(Sel 'MacroSpeed2') Ratón=$(Sel 'MacroModeMouse') Veces=$(Sel 'MacroCountRadio') (esperado On, True x3)"
Click "MacroSpeed1"; Click "MacroModeAll"; Click "MacroOnceRadio"

# Ajustes: algo visible arriba, y luego bajar con la RUEDA del ratón hasta "Funciones experimentales"
Click "SettingsButton"
Click "HoldModeRadio"; $hold = Sel 'HoldModeRadio'
Click "ToggleModeRadio"
foreach ($i in 1..12) { [R]::mouse_event(0x800, 0, 0, [uint32]4294967176, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40 }  # rueda abajo (-120)
Start-Sleep -Milliseconds 400
Click "ExperimentalSwitch"; $exp = Tog 'ExperimentalSwitch'
"3) Ajustes: abierto=$(Tog 'SettingsButton') Mantener=$hold experimentales (tras bajar con la rueda)=$exp (esperado On, True, On)"

# Anti-AFK (aparece al activar las experimentales)
Click "NavAntiAfkButton"
Click "AntiAfkSwitch"; $afkOn = Tog 'AntiAfkSwitch'
Click "AntiAfkSwitch"; $afkOff = Tog 'AntiAfkSwitch'
"4) Anti-AFK: sección=$(Tog 'NavAntiAfkButton') Activar=$afkOn Desactivar=$afkOff (esperado On / On / Off)"

# Editor de macros (2.3): elegir un paso, Borrar, Deshacer y Cancelar con clics reales
Click "NavMacrosButton"
(Find "MacroPickButton").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 600
$edit = $AE::RootElement.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, "Editar…")))
if ($edit) { $edit.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }; Start-Sleep -Milliseconds 1000
function StepItems { @((Find "StepList").FindAll($TS::Children, (New-Object Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [Windows.Automation.ControlType]::ListItem)))) }
$items = StepItems
if ($items.Count -ge 2) { ClickEl $items[1] "paso 2" }
$picked = $items.Count -ge 2 -and $items[1].GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected
Click "DeleteButton"; $afterDelete = (StepItems).Count
Click "UndoButton"; $afterUndo = (StepItems).Count
Click "CancelButton"
$discard = $AE::RootElement.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, "Descartar")))
if ($discard) { ClickEl $discard "Descartar" }
Start-Sleep -Milliseconds 400
"4b) Editor: abierto=$($items.Count -eq 2) paso elegido=$picked tras Borrar=$afterDelete tras Deshacer=$afterUndo pide descartar=$($null -ne $discard) cerrado=$($null -eq (Find 'StepList')) (esperado True True 1 2 True True)"

Click "NavClicksButton"
"5) Vuelta a Clics: $(Tog 'NavClicksButton'), Doble sigue clicable: " + $(Click "DoubleKindRadio"; Sel 'DoubleKindRadio') + " (esperado On, True)"

$proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(4000) | Out-Null
if (-not $proc.HasExited) { $proc.Kill() }
"6) error.log creado: " + (Test-Path (Join-Path $data "error.log")) + " (esperado False)"
[System.Windows.Forms.Cursor]::Position = $original
