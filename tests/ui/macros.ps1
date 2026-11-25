param([string]$AppPath, [string]$BenchPath, [string]$Dir)
# Macros (2.1): grabar sobre la diana (clic, teclas A y B, rueda, clic), terminar con F7,
# reproducir 2 vueltas a x2 y comprobar que la diana lo recibe todo, que no queda ninguna
# tecla pulsada y que la macro se guarda en la biblioteca.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class R {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
  public static void Key(byte vk) { byte sc = (byte)MapVirtualKey(vk, 0); keybd_event(vk, sc, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(40); keybd_event(vk, sc, 2, UIntPtr.Zero); }
}
"@
[R]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data10"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
function At($x, $y) { [System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point $x, $y; Start-Sleep -Milliseconds 80 }
function LeftClick { [R]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40; [R]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150 }

$out = Join-Path $env:TEMP "diana_p10.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "16" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
$proc = Start-Process $AppPath -ArgumentList "--data=$data", "--record-injected", "--theme=dark" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Find($id) { $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function Sel($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function Text($id) { (Find $id).Current.Name }

Tog "NavMacrosButton"; Start-Sleep -Milliseconds 400
"1) Sin macros: estado='$(Text 'MacroStatusText')' macro='$(Text 'MacroNameText')' (esperado 'Sin macros', 'Ninguna')"

# 2) Grabar: la ventana se oculta y aparece la barra flotante
Tog "RecordMacroButton"; Start-Sleep -Milliseconds 700
$bar = $AE::RootElement.FindFirst($TS::Children, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, "Grabando")))
"2) Grabando: barra visible=$($null -ne $bar)"
At 260 260; LeftClick
[R]::Key(0x41); Start-Sleep -Milliseconds 120; [R]::Key(0x42); Start-Sleep -Milliseconds 120
[R]::mouse_event(0x800, 0, 0, 120, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80
[R]::mouse_event(0x800, 0, 0, 120, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80
At 320 320; LeftClick
Start-Sleep -Milliseconds 200
[R]::Key(0x76)  # F7 termina (no debe quedar grabada)
Start-Sleep -Milliseconds 900
"3) Tras F7: estado='$(Text 'MacroStatusText')' macro='$(Text 'MacroNameText')' tiempo=$(Text 'MacroTimeText') detalle='$(Text 'MacroDetailText')' (esperado 'Macro guardada', 'Macro 1', 2 clics, 2 teclas)"

# 4) Reproducir 2 vueltas a x2
Sel "MacroCountRadio"; (Find "MacroCountBox").GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("2"); Sel "MacroSpeed2"
Start-Sleep -Milliseconds 200
At 1000 800
$sw = [Diagnostics.Stopwatch]::StartNew()
Tog "PlayMacroButton"
Start-Sleep -Milliseconds 300
$playing = Text 'MacroStatusText'
while ((Text 'MacroStatusText') -ne 'Completado' -and $sw.ElapsedMilliseconds -lt 8000) { Start-Sleep -Milliseconds 50 }
"4) Reproducción: durante='$playing' fin='$(Text 'MacroStatusText')' en $($sw.ElapsedMilliseconds) ms"
$stuck = @(0x41, 0x42, 0x01, 0x02) | Where-Object { ([R]::GetAsyncKeyState($_) -band 0x8000) -ne 0 }
"5) Teclas o botones pegados: " + $(if ($stuck) { $stuck -join ',' } else { 'ninguno' }) + " (esperado ninguno)"
$proc.Refresh()
"6) Ventana de nuevo visible: " + ($proc.MainWindowHandle -ne 0)

$proc.CloseMainWindow() | Out-Null; $proc.WaitForExit(4000) | Out-Null
$bench.WaitForExit(); Get-Content $out
$files = @(Get-ChildItem (Join-Path $data "macros") -Filter *.json -ErrorAction SilentlyContinue)
$json = Get-Content "$data\settings.json" -Raw | ConvertFrom-Json
"7) Biblioteca: $($files.Count) archivo(s) '$($files.Name -join ', ')'  elegida='$($json.Prefs.MacroName)' repetir=$($json.Prefs.MacroRepeat) veces=$($json.Prefs.MacroCount) velocidad=$($json.Prefs.MacroSpeed)"
"   (esperado: 1 archivo 'Macro 1.json', elegida 'Macro 1', Count, 2, 2. Diana: 6 clics, teclas A B A B A B, rueda 6)"
"8) error.log creado: " + (Test-Path (Join-Path $data "error.log")) + " (esperado False)"
[System.Windows.Forms.Cursor]::Position = $original
