param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Estadísticas, línea de comandos, accesos directos, exportar/importar perfiles y modo portátil (2.6).
#  1) 5 clics -> estadísticas 5.  2) Macro A -> 1 reproducción.  3) Órdenes desde otra copia: --play, --profile
#     --start, --stop, macro que no existe.  4) Acceso directo de la macro.  5) Exportar e importar el perfil.
#  6) Reiniciar estadísticas.  7) --exit.  8) --play con AutoClik cerrado (arranca oculto).  9) --help.
#  10) Modo portátil (copia del .exe en una carpeta de pruebas): apagar y encender, con reinicio.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(PT p);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [StructLayout(LayoutKind.Sequential)] public struct PT { public int X, Y; }
  public static uint PidAt(int x, int y) { var p = new PT { X = x, Y = y }; uint pid; GetWindowThreadProcessId(GetAncestor(WindowFromPoint(p), 2), out pid); return pid; }
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data16"; if (Test-Path $data) { [IO.Directory]::Delete($data, $true) }
New-Item -ItemType Directory -Force (Join-Path $data "macros") | Out-Null

$out = Join-Path $env:TEMP "diana_p16.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "45" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
Start-Sleep -Milliseconds 1200
# Seguridad: la app va a clicar en estos puntos; si otra ventana tapa la diana, no seguir.
$blocked = @(@(260,260), @(320,320)) | Where-Object { [R]::PidAt($_[0], $_[1]) -ne $bench.Id }
if ($blocked) { "OMITIDA: otra ventana tapa la diana. Apártala y repite."; $bench.Kill(); return }

[IO.File]::WriteAllText((Join-Path $data "settings.json"), '{ "CurrentProfile": "P", "Profiles": { "P": { "Position": "Fixed", "X": 260, "Y": 260, "Repeat": "Count", "RepeatCount": 5, "Interval": 50 }, "Rapido": { "Position": "Fixed", "X": 320, "Y": 320, "Repeat": "Count", "RepeatCount": 3, "Interval": 50 } }, "Prefs": { "MacroName": "A" } }')
[IO.File]::WriteAllText((Join-Path $data "macros\A.json"), '{ "Format": "AutoClik macro", "Version": 1, "Name": "A", "Events": "0,1,260,260,0,0;40000,2,260,260,0,0" }')

function Cond($p, $v) { New-Object Windows.Automation.PropertyCondition($p, $v) }
function Launch([string[]]$extra) {
  Start-Process $AppPath -ArgumentList (@("--data=$data", "--theme=dark") + $extra) -PassThru
}
function Attach($p) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($p.MainWindowHandle -eq 0 -and $sw.ElapsedMilliseconds -lt 8000) { Start-Sleep -Milliseconds 50; $p.Refresh() }
  Start-Sleep -Milliseconds 700
  $script:root = $AE::FromHandle($p.MainWindowHandle)
}
function Find($id) { $script:root.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty $id)) }
function Any($id) { $AE::RootElement.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty $id)) }
function Named($name) { $AE::RootElement.FindFirst($TS::Descendants, (Cond $AE::NameProperty $name)) }
function Invoke($el) { $el.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Toggle($el) { $el.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function Text($id) { (Find $id).Current.Name }
function Stats { Get-Content (Join-Path $data "stats.json") -Raw | ConvertFrom-Json }
function WaitText($id, $value, $ms) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ((Text $id) -ne $value -and $sw.ElapsedMilliseconds -lt $ms) { Start-Sleep -Milliseconds 50 }
  $sw.ElapsedMilliseconds
}
# Diálogo de archivo (Guardar / Abrir) de la app: escribir la ruta y aceptar
function FileDialog($appPid, $path) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $dlg = $null
  while (-not $dlg -and $sw.ElapsedMilliseconds -lt 5000) {
    $dlg = $AE::RootElement.FindFirst($TS::Descendants, (New-Object Windows.Automation.AndCondition((Cond $AE::ClassNameProperty "#32770"), (Cond $AE::ProcessIdProperty $appPid))))
    Start-Sleep -Milliseconds 100
  }
  if (-not $dlg) { return $false }
  Start-Sleep -Milliseconds 500
  # El nombre de archivo tiene el foco al abrirse: se escribe la ruta con el teclado (si el diálogo está delante)
  $h = [IntPtr]$dlg.Current.NativeWindowHandle
  [R]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [R]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
  [R]::SetForegroundWindow($h) | Out-Null
  Start-Sleep -Milliseconds 300
  if ([R]::GetForegroundWindow() -ne $h) { return $false }
  $escaped = [regex]::Replace($path, '[+^%~(){}\[\]]', '{$0}')
  [System.Windows.Forms.SendKeys]::SendWait("^a" + $escaped + "{ENTER}")
  Start-Sleep -Milliseconds 1000
  return $true
}

