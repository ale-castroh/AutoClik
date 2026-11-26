param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Programar y cadenas (2.5). Con --schedule-seconds ("Dentro de" en segundos), --power-test (apagar y
# suspender solo se anotan en power-test.txt: NUNCA se apaga el PC) y --countdown-seconds=2.
#  1) Clics "dentro de 2 s" + "al terminar: apagar": empiezan solos, 5 clics, cuenta atrás, se "apaga".
#  2) Programar y cancelar con el reloj.  3) Al terminar: suspender, pero Cancelar en la cuenta atrás.
#  4) Cadena A x2 -> pausa -> B.  5) "Nueva cadena…" desde el menú.  6) Macro programada "ahora" + avisar.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(PT p);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [StructLayout(LayoutKind.Sequential)] public struct PT { public int X, Y; }
  public static uint PidAt(int x, int y) { var p = new PT { X = x, Y = y }; uint pid; GetWindowThreadProcessId(GetAncestor(WindowFromPoint(p), 2), out pid); return pid; }
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data15"; if (Test-Path $data) { [IO.Directory]::Delete($data, $true) }
New-Item -ItemType Directory -Force (Join-Path $data "macros") | Out-Null

$out = Join-Path $env:TEMP "diana_p15.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "36" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
Start-Sleep -Milliseconds 1200
# Seguridad: la app va a clicar en estos puntos; si otra ventana tapa la diana, no seguir.
$blocked = @(@(260,260), @(320,320)) | Where-Object { [R]::PidAt($_[0], $_[1]) -ne $bench.Id }
if ($blocked) { "OMITIDA: otra ventana tapa la diana. Apártala y repite."; $bench.Kill(); return }

[IO.File]::WriteAllText((Join-Path $data "settings.json"), '{ "CurrentProfile": "P", "Profiles": { "P": { "Position": "Fixed", "X": 260, "Y": 260, "Repeat": "Count", "RepeatCount": 5, "Interval": 60 } } }')
function MacroFile($name, $events, $extra = '') {
  [IO.File]::WriteAllText((Join-Path $data "macros\$name.json"), "{ `"Format`": `"AutoClik macro`", `"Version`": 1, `"Name`": `"$name`", `"Events`": `"$events`" $extra }")
}
MacroFile "A" "0,1,260,260,0,0;40000,2,260,260,0,0"
MacroFile "B" "0,1,320,320,0,0;40000,2,320,320,0,0"
MacroFile "Ronda" "" ', "Chain": [ { "Macro": "A", "Times": 2, "PauseMs": 300 }, { "Macro": "B", "Times": 1, "PauseMs": 0 } ]'

$power = Join-Path $data "power-test.txt"
$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--theme=dark", "--schedule-seconds", "--power-test", "--countdown-seconds=2" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Cond($p, $v) { New-Object Windows.Automation.PropertyCondition($p, $v) }
function Find($id) { $root.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty $id)) }
function Any($id) { $AE::RootElement.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty $id)) }
function Named($name) { $AE::RootElement.FindFirst($TS::Descendants, (Cond $AE::NameProperty $name)) }
function Invoke($el) { $el.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Text($id) { (Find $id).Current.Name }
function Help($id) { (Find $id).Current.HelpText }
function Sel($el) { $el.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function SetText($el, $v) { $el.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($v) }
function Lines { if (Test-Path $power) { @(Get-Content $power) } else { @() } }

# Programar con el diálogo: modo (Now/In), segundos y acción al terminar
function Schedule($button, $mode, $seconds, $endName) {
  Invoke (Find $button); Start-Sleep -Milliseconds 700
  Sel (Any "$($mode)Radio"); Start-Sleep -Milliseconds 150
  if ($mode -eq 'In') { SetText (Any "InBox") "$seconds" }
  Invoke (Any "EndButton"); Start-Sleep -Milliseconds 500
  Invoke (Named $endName); Start-Sleep -Milliseconds 300
  Invoke (Any "OkButton"); Start-Sleep -Milliseconds 500
}
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800

# 1) Dentro de 2 s, al terminar apagar
Schedule "ClickScheduleButton" "In" 2 "Apagar el PC"
"1a) Programado: estado='$(Text 'StatusText')' reloj=$(Help 'ClickScheduleButton') (esperado 'Empieza a las ... (en 2 s)', on)"
$sw = [Diagnostics.Stopwatch]::StartNew()
while ((Text 'StatusText') -notlike 'Clicando*' -and $sw.ElapsedMilliseconds -lt 5000) { Start-Sleep -Milliseconds 50 }
"1b) Empieza solo tras $($sw.ElapsedMilliseconds) ms: '$(Text 'StatusText')' (esperado ~2000 ms, 'Clicando · al terminar: apagar el pc')"
while ($null -eq (Any "NowButton") -and $sw.ElapsedMilliseconds -lt 8000) { Start-Sleep -Milliseconds 50 }
"1c) Cuenta atrás visible=$($null -ne (Any 'NowButton'))  contador=$(Text 'CountText')"
Start-Sleep -Milliseconds 3000
"1d) power-test.txt: '$((Lines) -join ',')' cuenta atrás cerrada=$($null -eq (Any 'NowButton')) (esperado 'Shutdown', True)"

# 2) Programar y cancelar con el reloj
Schedule "ClickScheduleButton" "In" 30 "Nada"
$before = Help 'ClickScheduleButton'
Invoke (Find "ClickScheduleButton"); Start-Sleep -Milliseconds 400
"2) Reloj antes=$before después=$(Help 'ClickScheduleButton') estado='$(Text 'StatusText')' (esperado on, off, 'Programación cancelada')"

# 3) Ahora + suspender, pero Cancelar en la cuenta atrás
Schedule "ClickScheduleButton" "Now" 0 "Suspender el PC"
$sw.Restart()
while ($null -eq (Any "NowButton") -and $sw.ElapsedMilliseconds -lt 6000) { Start-Sleep -Milliseconds 50 }
$cd = Any "NowButton"
$cancel = $AE::RootElement.FindAll($TS::Descendants, (Cond $AE::AutomationIdProperty "CancelButton")) | Select-Object -First 1
if ($cancel) { Invoke $cancel }
Start-Sleep -Milliseconds 2500
"3) Cuenta atrás vista=$($null -ne $cd) power-test.txt='$((Lines) -join ',')' estado='$(Text 'StatusText')' (esperado True, 'Shutdown' sin Sleep, 'Apagado cancelado')"

# 4) Cadena
(Find "NavMacrosButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep -Milliseconds 300
Invoke (Find "MacroPickButton"); Start-Sleep -Milliseconds 500
Invoke (Named "Ronda"); Start-Sleep -Milliseconds 400
"4a) Cadena elegida: macro='$(Text 'MacroNameText')' detalle='$(Text 'MacroDetailText')' (esperado 'Ronda', 'Cadena · 2 macros')"
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800
$sw.Restart()
(Find "PlayMacroButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Milliseconds 100
while ((Text 'MacroStatusText') -ne 'Completado' -and $sw.ElapsedMilliseconds -lt 6000) { Start-Sleep -Milliseconds 30 }
"4b) Reproducir la cadena: '$(Text 'MacroStatusText')' en $($sw.ElapsedMilliseconds) ms (esperado Completado, ~0,5 s: A, A, pausa 0,3 s, B)"

# 5) Nueva cadena desde el menú
Invoke (Find "MacroPickButton"); Start-Sleep -Milliseconds 500
Invoke (Named "Nueva cadena…"); Start-Sleep -Milliseconds 800
SetText (Any "NameBox") "Otra"
Invoke (Any "AddButton"); Start-Sleep -Milliseconds 500
Invoke (Named "B"); Start-Sleep -Milliseconds 300
SetText (Any "TimesBox") "3"; Start-Sleep -Milliseconds 200
Invoke (Any "OkButton"); Start-Sleep -Milliseconds 600
$otra = Join-Path $data "macros\Otra.json"
$chain = if (Test-Path $otra) { (Get-Content $otra -Raw | ConvertFrom-Json).Chain } else { $null }
"5) Nueva cadena: archivo=$(Test-Path $otra) elementos='$(@($chain | ForEach-Object { "$($_.Macro) x$($_.Times)" }) -join ', ')' macro='$(Text 'MacroNameText')' (esperado True, 'B x3', 'Otra')"

# 6) Macro "Ronda" programada ahora + avisar (notificación; nada se apaga)
Invoke (Find "MacroPickButton"); Start-Sleep -Milliseconds 500
Invoke (Named "Ronda"); Start-Sleep -Milliseconds 400
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800
Schedule "MacroScheduleButton" "Now" 0 "Avisar"
$sw.Restart()
while ((Text 'MacroStatusText') -ne 'Completado' -and $sw.ElapsedMilliseconds -lt 6000) { Start-Sleep -Milliseconds 30 }
Start-Sleep -Milliseconds 300
"6) Macro programada ahora + avisar: '$(Text 'MacroStatusText')' reloj=$(Help 'MacroScheduleButton') power-test.txt='$((Lines) -join ',')' (esperado Completado, off, 'Shutdown')"

$proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(4000) | Out-Null
if (-not $proc.HasExited) { $proc.Kill() }
$bench.WaitForExit(); Get-Content $out
"   (esperado diana: (60,60) x14 = 5 + 5 clics y A x2 en cada una de las 2 cadenas; (120,120) x2 = B en cada cadena)"
"7) error.log creado: " + (Test-Path (Join-Path $data "error.log")) + " (esperado False)"
[System.Windows.Forms.Cursor]::Position = $original
