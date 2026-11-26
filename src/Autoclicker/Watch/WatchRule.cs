using System.Windows.Media.Imaging;
using Autoclicker.Persistence;

namespace Autoclicker.Watch;

public enum WatchKind { Color, Image }

public enum WatchAction { ClickOn, ClickAt, Key, StartClicks, PlayMacro, StopAll }

/// <summary>
/// Una regla de "Vigilar" (2.4): cuando aparezca un color en un punto o una imagen en una zona,
/// hacer algo. Se guarda en el perfil; la imagen, como PNG en la carpeta "images".
/// </summary>
public sealed class WatchRule
{
    public WatchKind Kind { get; set; } = WatchKind.Color;

    // Color en un punto (píxeles físicos de pantalla); Color = 0xRRGGBB
    public int X { get; set; }
    public int Y { get; set; }
    public int Color { get; set; }

    // Imagen: archivo en la carpeta de imágenes; zona donde buscarla (ancho 0 = toda la pantalla)
    public string? Image { get; set; }
    public int ZoneX { get; set; }
    public int ZoneY { get; set; }
    public int ZoneW { get; set; }
    public int ZoneH { get; set; }

    /// <summary>0-100: cuánto puede variar cada canal del color (10 ≈ 25 de 255).</summary>
    public int Tolerance { get; set; } = 10;

    public WatchAction Action { get; set; } = WatchAction.ClickOn;
    public int ActionX { get; set; }
    public int ActionY { get; set; }
    public int KeyVk { get; set; } = 0x20;
    public string? MacroName { get; set; }

    /// <summary>true = cada vez que vuelva a aparecer; false = solo la primera vez (hasta volver a vigilar).</summary>
    public bool EveryTime { get; set; } = true;
    public bool Enabled { get; set; } = true;

    public bool HasZone => ZoneW > 0 && ZoneH > 0;

    public WatchRule Clone() => (WatchRule)MemberwiseClone();

    internal Dictionary<string, object?> ToJson() => new()
    {
        ["Kind"] = Kind,
        ["X"] = X,
        ["Y"] = Y,
        ["Color"] = Color,
        ["Image"] = Image,
        ["ZoneX"] = ZoneX,
        ["ZoneY"] = ZoneY,
        ["ZoneW"] = ZoneW,
        ["ZoneH"] = ZoneH,
        ["Tolerance"] = Tolerance,
        ["Action"] = Action,
        ["ActionX"] = ActionX,
        ["ActionY"] = ActionY,
        ["KeyVk"] = KeyVk,
        ["MacroName"] = MacroName,
        ["EveryTime"] = EveryTime,
        ["Enabled"] = Enabled,
    };

    internal static WatchRule FromJson(JsonObj j)
    {
        var d = new WatchRule();
        return new WatchRule
        {
            Kind = j.Enum("Kind", d.Kind),
            X = j.Int("X", 0),
            Y = j.Int("Y", 0),
            Color = j.Int("Color", 0),
            Image = j.Str("Image"),
            ZoneX = j.Int("ZoneX", 0),
            ZoneY = j.Int("ZoneY", 0),
            ZoneW = j.Int("ZoneW", 0),
            ZoneH = j.Int("ZoneH", 0),
            Tolerance = Math.Max(0, Math.Min(100, j.Int("Tolerance", d.Tolerance))),
            Action = j.Enum("Action", d.Action),
            ActionX = j.Int("ActionX", 0),
            ActionY = j.Int("ActionY", 0),
            KeyVk = j.Int("KeyVk", d.KeyVk),
            MacroName = j.Str("MacroName"),
            EveryTime = j.Bool("EveryTime", true),
            Enabled = j.Bool("Enabled", true),
        };
    }
}

/// <summary>Las imágenes recortadas: PNG en la carpeta "images", junto a settings.json.</summary>
public static class WatchImages
{
    public static string Folder { get; } = Path.Combine(Path.GetDirectoryName(new SettingsStore().FilePath)!, "images");

    public static string PathOf(string name) => Path.Combine(Folder, name);

    /// <summary>Guarda una captura como PNG con un nombre nuevo y devuelve el nombre.</summary>
    public static string Save(PixelImage image)
    {
        Directory.CreateDirectory(Folder);
        string name = $"img-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 6)}.png";
        var bgra = image.Pixels.Select(p => p | unchecked((int)0xFF000000)).ToArray();
        var source = BitmapSource.Create(image.Width, image.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, bgra, image.Width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var file = File.Create(PathOf(name));
        encoder.Save(file);
        return name;
    }

    /// <summary>Carga un PNG como píxeles 0xRRGGBB; null si no existe o no se puede leer.</summary>
    public static PixelImage? Load(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string path = PathOf(name!);
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int w = converted.PixelWidth, h = converted.PixelHeight;
            var pixels = new int[w * h];
            converted.CopyPixels(pixels, w * 4, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] &= 0xFFFFFF;
            return new PixelImage(w, h, pixels);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or System.IO.FileFormatException)
        {
            ErrorLog.Write("imagen de regla", ex);
            return null;
        }
    }

    /// <summary>La imagen para mostrarla en la interfaz (sin bloquear el archivo).</summary>
    public static BitmapImage? Preview(string? name)
    {
        if (string.IsNullOrEmpty(name) || !File.Exists(PathOf(name!))) return null;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(PathOf(name!));
        image.EndInit();
        image.Freeze();
        return image;
    }
}
