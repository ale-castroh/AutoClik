# Genera dist\Autoclicker.exe: un solo archivo autónomo (no necesita instalar .NET).
# Uso:  powershell -File build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'

# Asegura que dotnet está en el PATH aunque se acabe de instalar
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')

Write-Host "Compilando Autoclicker (Release, autónomo)..." -ForegroundColor Cyan
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }

dotnet publish (Join-Path $root 'src\Autoclicker') -c Release -o $dist -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "La compilación ha fallado." }

$exe = Get-Item (Join-Path $dist 'Autoclicker.exe')
Write-Host ("Listo: {0}  ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB)) -ForegroundColor Green
Write-Host "La primera vez que se abre tarda unos segundos más (se descomprime una sola vez)."
