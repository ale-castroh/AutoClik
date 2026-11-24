# Genera src/Autoclicker/Assets/app.ico (16-256 px) dibujándolo con WPF.
# Uso: powershell -NoProfile -File tools\make-icon.ps1
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$out = Join-Path $PSScriptRoot "..\src\Autoclicker\Assets\app.ico"
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function Render-Png([int]$size) {
    $s = [double]$size
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()

    $grad = New-Object System.Windows.Media.LinearGradientBrush
    $grad.StartPoint = New-Object System.Windows.Point 0, 0
    $grad.EndPoint = New-Object System.Windows.Point 1, 1
    $grad.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.Color]::FromRgb(0x1E, 0x90, 0xFF)), 0))
    $grad.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.Color]::FromRgb(0x6C, 0x4D, 0xF0)), 1))

    $margin = [Math]::Max(0.5, $s * 0.04)
    $rect = New-Object System.Windows.Rect $margin, $margin, ($s - 2 * $margin), ($s - 2 * $margin)
    $dc.DrawRoundedRectangle($grad, $null, $rect, $s * 0.24, $s * 0.24)

    # Brillo suave arriba
    $shine = New-Object System.Windows.Media.LinearGradientBrush
    $shine.StartPoint = New-Object System.Windows.Point 0.5, 0
    $shine.EndPoint = New-Object System.Windows.Point 0.5, 0.6
    $shine.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.Color]::FromArgb(0x40, 255, 255, 255)), 0))
    $shine.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.Color]::FromArgb(0, 255, 255, 255)), 1))
    $dc.DrawRoundedRectangle($shine, $null, $rect, $s * 0.24, $s * 0.24)

    $typeface = New-Object System.Windows.Media.Typeface "Segoe Fluent Icons"
    $text = New-Object System.Windows.Media.FormattedText ([string][char]0xE8B0), ([Globalization.CultureInfo]::InvariantCulture), `
        ([System.Windows.FlowDirection]::LeftToRight), $typeface, ($s * 0.6), ([System.Windows.Media.Brushes]::White), 1.0
    $dc.DrawText($text, (New-Object System.Windows.Point (($s - $text.Width) / 2 + $s * 0.03), (($s - $text.Height) / 2)))
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
