using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Interop;

/// <summary>
/// Dibuja el icono de la app con WPF (cuadrado redondeado del color de acento + cursor),
/// así sigue el acento de Windows y no hace falta ningún archivo de imagen.
/// </summary>
internal static class IconFactory
{
    private static readonly Typeface IconTypeface = new(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public static BitmapSource Render(int size, Color accent, bool active)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            double s = size;
            Color fill = active ? Color.FromRgb(0x2E, 0xB8, 0x5C) : accent;
            dc.DrawRoundedRectangle(new SolidColorBrush(fill), null, new Rect(0, 0, s, s), s * 0.24, s * 0.24);

            bool lightFill = (0.2126 * fill.R + 0.7152 * fill.G + 0.0722 * fill.B) / 255 > 0.6;
            var glyph = new FormattedText(active ? "" : "", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, IconTypeface, s * 0.62,
                new SolidColorBrush(lightFill ? Color.FromRgb(0x10, 0x10, 0x10) : Colors.White), 1.0);
            dc.DrawText(glyph, new Point((s - glyph.Width) / 2, (s - glyph.Height) / 2));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Convierte el bitmap en un HICON (hay que liberarlo con DestroyIcon).</summary>
    public static unsafe nint ToHIcon(BitmapSource bitmap)
    {
        int w = bitmap.PixelWidth, h = bitmap.PixelHeight;
        var pixels = new byte[w * h * 4];
        bitmap.CopyPixels(pixels, w * 4, 0);

        // Pbgra (premultiplicado) -> BGRA directo, que es lo que espera un icono de 32 bits.
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte a = pixels[i + 3];
            if (a is 0 or 255) continue;
            pixels[i] = (byte)Math.Min(255, pixels[i] * 255 / a);
            pixels[i + 1] = (byte)Math.Min(255, pixels[i + 1] * 255 / a);
            pixels[i + 2] = (byte)Math.Min(255, pixels[i + 2] * 255 / a);
        }

        int maskStride = ((w + 15) / 16) * 2; // filas alineadas a WORD
        var mask = new byte[maskStride * h];   // todo 0: la transparencia la da el alfa

        fixed (byte* xor = pixels)
        fixed (byte* and = mask)
        {
            return CreateIcon(0, w, h, 1, 32, and, xor);
        }
    }
}
