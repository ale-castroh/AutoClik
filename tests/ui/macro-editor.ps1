param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Editor de macros (2.3): abrir desde el menú, ver los pasos, borrar, deshacer, insertar un texto,
# cambiar una posición, Probar (reproduce sin guardar y vuelve al editor), Guardar y reproducir.
# Después, posiciones relativas a una ventana: la diana (proceso Autoclicker.Bench) y una que no existe.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data13"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $data "macros") | Out-Null

# Macro de partida: clic (260,260), A, B, clic (320,320). La diana ocupa 200-440.
function MacroJson($name, $events, $extra = '') {
  "{ `"Format`": `"AutoClik macro`", `"Version`": 1, `"Name`": `"$name`", `"Events`": `"$events`" $extra }"
}
$ev = "0,1,260,260,0,0;40000,2,260,260,0,0;200000,5,30,0,65,0;260000,6,30,0,65,0;400000,5,48,0,66,0;460000,6,48,0,66,0;600000,1,320,320,0,0;640000,2,320,320,0,0"
[IO.File]::WriteAllText((Join-Path $data "macros\Prueba.json"), (MacroJson "Prueba" $ev))
# Relativa a la diana: grabada con su esquina en (150,150) -> hoy está en (200,200): el clic de 210 cae en 260
[IO.File]::WriteAllText((Join-Path $data "macros\Relativa.json"), (MacroJson "Relativa" "0,1,210,210,0,0;40000,2,210,210,0,0" ', "WindowProcess": "Autoclicker.Bench", "WindowTitle": "Bench target", "WindowX": 150, "WindowY": 150'))
[IO.File]::WriteAllText((Join-Path $data "macros\Sin ventana.json"), (MacroJson "Sin ventana" "0,1,210,210,0,0;40000,2,210,210,0,0" ', "WindowProcess": "NoExisteXyz", "WindowTitle": "Juego cerrado", "WindowX": 0, "WindowY": 0'))
[IO.File]::WriteAllText((Join-Path $data "settings.json"), '{ "Prefs": { "MacroName": "Prueba" } }')

$out = Join-Path $env:TEMP "diana_p13.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "24" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--theme=dark" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Cond($p, $v) { New-Object Windows.Automation.PropertyCondition($p, $v) }
function FindIn($r, $id) { $r.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty $id)) }
function Find($id) { FindIn $root $id }
function Invoke($el) { $el.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Text($id) { (Find $id).Current.Name }
function MenuItem($name) {
  Invoke (Find "MacroPickButton"); Start-Sleep -Milliseconds 600
  $i = $AE::RootElement.FindFirst($TS::Descendants, (Cond $AE::NameProperty $name))
  if ($i) { Invoke $i }; Start-Sleep -Milliseconds 800; $null -ne $i
}
function Editor {
  $sl = Find "StepList"; if (-not $sl) { return $null }
  $w = [Windows.Automation.TreeWalker]::ControlViewWalker; $e = $w.GetParent($sl)
  while ($e -and $e.Current.ControlType -ne [Windows.Automation.ControlType]::Window) { $e = $w.GetParent($e) }
  $e
}
function Steps { @((Find "StepList").FindAll($TS::Children, (Cond $AE::ControlTypeProperty ([Windows.Automation.ControlType]::ListItem))) | ForEach-Object { $_.Current.Name }) }
function SelectStep($n) {
  $items = (Find "StepList").FindAll($TS::Children, (Cond $AE::ControlTypeProperty ([Windows.Automation.ControlType]::ListItem)))
  $items[$n].GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 250
}
function Click($id) { Invoke (Find $id); Start-Sleep -Milliseconds 400 }

(Find "NavMacrosButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 400

# 1) Abrir el editor
$menu = MenuItem "Editar…"
$ed = Editor
"1) Menú 'Editar…'=$menu  editor abierto=$($null -ne $ed)  principal desactivada=$(-not $root.Current.IsEnabled)"
"   pasos: " + ((Steps) -join ' | ')
"   (esperado: Clic izquierdo en 260, 260 | Pulsar A (0.06 s) | Pulsar B (0.06 s) | Clic izquierdo en 320, 320; la coma decimal depende de Windows)"

# 2) Borrar "Pulsar B", borrar el último clic y deshacer eso
SelectStep 2; Click "DeleteButton"
SelectStep 2; Click "DeleteButton"
$afterDelete = (Steps).Count
Click "UndoButton"
"2) Tras borrar 2: $afterDelete pasos; tras deshacer: $((Steps).Count) (esperado 2 y 3)"

# 3) Insertar texto "hola" después del primer clic
SelectStep 0
Invoke (Find "InsertButton"); Start-Sleep -Milliseconds 500
$t = $AE::RootElement.FindFirst($TS::Descendants, (Cond $AE::NameProperty "Texto"))
if ($t) { Invoke $t }; Start-Sleep -Milliseconds 800
$box = $AE::RootElement.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty "NameBox"))
if ($box) {
  $box.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("hola")
  Invoke ($AE::RootElement.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty "OkButton")))
}
Start-Sleep -Milliseconds 600
"3) Diálogo de texto=$($null -ne $box)  pasos: " + ((Steps) -join ' | ')

# 4) Cambiar la X del último clic a 300
SelectStep 3
(Find "XBox").GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("300"); Start-Sleep -Milliseconds 300
"4) Último paso: '$((Steps)[3])' (esperado Clic izquierdo en 300, 320)"

# 5) Probar: se oculta, reproduce una vez y vuelve el editor (la macro aún no está guardada)
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800
Click "TestButton"
Start-Sleep -Milliseconds 300
$hidden = $null -eq (Find "StepList")
$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not (Find "StepList") -and $sw.ElapsedMilliseconds -lt 6000) { Start-Sleep -Milliseconds 100 }
$file = Join-Path $data "macros\Prueba.json"
$savedEvents = ((Get-Content $file -Raw | ConvertFrom-Json).Events -split ';').Count
"5) Probar: editor oculto=$hidden  vuelve en $($sw.ElapsedMilliseconds) ms  archivo sin cambios=$($savedEvents -eq 8)"

# 6) Guardar
Click "SaveButton"; Start-Sleep -Milliseconds 400
$json = Get-Content $file -Raw | ConvertFrom-Json
"6) Guardar: editor cerrado=$($null -eq (Find 'StepList'))  principal activa=$($root.Current.IsEnabled)  estado='$(Text 'MacroStatusText')'  eventos=$(($json.Events -split ';').Count) (esperado True True 'Macro guardada' 10)"

# 7) Reproducir la macro guardada desde la ventana principal
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800
(Find "PlayMacroButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 300
$sw = [Diagnostics.Stopwatch]::StartNew()
while ((Text 'MacroStatusText') -ne 'Completado' -and $sw.ElapsedMilliseconds -lt 6000) { Start-Sleep -Milliseconds 50 }
"7) Reproducir: '$(Text 'MacroStatusText')'"

# 8) Relativa a la diana: el clic grabado en 210 cae en 260
MenuItem "Relativa" | Out-Null
(Find "PlayMacroButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 300
$sw = [Diagnostics.Stopwatch]::StartNew()
while ((Text 'MacroStatusText') -ne 'Completado' -and $sw.ElapsedMilliseconds -lt 6000) { Start-Sleep -Milliseconds 50 }
"8) Relativa: '$(Text 'MacroStatusText')'"

# 9) Relativa a una ventana que no está abierta: avisa y no reproduce
MenuItem "Sin ventana" | Out-Null
(Find "PlayMacroButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 500
"9) Sin ventana: '$(Text 'MacroStatusText')'  reproduciendo=$((Find 'PlayMacroButton').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState)"
"   (esperado: No se encuentra la ventana de Juego cerrado..., Off)"

$stuck = @(0x41, 0x42, 0x01) | Where-Object { ([R]::GetAsyncKeyState($_) -band 0x8000) -ne 0 }
"10) Teclas o botones pegados: " + $(if ($stuck) { $stuck -join ',' } else { 'ninguno' })

$proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(4000) | Out-Null
$bench.WaitForExit(); Get-Content $out
"   (esperado diana: 5 clics: (60,60) x3 y (100,120) x2; teclas A A; texto holaaholaa)"
"11) error.log creado: " + (Test-Path (Join-Path $data "error.log")) + " (esperado False)"
[System.Windows.Forms.Cursor]::Position = $original