$proc = Launch @()
Attach $proc
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800

# 1) Clics
Toggle (Find "StartButton")
WaitText 'StatusText' 'Completado' 4000 | Out-Null
Start-Sleep -Milliseconds 300
"1) Tras 5 clics: stats.json clics=$((Stats).Total.Clicks) hoy=$((Stats).Today.Clicks) (esperado 5, 5)"

# 2) Macro A
Toggle (Find "NavMacrosButton"); Start-Sleep -Milliseconds 300
"2a) Antes: '$(Text 'MacroUsageText')' (esperado 'Aún no se ha reproducido')"
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800
Toggle (Find "PlayMacroButton")
WaitText 'MacroStatusText' 'Completado' 4000 | Out-Null
Start-Sleep -Milliseconds 300
"2b) Tras reproducir A: '$(Text 'MacroUsageText')' macros=$((Stats).Total.MacroPlays) clics=$((Stats).Total.Clicks) (esperado 'Reproducida 1 vez · última, hoy a las ...', 1, 6)"

# 3) Órdenes desde otra copia
Launch @("--play", "A") | Out-Null
Start-Sleep -Milliseconds 400
$t = WaitText 'MacroStatusText' 'Completado' 4000
Start-Sleep -Milliseconds 300
"3a) --play A desde otra copia: '$(Text 'MacroStatusText')' reproducciones=$((Stats).Macros.A.Plays) copias=$(@(Get-Process AutoClik -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $proc.Id -and $_.StartTime -gt $proc.StartTime }).Count) (esperado Completado, 2, 0)"
Launch @("--profile", "Rapido", "--start") | Out-Null
Start-Sleep -Milliseconds 400
Toggle (Find "NavClicksButton"); Start-Sleep -Milliseconds 200
WaitText 'StatusText' 'Completado' 4000 | Out-Null
"3b) --profile Rapido --start: perfil='$(Text 'ProfileNameText')' estado='$(Text 'StatusText')' contador=$(Text 'CountText') (esperado Rapido, Completado, 3)"
Launch @("--profile", "P") | Out-Null
Start-Sleep -Milliseconds 800
Launch @("--toggle") | Out-Null
Start-Sleep -Milliseconds 300
$running = Text 'StatusText'
Launch @("--stop") | Out-Null
Start-Sleep -Milliseconds 600
"3c) --toggle y --stop: durante='$running' después='$(Text 'StatusText')' perfil='$(Text 'ProfileNameText')' (esperado 'Clicando' o 'Completado', Detenido/Completado, P)"
Launch @("--play", "`"No existe`"") | Out-Null
Start-Sleep -Milliseconds 800
"3d) Macro que no existe: '$(Text 'StatusText')' (esperado 'No existe la macro «No existe»')"

# 4) Acceso directo de la macro
Toggle (Find "NavMacrosButton"); Start-Sleep -Milliseconds 300
Invoke (Find "MacroPickButton"); Start-Sleep -Milliseconds 500
Invoke (Named "Crear acceso directo en el escritorio"); Start-Sleep -Milliseconds 800
$lnk = Join-Path $data "desktop\AutoClik - A.lnk"
$args4 = if (Test-Path $lnk) { (New-Object -ComObject WScript.Shell).CreateShortcut($lnk).Arguments } else { "" }
"4) Acceso directo: existe=$(Test-Path $lnk) argumentos=[$args4] aviso='$(Text 'MacroStatusText')' (esperado True, --data=... --play ""A"", 'Acceso directo creado en el escritorio')"

# 5) Exportar e importar el perfil
Toggle (Find "NavClicksButton"); Start-Sleep -Milliseconds 300
$export = Join-Path $data "P exportado.autoclik-profile"
Invoke (Find "ProfileButton"); Start-Sleep -Milliseconds 500
Invoke (Named "Exportar perfil…")
$ok = FileDialog $proc.Id $export
$exp = if (Test-Path $export) { Get-Content $export -Raw | ConvertFrom-Json } else { $null }
"5a) Exportar: diálogo=$ok archivo=$(Test-Path $export) formato='$($exp.Format)' nombre='$($exp.Name)' repetir=$($exp.Profile.RepeatCount) (esperado True True 'AutoClik profile' 'P' 5)"
Invoke (Find "ProfileButton"); Start-Sleep -Milliseconds 500
Invoke (Named "Importar perfil…")
$ok = FileDialog $proc.Id $export
Start-Sleep -Milliseconds 300
$saved = Get-Content (Join-Path $data "settings.json") -Raw | ConvertFrom-Json
"5b) Importar: diálogo=$ok perfil='$(Text 'ProfileNameText')' perfiles='$(@($saved.Profiles.PSObject.Properties.Name) -join ', ')' estado='$(Text 'StatusText')' (esperado True, 'P (2)', 'P, Rapido, P (2)', 'Perfil «P (2)» importado')"

# 6) Estadísticas en Ajustes y reiniciar
Toggle (Find "SettingsButton"); Start-Sleep -Milliseconds 400
$before = "clics=$(Text 'StatClicksText') teclas=$(Text 'StatKeysText') macros=$(Text 'StatMacrosText')"
Invoke (Find "StatsResetButton"); Start-Sleep -Milliseconds 600
$okButton = $AE::RootElement.FindAll($TS::Descendants, (Cond $AE::AutomationIdProperty "OkButton")) | Where-Object { $_.Current.ProcessId -eq $proc.Id } | Select-Object -First 1
if ($okButton) { Invoke $okButton; Start-Sleep -Milliseconds 400 }
"6) Ajustes antes: $before; tras Reiniciar: clics=$(Text 'StatClicksText') archivo=$((Stats).Total.Clicks) (esperado antes clics=15 aprox. y macros=2; después 0 y 0)"

# 7) --exit
Launch @("--exit") | Out-Null
$closed = $proc.WaitForExit(5000)
"7) --exit desde otra copia: cerrada=$closed (esperado True)"

# 8) --play con AutoClik cerrado: arranca oculto, la reproduce y se queda en la bandeja
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800
$cold = Launch @("--play", "A")
Start-Sleep -Milliseconds 2500
$cold.Refresh()
$visible = $cold.MainWindowHandle -ne 0 -and [R]::IsWindowVisible($cold.MainWindowHandle)
"8) --play con AutoClik cerrado: sigue abierto=$(-not $cold.HasExited) ventana visible=$visible reproducciones de A=$((Stats).Macros.A.Plays) (esperado True, False, 1)"
Launch @("--exit") | Out-Null
$cold.WaitForExit(5000) | Out-Null
if (-not $cold.HasExited) { $cold.Kill() }

# 9) --help
$help = Launch @("--help")
Start-Sleep -Milliseconds 1200
$box = $AE::RootElement.FindFirst($TS::Children, (New-Object Windows.Automation.AndCondition((Cond $AE::ProcessIdProperty $help.Id), (Cond $AE::NameProperty "AutoClik"))))
$helpText = if ($box) { ($box.FindAll($TS::Descendants, [Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name }) -join ' ' } else { "" }
"9) --help: ventana=$($null -ne $box) menciona --play=$($helpText -like '*--play*') (esperado True, True)"
if (-not $help.HasExited) { $help.Kill() }

# 10) Modo portátil, en una copia del .exe dentro de la carpeta de pruebas
$pdir = Join-Path $Dir "portable16"; if (Test-Path $pdir) { [IO.Directory]::Delete($pdir, $true) }
New-Item -ItemType Directory -Force (Join-Path $pdir "AutoClik-datos") | Out-Null
$pexe = Join-Path $pdir "AutoClik.exe"
Copy-Item $AppPath $pexe
$normal = Join-Path $Dir "data16n"; if (Test-Path $normal) { [IO.Directory]::Delete($normal, $true) }
$pproc = Start-Process $pexe -ArgumentList "--data=$normal", "--theme=dark" -PassThru
Attach $pproc
Toggle (Find "PinButton"); Start-Sleep -Milliseconds 1200
Toggle (Find "SettingsButton"); Start-Sleep -Milliseconds 300
"10a) Portátil: ajustes en AutoClik-datos=$(Test-Path (Join-Path $pdir 'AutoClik-datos\settings.json')) en la normal=$(Test-Path (Join-Path $normal 'settings.json')) interruptor=$((Find 'PortableSwitch').GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState) (esperado True, False, On)"
Toggle (Find "PinButton"); Start-Sleep -Milliseconds 300
Toggle (Find "PortableSwitch"); Start-Sleep -Milliseconds 700
$restart = $AE::RootElement.FindAll($TS::Descendants, (Cond $AE::AutomationIdProperty "OkButton")) | Where-Object { $_.Current.ProcessId -eq $pproc.Id } | Select-Object -First 1
if ($restart) { Invoke $restart }
$pproc.WaitForExit(5000) | Out-Null
Start-Sleep -Milliseconds 1500
$next = Get-Process AutoClik -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $pexe } | Select-Object -First 1
"10b) Desactivar: diálogo=$($null -ne $restart) se reinició=$($pproc.HasExited -and $null -ne $next) carpeta portátil borrada=$(-not (Test-Path (Join-Path $pdir 'AutoClik-datos'))) ajustes en la normal=$(Test-Path (Join-Path $normal 'settings.json')) (esperado True, True, True, True)"
if ($next) {
  Attach $next
  Toggle (Find "SettingsButton"); Start-Sleep -Milliseconds 300
  Toggle (Find "PortableSwitch"); Start-Sleep -Milliseconds 700
  $restart = $AE::RootElement.FindAll($TS::Descendants, (Cond $AE::AutomationIdProperty "OkButton")) | Where-Object { $_.Current.ProcessId -eq $next.Id } | Select-Object -First 1
  if ($restart) { Invoke $restart }
  $next.WaitForExit(5000) | Out-Null
  Start-Sleep -Milliseconds 1500
  $third = Get-Process AutoClik -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $pexe } | Select-Object -First 1
  "10c) Activar: se reinició=$($next.HasExited -and $null -ne $third) ajustes copiados a AutoClik-datos=$(Test-Path (Join-Path $pdir 'AutoClik-datos\settings.json')) (esperado True, True)"
  if ($third) { $third.CloseMainWindow() | Out-Null; $third.WaitForExit(4000) | Out-Null; if (-not $third.HasExited) { $third.Kill() } }
}
Get-Process AutoClik -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $pexe } | Stop-Process -Force

# 10d) El .exe en la RAÍZ de una unidad (como un USB: D:\AutoClik.exe). En 2.6 sin publicar, "D:" + carpeta daba una
# ruta relativa y, con una regla de imagen, AutoClik se quedaba abierto sin ventana. Se simula con subst.
$letter = 'RSTUVWXY'.ToCharArray() | Where-Object { -not (Test-Path "$($_):\") } | Select-Object -First 1
if ($letter) {
  $rootDir = Join-Path $Dir "raiz16"; if (Test-Path $rootDir) { [IO.Directory]::Delete($rootDir, $true) }
  New-Item -ItemType Directory -Force (Join-Path $rootDir "AutoClik-datos\images") | Out-Null
  $png = New-Object Drawing.Bitmap 4, 4; $png.Save((Join-Path $rootDir "AutoClik-datos\images\img-prueba.png")); $png.Dispose()
  [IO.File]::WriteAllText((Join-Path $rootDir "AutoClik-datos\settings.json"), '{ "CurrentProfile": "P", "Profiles": { "P": { "WatchRules": [ { "Kind": "Image", "Image": "img-prueba.png" } ] } }, "Prefs": { "Experimental": true } }')
  Copy-Item $AppPath (Join-Path $rootDir "AutoClik.exe")
  subst "$($letter):" $rootDir | Out-Null
  $rexe = "$($letter):\AutoClik.exe"
  $normal2 = Join-Path $Dir "data16r"; if (Test-Path $normal2) { [IO.Directory]::Delete($normal2, $true) }
  $rproc = Start-Process $rexe -ArgumentList "--data=$normal2", "--theme=dark" -PassThru
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($rproc.MainWindowHandle -eq 0 -and -not $rproc.HasExited -and $sw.ElapsedMilliseconds -lt 8000) { Start-Sleep -Milliseconds 100; $rproc.Refresh() }
  $shown = $rproc.MainWindowHandle -ne 0 -and [R]::IsWindowVisible($rproc.MainWindowHandle)
  $rlog = Join-Path $rootDir "AutoClik-datos\error.log"
  "10d) En la raíz de una unidad con regla de imagen: ventana visible=$shown error.log=$(Test-Path $rlog) (esperado True, False)"
  if ($shown) {
    # Y desactivar/activar desde la raíz también funciona
    Attach $rproc
    Toggle (Find "SettingsButton"); Start-Sleep -Milliseconds 300
    Toggle (Find "PortableSwitch"); Start-Sleep -Milliseconds 700
    $restart = $AE::RootElement.FindAll($TS::Descendants, (Cond $AE::AutomationIdProperty "OkButton")) | Where-Object { $_.Current.ProcessId -eq $rproc.Id } | Select-Object -First 1
    if ($restart) { Invoke $restart }
    $rproc.WaitForExit(5000) | Out-Null
    Start-Sleep -Milliseconds 1500
    $rnext = Get-Process AutoClik -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $rproc.Id -and $_.StartTime -gt $rproc.StartTime -and $_.Path -like "*AutoClik.exe" } | Select-Object -First 1
    $rnext2 = if ($rnext) { $rnext.Refresh(); $rnext.MainWindowHandle -ne 0 } else { $false }
    "10e) Desactivar en la raíz: reiniciada con ventana=$rnext2 carpeta borrada=$(-not (Test-Path (Join-Path $rootDir 'AutoClik-datos'))) imagen en la normal=$(Test-Path (Join-Path $normal2 'images\img-prueba.png')) (esperado True, True, True)"
  }
  Get-Process AutoClik -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -gt $rproc.StartTime.AddSeconds(-1) -and ($_.Path -eq $rexe -or $_.Path -eq (Join-Path $rootDir "AutoClik.exe")) } | Stop-Process -Force
  if (-not $rproc.HasExited) { $rproc.Kill() }
  Start-Sleep -Milliseconds 500
  subst "$($letter):" /d | Out-Null
} else { "10d) OMITIDA: no hay ninguna letra de unidad libre para simular la raíz" }

$bench.WaitForExit(); Get-Content $out
"   (esperado diana: (60,60) unos 13 = 5 clics + hasta 5 de --toggle + A x3; (120,120) x3 = perfil Rapido)"
"11) error.log creado: " + ((Test-Path (Join-Path $data "error.log")) -or (Test-Path (Join-Path $normal "error.log"))) + " (esperado False)"
[System.Windows.Forms.Cursor]::Position = $original
