param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Clic por permanencia y buscar actualizaciones (2.7).
#  Permanencia: clica al quedarse quieto, no repite, ignora el temblor, derecho, apagado no clica, esquina lo apaga.
#  Actualizaciones (sin internet: --update-test=archivo): versión nueva, la misma, respuestas maliciosas o enormes,
#  apagado por defecto (no comprueba solo) y "Buscar ahora".
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(PT p);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [StructLayout(LayoutKind.Sequential)] public struct PT { public int X, Y; }
  public static uint PidAt(int x, int y) { var p = new PT { X = x, Y = y }; uint pid; GetWindowThreadProcessId(GetAncestor(WindowFromPoint(p), 2), out pid); return pid; }
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
function Cond($p, $v) { New-Object Windows.Automation.PropertyCondition($p, $v) }
function Find($id) { $script:root.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty $id)) }
function Invoke($el) { $el.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Toggle($el) { $el.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState }
function Sel($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function Text($id) { (Find $id).Current.Name }
function Value($id) { (Find $id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value }
function Launch($data, [string[]]$extra) {
  $p = Start-Process $AppPath -ArgumentList (@("--data=$data", "--theme=dark") + $extra) -PassThru
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($p.MainWindowHandle -eq 0 -and $sw.ElapsedMilliseconds -lt 8000) { Start-Sleep -Milliseconds 50; $p.Refresh() }
  Start-Sleep -Milliseconds 700
  $script:root = $AE::FromHandle($p.MainWindowHandle)
  $p
}
function Close($p) { $p.CloseMainWindow() | Out-Null; $p.WaitForExit(4000) | Out-Null; if (-not $p.HasExited) { $p.Kill() } }
function NewData($name, $json) {
  $d = Join-Path $Dir $name; if (Test-Path $d) { [IO.Directory]::Delete($d, $true) }
  New-Item -ItemType Directory -Force $d | Out-Null
  if ($json) { [IO.File]::WriteAllText((Join-Path $d "settings.json"), $json) }
  $d
}

# ================= Clic por permanencia =================
$out = Join-Path $env:TEMP "diana_p17.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "30" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
Start-Sleep -Milliseconds 1200
# El cursor empieza ya sobre la diana: la permanencia solo clica donde se deja quieto el ratón
[R]::SetCursorPos(250, 250) | Out-Null
$data = NewData "data17" '{ "Prefs": { "DwellEnabled": true, "DwellMs": 500, "DwellTolerance": 6, "Experimental": false } }'
$proc = Launch $data @()
Toggle (Find "SettingsButton"); Start-Sleep -Milliseconds 400   # los controles de Ajustes solo se ven con la sección abierta
# Seguridad: la app va a clicar en estos puntos; si otra ventana tapa la diana, no seguir.
$blocked = @(@(250,250), @(260,260), @(263,262), @(320,320)) | Where-Object { [R]::PidAt($_[0], $_[1]) -ne $bench.Id }
if ($blocked) { "OMITIDA: otra ventana tapa la diana. Apártala y repite."; Close $proc; $bench.Kill(); return }

"1) Al abrir: interruptor=$(Tog 'DwellSwitch') espera='$(Value 'DwellDelayBox')' temblor='$(Value 'DwellToleranceBox')' atajo='$(Text 'DwellHotkeyButton')' (esperado On, 0,5, 6, Ninguno)"
Start-Sleep -Milliseconds 900   # quieto desde el principio, sin haber movido: no clica
[R]::SetCursorPos(260, 260) | Out-Null; Start-Sleep -Milliseconds 900   # se mueve y se queda quieto: 1 clic
Start-Sleep -Milliseconds 900                                            # sigue quieto: no repite
[R]::SetCursorPos(263, 262) | Out-Null; Start-Sleep -Milliseconds 900   # temblor de 3 px: no cuenta
[R]::SetCursorPos(320, 320) | Out-Null; Start-Sleep -Milliseconds 300   # se mueve y se va antes de tiempo
[R]::SetCursorPos(250, 250) | Out-Null; Start-Sleep -Milliseconds 900   # quieto en otro sitio: 1 clic
"2) Movimientos hechos (esperado en la diana: 2 clics izquierdos, en (60,60) y (40,40); ninguno en (120,120))"
Sel "DwellRightRadio"; Start-Sleep -Milliseconds 200
[R]::SetCursorPos(320, 320) | Out-Null; Start-Sleep -Milliseconds 900   # clic derecho
Sel "DwellDoubleRadio"; Start-Sleep -Milliseconds 200
[R]::SetCursorPos(260, 260) | Out-Null; Start-Sleep -Milliseconds 900   # doble clic
Sel "DwellLeftRadio"; Start-Sleep -Milliseconds 200
"3) Derecho en (120,120) y doble en (60,60) hechos"
# Repetir "Sin fin": quieto = clica sin parar (intervalo de Clics: 100 ms); mover = para
Sel "DwellForeverRadio"; Start-Sleep -Milliseconds 200
[R]::SetCursorPos(320, 320) | Out-Null; Start-Sleep -Milliseconds 1200  # 0,5 s de círculo + ~0,7 s clicando
[R]::SetCursorPos(330, 330) | Out-Null; Start-Sleep -Milliseconds 150   # se mueve: para
Toggle (Find "DwellSwitch"); Start-Sleep -Milliseconds 300               # apagar antes de que vuelva a llenarse
$runStats = (Get-Content (Join-Path $data "stats.json") -Raw | ConvertFrom-Json).Total.Clicks
"3b) Sin fin: clics contados en estadísticas=$runStats sigue el interruptor=$(Tog 'DwellSwitch') (esperado unos 6-8, Off; la diana suma esos clics en (120,120))"
Sel "DwellOnceRadio"; Start-Sleep -Milliseconds 200
Toggle (Find "DwellSwitch"); Start-Sleep -Milliseconds 200
Toggle (Find "DwellSwitch"); Start-Sleep -Milliseconds 200
[R]::SetCursorPos(320, 320) | Out-Null; Start-Sleep -Milliseconds 900   # apagado: no clica
"4) Apagado: interruptor=$(Tog 'DwellSwitch') (esperado Off; la diana no suma nada en (120,120) después del derecho)"
Toggle (Find "DwellSwitch"); Start-Sleep -Milliseconds 200
[R]::SetCursorPos(0, 0) | Out-Null; Start-Sleep -Milliseconds 300       # esquina: se apaga solo
$corner = Tog 'DwellSwitch'
Toggle (Find "NavClicksButton"); Start-Sleep -Milliseconds 300
"5) Esquina: interruptor=$corner aviso='$(Text 'StatusText')' (esperado Off, 'Clic por permanencia apagado (esquina)')"
Close $proc
$saved = Get-Content (Join-Path $data "settings.json") -Raw | ConvertFrom-Json
"6) Guardado: encendido=$($saved.Prefs.DwellEnabled) espera=$($saved.Prefs.DwellMs) clic=$($saved.Prefs.DwellClick) (esperado False, 500, Left)"
[R]::SetCursorPos($original.X, $original.Y) | Out-Null
$bench.WaitForExit(); Get-Content $out
"   (esperado diana: izquierdos (60,60) x3 [1 + 2 del doble], (40,40) x1 (la diana agrupa de 20 en 20 px) y (120,120) los de Sin fin; derecho (120,120) x1)"

# ================= Buscar actualizaciones =================
function Feed($name, $content) { $f = Join-Path $Dir $name; [IO.File]::WriteAllText($f, $content); $f }
$newer = Feed "upd-newer.json" '{ "tag_name": "v9.9.9", "html_url": "https://ejemplo-malicioso.invalid/descarga.exe", "draft": false, "prerelease": false }'
$same = Feed "upd-same.json" '{ "tag_name": "v2.7.0" }'
$evil = Feed "upd-evil.json" '{ "tag_name": "v9.9.9 & calc.exe" }'
$evil2 = Feed "upd-evil2.json" '{ "tag_name": "https://ejemplo-malicioso.invalid" }'
$pre = Feed "upd-pre.json" '{ "tag_name": "v9.9.9", "prerelease": true }'
$huge = Join-Path $Dir "upd-huge.json"; [IO.File]::WriteAllText($huge, '{ "tag_name": "v9.9.9", "x": "' + ('a' * 700000) + '" }')

function UpdateCase($label, $file, $on, [scriptblock]$after) {
  $d = NewData "data17u" ($(if ($on) { '{ "Prefs": { "UpdateCheck": true } }' } else { $null }))
  $p = Launch $d @("--update-test=$file")
  Toggle (Find "SettingsButton"); Start-Sleep -Milliseconds 1800
  if ($after) { & $after }
  $banner = $null -ne (Find "OpenReleasesButton")
  $line = "$label estado='$(Text 'UpdateStatusText')' aviso=$banner texto='$(Text 'UpdateBannerText')' puntito=$((Find 'SettingsButton').Current.HelpText)"
  Close $p
  $line
}
UpdateCase "7) Encendido, versión nueva:" $newer $true $null
"   (esperado: 'Última comprobación: ...', True, 'Hay una versión nueva: 9.9.9', update; el botón abre siempre github.com/ale-castroh/AutoClik/releases)"
UpdateCase "8) Encendido, la misma versión:" $same $true $null
"   (esperado: '... · tienes la última versión', False, '', puntito='Ajustes' = sin aviso)"
UpdateCase "9) Versión con texto extra:" $evil $true $null
UpdateCase "10) Una dirección en vez de versión:" $evil2 $true $null
UpdateCase "11) Versión de prueba (prerelease):" $pre $true $null
UpdateCase "12) Respuesta enorme:" $huge $true $null
"   (esperado 9-12: 'No se pudo comprobar (¿sin internet?)', False, '')"
UpdateCase "13) Apagado (por defecto), sin tocar nada:" $newer $false $null
"   (esperado: 'Última comprobación: nunca', False: apagado no comprueba solo)"
UpdateCase "14) Apagado + Buscar ahora:" $newer $false { Invoke (Find "CheckUpdatesButton"); Start-Sleep -Milliseconds 1200 }
"   (esperado: 'Última comprobación: ...', True, 'Hay una versión nueva: 9.9.9')"
$err = @(Get-ChildItem $Dir -Filter error.log -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.DirectoryName -like '*data17' })
"15) error.log de la permanencia: $($err.Count -gt 0) (esperado False; en las actualizaciones fallidas sí se anota el motivo)"
[System.Windows.Forms.Cursor]::Position = $original
