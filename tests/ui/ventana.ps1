param([string]$AppPath, [string]$BenchPath, [string]$Dir)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class F {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  public static void Front(IntPtr h) { keybd_event(0x12,0,0,UIntPtr.Zero); keybd_event(0x12,0,2,UIntPtr.Zero); SetForegroundWindow(h); }
  public static string FgTitle() { var sb = new StringBuilder(100); GetWindowText(GetForegroundWindow(), sb, 100); return sb.ToString(); }
}
"@
[F]::SetProcessDPIAware() | Out-Null
$original = [System.Windows.Forms.Cursor]::Position
$AE = [Windows.Automation.AutomationElement]; $TS = [Windows.Automation.TreeScope]
$data = Join-Path $Dir "data5c"; Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue

$out = Join-Path $env:TEMP "diana_p5c.txt"
$bench = Start-Process $BenchPath -ArgumentList "target", "8" -PassThru -RedirectStandardOutput $out -WindowStyle Hidden
$proc = Start-Process $AppPath -ArgumentList "--data=$data" -PassThru
while ($proc.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $proc.Refresh() }
Start-Sleep -Milliseconds 900
$root = $AE::FromHandle($proc.MainWindowHandle)
function Find($id) { $root.FindFirst($TS::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id))) }
function Tog($id) { (Find $id).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle() }
function State { "estado='" + (Find "StatusText").Current.Name + "' clics=" + (Find "CountText").Current.Name + " primer plano='" + [F]::FgTitle() + "'" }

Tog "SettingsButton"; Start-Sleep -Milliseconds 300; Tog "LockWindowSwitch"; Tog "SettingsButton"; Start-Sleep -Milliseconds 300
[System.Windows.Forms.Cursor]::Position = New-Object Drawing.Point 400, 400
[F]::Front($proc.MainWindowHandle); Start-Sleep -Milliseconds 300
Tog "StartButton"; Start-Sleep -Milliseconds 600
"a) " + (State)
$diana = $AE::RootElement.FindFirst($TS::Children, (New-Object Windows.Automation.PropertyCondition($AE::NameProperty, "Bench target")))
[F]::Front([IntPtr]$diana.Current.NativeWindowHandle); Start-Sleep -Milliseconds 1000
"b) " + (State)
[F]::Front($proc.MainWindowHandle); Start-Sleep -Milliseconds 200
"c) " + (State)
Start-Sleep -Milliseconds 800
"d) " + (State) + "  <- el contador no debe haber subido desde c)"
Tog "StartButton"
$bench.WaitForExit(); Get-Content $out | Select-Object -First 1
[System.Windows.Forms.Cursor]::Position = $original
$proc.CloseMainWindow() | Out-Null
