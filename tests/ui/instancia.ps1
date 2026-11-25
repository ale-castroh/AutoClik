param([string]$AppPath, [string]$BenchPath, [string]$Dir, [string]$ShotDir)
# Instancia única: abrir el .exe otra vez debe traer al frente la copia ya abierta (también desde la bandeja).
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class S {
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  public static string Fg() { var sb = new StringBuilder(100); GetWindowText(GetForegroundWindow(), sb, 100); return sb.ToString(); }
}
"@
$data = Join-Path $Dir "data_instancia"
$name = [IO.Path]::GetFileNameWithoutExtension($AppPath)
function Count { @(Get-Process $name -ErrorAction SilentlyContinue | Where-Object { $_.Id -eq $first.Id -or $_.StartTime -ge $script:t0 }).Count }

$script:t0 = Get-Date
$first = Start-Process $AppPath -ArgumentList "--data=$data" -PassThru
while ($first.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $first.Refresh() }
$hwnd = $first.MainWindowHandle
Start-Sleep -Milliseconds 800

# 1) Ocultar en la bandeja y abrir otra copia
$root = [Windows.Automation.AutomationElement]::FromHandle($hwnd)
$root.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([Windows.Automation.WindowVisualState]::Minimized)
Start-Sleep -Milliseconds 500
"1) En la bandeja: visible=" + [S]::IsWindowVisible($hwnd)
$second = Start-Process $AppPath -ArgumentList "--data=$data" -PassThru
$second.WaitForExit(5000) | Out-Null
Start-Sleep -Milliseconds 600
"   Tras abrir otra: la nueva se cerró=" + $second.HasExited + "  visible=" + [S]::IsWindowVisible($hwnd) + "  delante='" + [S]::Fg() + "'  copias=" + (Count)

# 2) Tapada por otra ventana (la diana de pruebas) y abrir otra copia
$np = Start-Process $BenchPath -ArgumentList "target", "6" -PassThru -WindowStyle Hidden
Start-Sleep -Milliseconds 1200
"2) Con otra ventana delante: delante='" + [S]::Fg() + "'"
$third = Start-Process $AppPath -ArgumentList "--data=$data" -PassThru
$third.WaitForExit(5000) | Out-Null
Start-Sleep -Milliseconds 600
"   Tras abrir otra: la nueva se cerró=" + $third.HasExited + "  delante='" + [S]::Fg() + "'  copias=" + (Count)

$np | Stop-Process -Force -ErrorAction SilentlyContinue
$first.Kill()
