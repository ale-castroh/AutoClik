param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Mejoras 1.3: grabar un arrastre y reproducirlo, devolver el cursor, iniciar con Windows
# (sin tocar el registro: con --data se usa un archivo) y arranque oculto con --tray.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data7"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
function At($x, $y) { [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point $x, $y; Start-Sleep -Milliseconds 60 }
function Pos { $p = [System.Windows.Forms.Cursor]::Position; "($($p.X),$($p.Y))" }

$out = Join-Path $env:TEMP "diana_p7.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "14" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--record-injected", "--theme=dark" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Find($id) { $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Sel($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function Status { (Find "StatusText").Current.Name }
function Play {
    (Find "StartButton").GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ((Status) -ne "Completado" -and $sw.ElapsedMilliseconds -lt 4000) { Start-Sleep -Milliseconds 20 }
    "estado='$(Status)' en $($sw.ElapsedMilliseconds) ms"
}

# 1) Grabar un arrastre dentro de la diana: (250,250) -> (400,380) en ~400 ms
Sel "PointsPositionRadio"; Start-Sleep -Milliseconds 200
(Find "RecordButton").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 700
At 250 250; [R]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
foreach ($i in 1..8) { At (250 + $i * 150 / 8) (250 + $i * 130 / 8); Start-Sleep -Milliseconds 40 }
[R]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 300
[R]::keybd_event(0x1B,0,0,[UIntPtr]::Zero); [R]::keybd_event(0x1B,0,2,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 800
"1) Grabado: estado='$(Status)' resumen='$((Find "PointsSummaryText").Current.Name)'  (esperado: 1 clic, 1 punto)"

# 2) Reproducir una vuelta sin devolver el cursor: debe acabar en el destino del arrastre
Sel "RepeatCountRadio"; (Find "RepeatCountBox").GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("1")
Start-Sleep -Milliseconds 200
At 1000 800
"2) Reproducción del arrastre: " + (Play) + "  cursor=" + (Pos) + " (esperado cerca de (400,380))"

# 3) Con "Devolver el ratón a su sitio": el cursor vuelve a (1000,800)
Tog "SettingsButton"; Start-Sleep -Milliseconds 300
Tog "RestoreCursorSwitch"
Tog "SettingsButton"; Start-Sleep -Milliseconds 300
At 1000 800
"3) Con devolver el cursor: " + (Play) + "  cursor=" + (Pos) + " (esperado (1000,800))"

# 4) Iniciar con Windows: con --data se escribe autostart.txt (nunca el registro)
Tog "SettingsButton"; Start-Sleep -Milliseconds 300
Tog "StartWithWindowsSwitch"; Start-Sleep -Milliseconds 200
$file = Join-Path $data "autostart.txt"
"4a) Activado: autostart.txt=" + (Test-Path $file) + "  contenido termina en --tray: " + ((Get-Content $file -Raw) -match '--tray$')
Tog "StartWithWindowsSwitch"; Start-Sleep -Milliseconds 200
"4b) Desactivado: autostart.txt=" + (Test-Path $file) + " (esperado False)"
Tog "SettingsButton"; Start-Sleep -Milliseconds 300

$proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(4000) | Out-Null
$bench.WaitForExit(); Get-Content $out
$json = Get-Content "$data\settings.json" -Raw | ConvertFrom-Json
$p = $json.Profiles.Predeterminado.Points[0]
"5) Guardado: tipo={0} origen=({1},{2}) destino=({3},{4}) duración={5} ms devolver cursor={6}" -f $p.Kind, $p.X, $p.Y, $p.EndX, $p.EndY, $p.HoldMs, $json.Profiles.Predeterminado.RestoreCursor
"6) error.log creado: " + (Test-Path (Join-Path $data "error.log")) + " (esperado False)"

# 7) --tray: arranca sin ventana; abrirlo otra vez (sin --tray) la muestra
$data2 = Join-Path $Dir "data7b"; Remove-Item -Recurse -Force $data2 -ErrorAction SilentlyContinue
$tray = Start-Process $AppPath -ArgumentList "--data=$data2", "--tray" -PassThru
Start-Sleep -Milliseconds 2500
$tray.Refresh()
"7a) --tray: sigue abierto=" + (-not $tray.HasExited) + "  ventana visible=" + ($tray.MainWindowHandle -ne 0) + " (esperado True / False)"
$again = Start-Process $AppPath -ArgumentList "--data=$data2" -PassThru
$again.WaitForExit(5000) | Out-Null
Start-Sleep -Milliseconds 800
$tray.Refresh()
"7b) Al abrirlo otra vez: la nueva se cerró=" + $again.HasExited + "  ventana visible=" + ($tray.MainWindowHandle -ne 0) + " (esperado True / True)"
$tray.CloseMainWindow() | Out-Null; $tray.WaitForExit(4000) | Out-Null
if (-not $tray.HasExited) { $tray.Kill() }

[System.Windows.Forms.Cursor]::Position = $original
