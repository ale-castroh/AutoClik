# Genera dist\AutoClik.exe: un solo archivo de menos de 1 MB.
# Usa .NET Framework 4.8, que ya viene con Windows 10 (1903+) y Windows 11: no hay que instalar nada.
# Uso:  powershell -File build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'

# Asegura que dotnet está en el PATH aunque se acabe de instalar
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')

Write-Host "Compilando AutoClik (Release)..." -ForegroundColor Cyan
dotnet build (Join-Path $root 'src\Autoclicker') -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "La compilación ha fallado." }

if (Test-Path $dist) {
    try { Remove-Item -Recurse -Force $dist -ErrorAction Stop }
    catch { throw "No se puede reemplazar dist\: AutoClik está abierto. Ciérralo (también desde la bandeja) y vuelve a intentarlo." }
}
New-Item -ItemType Directory $dist | Out-Null
Copy-Item (Join-Path $root 'src\Autoclicker\bin\Release\net48\AutoClik.exe') $dist

$exe = Get-Item (Join-Path $dist 'AutoClik.exe')
$hash = (Get-FileHash $exe.FullName -Algorithm SHA256).Hash
Write-Host ("Listo: {0}  ({1:N0} KB)" -f $exe.FullName, ($exe.Length / 1KB)) -ForegroundColor Green
Write-Host "SHA-256: $hash"
