param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Vigilar (2.4, experimental): una regla de color (morado de la diana -> clic en otro punto) y otra de
# imagen (el cuadrado amarillo de la diana -> clic encima). Salta al aparecer, no repite mientras sigue
# a la vista, F10 deja de vigilar. Y el paso de macro "Esperar a un color": uno que ya está (sigue al
# momento) y otro que no llega (espera su máximo, 1 s, y sigue).
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(PT p);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [StructLayout(LayoutKind.Sequential)] public struct PT { public int X, Y; }
  public static uint PidAt(int x, int y) { var p = new PT { X = x, Y = y }; uint pid; GetWindowThreadProcessId(GetAncestor(WindowFromPoint(p), 2), out pid); return pid; }
  public static void Key(byte vk) { byte sc = (byte)MapVirtualKey(vk, 0); keybd_event(vk, sc, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(40); keybd_event(vk, sc, 2, UIntPtr.Zero); }
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data14"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $data "images"), (Join-Path $data "macros") | Out-Null

$out = Join-Path $env:TEMP "diana_p14.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "22" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
Start-Sleep -Milliseconds 1200
# Seguridad: la app va a clicar en estos puntos. Si encima de la diana hay otra ventana (p. ej. un Paint
# tuyo), los clics irían a ella: no seguir.
$blocked = @(@(260,260), @(300,300), @(320,320), @(375,235)) | Where-Object { [R]::PidAt($_[0], $_[1]) -ne $bench.Id }
if ($blocked) {
  "OMITIDA: otra ventana tapa la diana en " + (($blocked | ForEach-Object { "($($_[0]),$($_[1]))" }) -join ' ') + ". Apártala y repite."
  $bench.Kill(); return
}
# La imagen a buscar: el cuadrado amarillo tal como se ve en pantalla
$bmp = New-Object Drawing.Bitmap 30, 30
[Drawing.Graphics]::FromImage($bmp).CopyFromScreen(360, 220, 0, 0, $bmp.Size)
$bmp.Save((Join-Path $data "images\cuadrado.png"), [Drawing.Imaging.ImageFormat]::Png)
$purple = [Drawing.Color]::MediumPurple; $rgb = ([int]$purple.R -shl 16) -bor ([int]$purple.G -shl 8) -bor [int]$purple.B

[IO.File]::WriteAllText((Join-Path $data "settings.json"), @"
{ "CurrentProfile": "P", "Prefs": { "Experimental": true },
  "Profiles": { "P": { "WatchRules": [
    { "Kind": "Color", "X": 300, "Y": 300, "Color": $rgb, "Tolerance": 10, "Action": "ClickAt", "ActionX": 260, "ActionY": 260, "EveryTime": true, "Enabled": true },
    { "Kind": "Image", "Image": "cuadrado.png", "Tolerance": 10, "Action": "ClickOn", "EveryTime": false, "Enabled": true },
    { "Kind": "Color", "X": 300, "Y": 300, "Color": 65280, "Action": "StopAll", "Enabled": false } ] } } }
"@)
# Macros con "Esperar a un color" (tipo 8): Flags = tolerancia 10 + segundos * 256
$flags = 10 + 2 * 256
[IO.File]::WriteAllText((Join-Path $data "macros\Espera morado.json"),
  "{ `"Format`": `"AutoClik macro`", `"Version`": 1, `"Name`": `"Espera morado`", `"Events`": `"0,8,300,300,$rgb,$flags;100000,1,320,320,0,0;140000,2,320,320,0,0`" }")
$flags1 = 10 + 1 * 256
[IO.File]::WriteAllText((Join-Path $data "macros\Espera verde.json"),
  "{ `"Format`": `"AutoClik macro`", `"Version`": 1, `"Name`": `"Espera verde`", `"Events`": `"0,8,300,300,65280,$flags1;100000,1,320,320,0,0;140000,2,320,320,0,0`" }")

$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--theme=dark" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Find($id) { $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function State($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState }
function Text($id) { (Find $id).Current.Name }
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800

Tog "NavWatchButton"; Start-Sleep -Milliseconds 400
"1) Sección: visible=$($null -ne (Find 'WatchToggle'))  reglas=$((Find 'Rule0').Current.Name) | $((Find 'Rule1').Current.Name) | $((Find 'Rule2').Current.Name)  activa 3=$(State 'RuleSwitch2')"
"   (esperado: True, 'Color en 300, 300 | Imagen en toda la pantalla | Color en 300, 300', Off)"

Tog "WatchToggle"; Start-Sleep -Milliseconds 1500
"2) Vigilando: estado='$(Text 'WatchStatusText')'  veces=$(Text 'WatchCountText') (esperado 'Vigilando · 2 reglas', 2)"
Start-Sleep -Milliseconds 1200
"3) Sigue a la vista: veces=$(Text 'WatchCountText') (esperado 2: no repite)"
[R]::Key(0x79); Start-Sleep -Milliseconds 500   # F10
"4) Tras F10: $(State 'WatchToggle') estado='$(Text 'WatchStatusText')' (esperado Off, 'Sin vigilar')"
# 4b) Dejar de vigilar responde al momento (en 2.4 en pruebas se congelaba buscando en zonas de un color)
Tog "WatchToggle"; Start-Sleep -Milliseconds 800
$sw = [Diagnostics.Stopwatch]::StartNew(); Tog "WatchToggle"; $ms = $sw.ElapsedMilliseconds
"4b) Dejar de vigilar con el botón: $(State 'WatchToggle') en $ms ms (esperado Off, menos de 500 ms)"

# Esperar a un color
Tog "NavMacrosButton"; Start-Sleep -Milliseconds 300
function Play($name) {
  (Find "MacroPickButton").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 500
  $AE::RootElement.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, $name))).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
  Start-Sleep -Milliseconds 300
  [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 1000, 800
  $sw = [Diagnostics.Stopwatch]::StartNew()
  Tog "PlayMacroButton"; Start-Sleep -Milliseconds 100
  while ((Text 'MacroStatusText') -ne 'Completado' -and $sw.ElapsedMilliseconds -lt 6000) { Start-Sleep -Milliseconds 30 }
  "'$(Text 'MacroStatusText')' en $($sw.ElapsedMilliseconds) ms"
}
"5) Espera morado (ya está): " + (Play "Espera morado") + " (esperado Completado, ~0,2-0,5 s)"
"6) Espera verde (no llega, máx. 1 s): " + (Play "Espera verde") + " (esperado Completado, ~1,2-1,6 s)"

$proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(4000) | Out-Null
$json = Get-Content "$data\settings.json" -Raw | ConvertFrom-Json
"7) Guardado: reglas=$(@($json.Profiles.P.WatchRules).Count) atajo=$($json.Prefs.WatchVk) (esperado 3, 121 = F10)"
$bench.WaitForExit(); Get-Content $out
"   (esperado diana: (60,60) x2 de la regla de color y (160,20) x2 del cuadrado, una por cada vez que se empieza a vigilar; (120,120) x2 de las macros)"
"8) error.log creado: " + (Test-Path (Join-Path $data "error.log")) + " (esperado False)"
[System.Windows.Forms.Cursor]::Position = $original