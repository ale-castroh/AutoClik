# Genera src/Autoclicker/Assets/app.ico (16-256 px) dibujándolo con WPF.
# Uso: powershell -NoProfile -File tools\make-icon.ps1
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$out = Join-Path $PSScriptRoot "..\src\Autoclicker\Assets\app.ico"
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function Render-Png([int]$size) {
    $s = [double]$size
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()

    # Logo sin fondo: ratón blanco con contorno grafito + chispa ámbar (mismas formas que IconFactory.cs)
    $k = $s / 24.0
    $scale = New-Object System.Windows.Media.ScaleTransform $k, $k
    function Shape([string]$d) { $g = [System.Windows.Media.Geometry]::Parse($d).Clone(); $g.Transform = $scale; return $g }
    $inkBrush = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0x1C, 0x1E, 0x22))
    $ink = New-Object System.Windows.Media.Pen $inkBrush, ([Math]::Max(1.0, 1.4 * $k))
    $ink.LineJoin = 'Round'; $ink.StartLineCap = 'Round'; $ink.EndLineCap = 'Round'
    $sparkBrush = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0xFF, 0xB0, 0x00))
    $spark = New-Object System.Windows.Media.Pen $sparkBrush, ([Math]::Max(1.0, 1.2 * $k))
    $spark.StartLineCap = 'Round'; $spark.EndLineCap = 'Round'
    $dc.DrawGeometry([System.Windows.Media.Brushes]::White, $ink, (Shape "M12,5.0 C14.35,5.0 16.25,6.90 16.25,9.25 L16.25,14.75 C16.25,17.10 14.35,19.0 12,19.0 C9.65,19.0 7.75,17.10 7.75,14.75 L7.75,9.25 C7.75,6.90 9.65,5.0 12,5.0 Z"))
    $dc.DrawGeometry($null, $ink, (Shape "M7.75,10.5 L16.25,10.5 M12,5.0 L12,10.5"))
    $dc.DrawGeometry($null, $spark, (Shape "M5.98,5.77 L3.56,4.37 M7.09,4.34 L5.11,2.36 M8.52,3.23 L7.12,0.81"))
    $dc.Close()

    $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($visual)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = New-Object IO.MemoryStream
    $enc.Save($ms)
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 256
$images = foreach ($sz in $sizes) { , (Render-Png $sz) }

# Formato ICO: cabecera + directorio + imágenes PNG
$fs = [IO.File]::Create($out)
$w = New-Object IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
"Icono generado: $out ($((Get-Item $out).Length) bytes)"

# Vista previa 256 px para revisar
[IO.File]::WriteAllBytes((Join-Path (Split-Path $out) "app-preview.png"), $images[-1])
