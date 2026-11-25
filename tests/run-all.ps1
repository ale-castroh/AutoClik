# Batería completa de pruebas de AutoClik.
#   powershell -NoProfile -File tests\run-all.ps1                 (prueba dist\AutoClik.exe)
#   powershell -NoProfile -File tests\run-all.ps1 -App <ruta.exe>
#
# AVISO: durante ~1 minuto mueve el ratón y hace clics reales sobre una ventana de prueba
# (cuadrado morado en la esquina superior izquierda). No uses el PC mientras se ejecuta.
# Nunca toca tu configuración: usa %TEMP%\autoclicker-tests.
param([string]$App = (Join-Path $PSScriptRoot '..\dist\AutoClik.exe'))

$ErrorActionPreference = 'Stop'
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
$App = (Resolve-Path $App).Path

# Las pruebas pulsan F6: con otro AutoClik abierto, ese empezaría a clicar donde esté el ratón.
if (Get-Process AutoClik, Autoclicker -ErrorAction SilentlyContinue) {
    Write-Host "Cierra todos los AutoClik abiertos (también el de la bandeja) antes de ejecutar las pruebas." -ForegroundColor Yellow
    exit 1
}
$startedAt = Get-Date
# Solo se cierran los procesos que hayan arrancado las propias pruebas.
function Stop-TestProcesses {
    Get-Process AutoClik, Autoclicker.Bench -ErrorAction SilentlyContinue |
        Where-Object { $_.StartTime -ge $startedAt } | Stop-Process -Force
}
# Las pruebas buscan botones por su texto en español: forzar el idioma de la app.
$env:AUTOCLICKER_LANG = 'es'
$work = Join-Path $env:TEMP 'autoclicker-tests'
New-Item -ItemType Directory -Force $work | Out-Null

Write-Host "== Compilando el banco de pruebas" -ForegroundColor Cyan
dotnet build (Join-Path $PSScriptRoot 'Autoclicker.Bench') -c Release -nologo -v q | Out-Null
$bench = (Resolve-Path (Join-Path $PSScriptRoot 'Autoclicker.Bench\bin\Release\net11.0-windows\Autoclicker.Bench.exe')).Path

Write-Host "`n== Motor: precisión del intervalo" -ForegroundColor Cyan
& $bench timing
Write-Host "`n== Motor: clics reales" -ForegroundColor Cyan
& $bench clicks

$ui = @(
    @{ Name = 'Básico (repetir, mantener, F6, bandeja)'; File = 'basico.ps1' },
    @{ Name = 'Atajo configurable'; File = 'atajo.ps1' },
    @{ Name = 'Multi-punto'; File = 'puntos.ps1' },
    @{ Name = 'Guardado y perfiles'; File = 'perfiles.ps1' },
    @{ Name = 'Pausar si cambia la ventana'; File = 'ventana.ps1' },
    @{ Name = 'Grabadora'; File = 'grabadora.ps1' },
    @{ Name = 'Una sola copia abierta'; File = 'instancia.ps1' },
    @{ Name = 'Arrastres, cursor, inicio con Windows'; File = 'extras.ps1' },
    @{ Name = 'Anti-AFK (experimental)'; File = 'antiafk.ps1' },
    @{ Name = 'Clics de ratón reales en cada sección'; File = 'clics-reales.ps1' }
)
foreach ($t in $ui) {
    Write-Host "`n== $($t.Name)" -ForegroundColor Cyan
    Remove-Item -Recurse -Force (Join-Path $work 'data*') -ErrorAction SilentlyContinue
    Stop-TestProcesses
    # Cada prueba en su propio proceso (definen tipos con el mismo nombre)
    powershell -NoProfile -File (Join-Path $PSScriptRoot "ui\$($t.File)") -AppPath $App -BenchPath $bench -Dir $work -ShotDir $work
}
Stop-TestProcesses
Write-Host "`nTerminado. Revisa que cada línea coincide con lo esperado." -ForegroundColor Green
