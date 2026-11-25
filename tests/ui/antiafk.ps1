param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Anti-AFK (experimental) con la ventana diana como "juego": elegirla con la cruz, activar el
# anti-AFK cada 4 s (--antiafk-seconds) y comprobar que la diana recibe el clic, que vuelve la
# ventana que estaba activa y que el ratón queda donde estaba.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  public static string ForegroundTitle() { var s = new StringBuilder(256); GetWindowTextW(GetForegroundWindow(), s, 256); return s.ToString(); }
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data8"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
function At($x, $y) { [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point $x, $y; Start-Sleep -Milliseconds 60 }
function Pos { $p = [System.Windows.Forms.Cursor]::Position; "($($p.X),$($p.Y))" }

$out = Join-Path $env:TEMP "diana_p8.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "22" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--antiafk-seconds=4", "--theme=dark" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Find($id) { $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function Text($id) { (Find $id).Current.Name }

# Sin "Funciones experimentales" el Anti-AFK no aparece en la barra lateral (2.0)
"0a) Sin experimentales: Anti-AFK en la barra=" + ($null -ne (Find "NavAntiAfkButton")) + " (esperado False)"
Tog "SettingsButton"; Start-Sleep -Milliseconds 400
Tog "ExperimentalSwitch"; Start-Sleep -Milliseconds 300
"0b) Con experimentales: Anti-AFK en la barra=" + ($null -ne (Find "NavAntiAfkButton")) + " (esperado True)"
Tog "NavAntiAfkButton"; Start-Sleep -Milliseconds 400
"1) Sin elegir: ventana='$(Text "AfkTargetText")'  estado='$(Text "AntiAfkStatusText")'"

# 2) Elegir la diana con la cruz: clic en (240,240) = punto (40,40) dentro de ella
(Find "AfkPickButton").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 700
At 240 240; [R]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40; [R]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900
"2) Elegida: ventana='$(Text "AfkTargetText")'  (esperado 'Bench target · X 40 · Y 40')"

# 2b) Probar ahora: un clic inmediato aunque el anti-AFK esté apagado
At 1000 800
(Find "AfkTestButton").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 1200
"2b) Probar ahora: estado='$(Text "AntiAfkStatusText")'  detalle='$(Text "AfkDetailText")'  delante='$([R]::ForegroundTitle())'  cursor=$(Pos) (esperado 'Clic de prueba hecho', AutoClik, (1000,800))"

# 3) Activar y dejar el PC quieto 9 s: debe clicar la diana una o dos veces y volver
Tog "AntiAfkSwitch"; Start-Sleep -Milliseconds 300
At 1000 800
$before = Pos
"3a) Activado: estado='$(Text "AntiAfkStatusText")'  delante='$([R]::ForegroundTitle())'"
Start-Sleep -Seconds 9
"3b) Tras 9 s: estado='$(Text "AntiAfkStatusText")'  delante='$([R]::ForegroundTitle())' (esperado AutoClik)  cursor=$(Pos) (esperado $before)"

# 4) Apagar las funciones experimentales con el anti-AFK activo: se detiene y desaparece
"4a) Antes de apagar: anti-AFK activo=" + ((Find "AntiAfkSwitch").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState)
Tog "SettingsButton"; Start-Sleep -Milliseconds 400
Tog "ExperimentalSwitch"; Start-Sleep -Milliseconds 300
"4b) Sin experimentales: Anti-AFK en la barra=" + ($null -ne (Find "NavAntiAfkButton")) + " (esperado False)"
Tog "SettingsButton"; Start-Sleep -Milliseconds 300
"4c) Vuelta a Clics: botón Iniciar visible=" + ($null -ne (Find "StartButton"))

$proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(4000) | Out-Null
$bench.WaitForExit(); Get-Content $out
$json = Get-Content "$data\settings.json" -Raw | ConvertFrom-Json
"5) Guardado: proceso={0} título='{1}' punto=({2},{3}) minutos={4} activado={5} experimentales={6} (esperado activado=False, experimentales=False)" -f $json.Prefs.AntiAfkProcess, $json.Prefs.AntiAfkTitle, $json.Prefs.AntiAfkX, $json.Prefs.AntiAfkY, $json.Prefs.AntiAfkMinutes, $json.Prefs.AntiAfkEnabled, $json.Prefs.Experimental
"6) error.log creado: " + (Test-Path (Join-Path $data "error.log")) + " (esperado False)"
[System.Windows.Forms.Cursor]::Position = $original
