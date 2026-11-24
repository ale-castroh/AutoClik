param([string]$AppPath, [string]$BenchPath, [string]$Dir)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Q {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
}
"@
[Q]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]
$TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data5"
Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue

function Launch {
  $p = Start-Process $AppPath -ArgumentList "--data=$data" -PassThru
  while ($p.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $p.Refresh() }
  Start-Sleep -Milliseconds 900
  $script:proc = $p
  $script:root = $AE::FromHandle($p.MainWindowHandle)
}
function Find($id) { $script:root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Top($name) { $AE::RootElement.FindFirst($TS::Children, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, $name))) }
function SetVal($id, $v) { (Find $id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($v) }
function GetVal($id) { (Find $id).GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value }
function Sel($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function IsSel($id) { (Find $id).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function TogState($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Current.ToggleState }
function Status { (Find "StatusText").Current.Name }
function CloseApp { $script:proc.CloseMainWindow() | Out-Null; $script:proc.WaitForExit(5000) | Out-Null }
function MenuItemByName($name) {
  # Los menús contextuales de WPF son ventanas emergentes de primer nivel
  $AE::RootElement.FindFirst($TS::Descendants, (New-Object Windows.Automation.AndCondition(
    (New-Object Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [Windows.Automation.ControlType]::MenuItem)),
    (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, $name)))))
}
function OpenProfileMenu { (Find "ProfileButton").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 500 }

# ---------- 1) Guardado y restauración ----------
Launch
SetVal "IntervalBox" "250"; Sel "UnitSRadio"; Sel "RepeatCountRadio"; SetVal "RepeatCountBox" "7"; Sel "DoubleKindRadio"
Tog "SettingsButton"; Start-Sleep -Milliseconds 300; Tog "EmergencySwitch"; Tog "LockWindowSwitch"; Tog "SettingsButton"
Start-Sleep -Milliseconds 800
CloseApp
"1a) settings.json creado: " + (Test-Path "$data\settings.json")
Launch
"1b) Restaurado: intervalo=" + (GetVal "IntervalBox") + " s=" + (IsSel "UnitSRadio") + " veces=" + (IsSel "RepeatCountRadio") + "/" + (GetVal "RepeatCountBox") + " doble=" + (IsSel "DoubleKindRadio")
Tog "SettingsButton"; Start-Sleep -Milliseconds 300
"    esquina=" + (TogState "EmergencySwitch") + " (esperado Off)  ventana=" + (TogState "LockWindowSwitch") + " (esperado On)"
Tog "LockWindowSwitch"; Tog "SettingsButton"; Start-Sleep -Milliseconds 300

# ---------- 2) Perfiles ----------
"2a) Perfil actual: " + (Find "ProfileNameText").Current.Name
OpenProfileMenu
$item = MenuItemByName "Nuevo perfil…"
if ($null -eq $item) { "   menú no encontrado"; } else {
  $item.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
  Start-Sleep -Milliseconds 700
  $dlg = $script:root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "NameBox"))); $dlg = $AE::RootElement.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, "Perfil"))); "   diálogo encontrado: " + ($null -ne $dlg)
  $nameBox = $dlg.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "NameBox")))
  $nameBox.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("Juego")
  $ok = $dlg.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "OkButton")))
  $ok.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
  Start-Sleep -Milliseconds 600
  "2b) Tras crear: perfil=" + (Find "ProfileNameText").Current.Name + " intervalo=" + (GetVal "IntervalBox") + " (copia: 250)"
  SetVal "IntervalBox" "40"; Sel "UnitMsRadio"; Start-Sleep -Milliseconds 800
  OpenProfileMenu
  (MenuItemByName "Predeterminado").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 600
  "2c) En Predeterminado: intervalo=" + (GetVal "IntervalBox") + " s=" + (IsSel "UnitSRadio") + "  (esperado 250, True)"
  OpenProfileMenu
  (MenuItemByName "Juego").GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 600
  "2d) En Juego: intervalo=" + (GetVal "IntervalBox") + " ms=" + (IsSel "UnitMsRadio") + "  (esperado 40, True)"
}
CloseApp
$json = Get-Content "$data\settings.json" -Raw | ConvertFrom-Json
"2e) JSON: actual=" + $json.CurrentProfile + " perfiles=" + (($json.Profiles.PSObject.Properties.Name) -join ", ")

# ---------- 3) Esquina con movimiento inyectado: NO debe parar ----------
# (La pausa al cambiar de ventana se prueba en ventana.ps1)
Launch
Sel "CursorPositionRadio"; Sel "RepeatForeverRadio"
Tog "SettingsButton"; Start-Sleep -Milliseconds 300; Tog "EmergencySwitch"; Tog "SettingsButton"; Start-Sleep -Milliseconds 300
$r = New-Object Q+RECT; [Q]::DwmGetWindowAttribute($proc.MainWindowHandle, 9, [ref]$r, 16) | Out-Null
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point ([int](($r.L + $r.R) / 2)), ($r.T + 120)
Tog "StartButton"; Start-Sleep -Milliseconds 300
[Q]::mouse_event(0x8001, 0, 0, 0, [UIntPtr]::Zero)   # MOVE|ABSOLUTE a (0,0): inyectado
Start-Sleep -Milliseconds 500
"3) Movimiento inyectado a la esquina: " + (Status) + " (esperado Clicando: solo cuenta el ratón físico)"
Tog "StartButton"
[System.Windows.Forms.Cursor]::Position = $original
CloseApp
