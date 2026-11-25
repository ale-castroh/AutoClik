using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Interop;

/// <summary>
/// Dibuja el logo con WPF: un ratón blanco con contorno grafito (se ve sobre barras de tareas claras
/// y oscuras) y una chispa ámbar arriba a la izquierda; sin fondo. Mientras clica, la chispa es verde.
/// Las formas son las mismas que en Theme/Controls.xaml y tools/make-icon.ps1 (rejilla de 24).
/// </summary>
internal static class IconFactory
{
    public const string BodyPath = "M12,5.0 C14.35,5.0 16.25,6.90 16.25,9.25 L16.25,14.75 C16.25,17.10 14.35,19.0 12,19.0 C9.65,19.0 7.75,17.10 7.75,14.75 L7.75,9.25 C7.75,6.90 9.65,5.0 12,5.0 Z";
    public const string LinesPath = "M7.75,10.5 L16.25,10.5 M12,5.0 L12,10.5";
    public const string SparkPath = "M5.98,5.77 L3.56,4.37 M7.09,4.34 L5.11,2.36 M8.52,3.23 L7.12,0.81";

    private static readonly Color Ink = Color.FromRgb(0x1C, 0x1E, 0x22);
    private static readonly Color Spark = Color.FromRgb(0xFF, 0xB0, 0x00);
    private static readonly Color SparkActive = Color.FromRgb(0x2E, 0xB8, 0x5C);

    public static BitmapSource Render(int size, bool active)
    {
        double k = size / 24.0;
        var scale = new ScaleTransform(k, k);
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            var ink = new Pen(new SolidColorBrush(Ink), Math.Max(1.0, 1.4 * k))
                { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var spark = new Pen(new SolidColorBrush(active ? SparkActive : Spark), Math.Max(1.0, 1.2 * k))
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

            dc.DrawGeometry(Brushes.White, ink, Scaled(BodyPath, scale));
            dc.DrawGeometry(null, ink, Scaled(LinesPath, scale));
            dc.DrawGeometry(null, spark, Scaled(SparkPath, scale));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Geometry Scaled(string path, Transform scale)
    {
        Geometry g = Geometry.Parse(path).Clone();
        g.Transform = scale;
        return g;
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
