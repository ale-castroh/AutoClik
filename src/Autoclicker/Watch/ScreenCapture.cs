using System.Runtime.InteropServices;

namespace Autoclicker.Watch;

/// <summary>Una captura de pantalla en memoria: píxeles 0xRRGGBB, fila a fila, en píxeles físicos.</summary>
public sealed class PixelImage
{
    public PixelImage(int width, int height, int[] pixels) { Width = width; Height = height; Pixels = pixels; }

    public int Width { get; }
    public int Height { get; }
    public int[] Pixels { get; }

    public int this[int x, int y] => Pixels[y * Width + x];
}

/// <summary>
/// Lee la pantalla (BitBlt del escritorio): el color de un punto, una zona entera, y busca una imagen
/// dentro de otra. Coordenadas físicas, como el resto de AutoClik (la app es PerMonitorV2).
/// Los juegos a pantalla completa "exclusiva" no se pueden leer así (se ven negros).
/// </summary>
public static unsafe class ScreenCapture
{
    /// <summary>Captura un rectángulo de la pantalla.</summary>
    public static PixelImage Capture(int x, int y, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var pixels = new int[width * height];
        nint screen = GetDC(0);
        nint mem = CreateCompatibleDC(screen);
        var info = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = width, biHeight = -height, // de arriba abajo
            biPlanes = 1, biBitCount = 32,
        };
        nint bitmap = CreateDIBSection(screen, ref info, 0, out nint bits, 0, 0);
        try
        {
            if (bitmap == 0) return new PixelImage(width, height, pixels);
            nint old = SelectObject(mem, bitmap);
            BitBlt(mem, 0, 0, width, height, screen, x, y, SRCCOPY);
            SelectObject(mem, old);
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            for (int i = 0; i < pixels.Length; i++) pixels[i] &= 0xFFFFFF;
        }
        finally
        {
            if (bitmap != 0) DeleteObject(bitmap);
            DeleteDC(mem);
            ReleaseDC(0, screen);
        }
        return new PixelImage(width, height, pixels);
    }

    /// <summary>Todo el escritorio virtual (todos los monitores), con su esquina.</summary>
    public static (PixelImage Image, int Left, int Top) CaptureDesktop()
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN), top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        return (Capture(left, top, GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN)), left, top);
    }

    /// <summary>Color (0xRRGGBB) del punto.</summary>
    public static int PixelAt(int x, int y) => Capture(x, y, 1, 1).Pixels[0];

    /// <summary>Tolerancia 0-100 → diferencia máxima por canal (0-255).</summary>
    public static int ChannelTolerance(int tolerance) => (int)Math.Round(Math.Max(0, Math.Min(100, tolerance)) * 2.55);

    /// <summary>¿Los dos colores se parecen? Diferencia de cada canal (rojo, verde, azul) como mucho <paramref name="maxDiff"/>.</summary>
    public static bool Near(int a, int b, int maxDiff) =>
        Math.Abs(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)) <= maxDiff
        && Math.Abs(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF)) <= maxDiff
        && Math.Abs((a & 0xFF) - (b & 0xFF)) <= maxDiff;

    /// <summary>
    /// Busca <paramref name="needle"/> dentro de <paramref name="hay"/>. Devuelve la esquina donde
    /// está, o null. Cada píxel puede diferir hasta la tolerancia, y hasta un 3 % de los píxeles puede no
    /// coincidir (bordes suavizados, un cursor encima...).
    /// <paramref name="cancel"/> se consulta en cada fila: la búsqueda se puede interrumpir al momento.
    /// </summary>
    public static (int X, int Y)? Find(PixelImage hay, PixelImage needle, int tolerance, Func<bool>? cancel = null)
    {
        int nw = needle.Width, nh = needle.Height, hw = hay.Width;
        if (nw > hay.Width || nh > hay.Height || nw * nh == 0) return null;
        int maxDiff = ChannelTolerance(tolerance);
        int allowed = nw * nh * 3 / 100;
        int[] h = hay.Pixels, n = needle.Pixels;

        // Filtro rápido con píxeles de MUESTRA. Se eligen los distintivos (los que no son del color de
        // fondo de la imagen): en un círculo negro sobre rojo, puntos del borde negro. Así, dentro de una
        // zona grande del color de fondo, cada posición se descarta con una o dos comparaciones.
        (int Offset, int Color)[] samples = Samples(needle, maxDiff, hw);

        for (int y = 0; y <= hay.Height - nh; y++)
        {
            if (cancel?.Invoke() == true) return null;
            int rowStart = y * hw;
            for (int x = 0; x <= hay.Width - nw; x++)
            {
                int origin = rowStart + x;
                int misses = 0;
                foreach (var (offset, color) in samples)
                    if (!Near(h[origin + offset], color, maxDiff) && ++misses > 1) break;
                if (misses > 1) continue;

                misses = 0;
                for (int ny = 0; ny < nh && misses <= allowed; ny++)
                {
                    int hRow = (y + ny) * hw + x, nRow = ny * nw;
                    for (int nx = 0; nx < nw; nx++)
                        if (!Near(h[hRow + nx], n[nRow + nx], maxDiff) && ++misses > allowed) break;
                }
                if (misses <= allowed) return (x, y);
            }
        }
        return null;
    }

    /// <summary>
    /// Hasta 24 píxeles de muestra repartidos por la imagen, primero los distintos de su color más
    /// común; si la imagen es de un solo color, una rejilla. Desplazamiento calculado para el ancho del pajar.
    /// </summary>
    private static (int Offset, int Color)[] Samples(PixelImage needle, int maxDiff, int hayWidth)
    {
        int nw = needle.Width, nh = needle.Height;
        int[] n = needle.Pixels;

        // Color de fondo = el más repetido
        var counts = new Dictionary<int, int>();
        foreach (int c in n) counts[c] = counts.TryGetValue(c, out int k) ? k + 1 : 1;
        int background = counts.OrderByDescending(kv => kv.Value).First().Key;

        var distinct = new List<int>();
        for (int i = 0; i < n.Length; i++)
            if (!Near(n[i], background, maxDiff)) distinct.Add(i);

        var picked = new List<int>();
        if (distinct.Count > 0)
        {
            int step = Math.Max(1, distinct.Count / 16);
            for (int i = 0; i < distinct.Count && picked.Count < 16; i += step) picked.Add(distinct[i]);
        }
        // Más una rejilla 3×3 (incluye fondo: confirma que alrededor también coincide)
        for (int gy = 0; gy < 3; gy++)
            for (int gx = 0; gx < 3; gx++)
                picked.Add((gy * (nh - 1) / 2) * nw + gx * (nw - 1) / 2);

        return picked.Distinct()
            .Select(i => (i / nw * hayWidth + i % nw, n[i]))
            .ToArray();
    }

    // ================= Win32 =================

    private const uint SRCCOPY = 0x00CC0020;
    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")]
    private static extern nint CreateDIBSection(nint dc, ref BITMAPINFOHEADER info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);
}
