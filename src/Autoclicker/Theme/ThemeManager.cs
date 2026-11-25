using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Autoclicker.Theme;

public enum AppTheme { Light, Dark }

/// <summary>
/// Lee el tema (claro/oscuro) y el color de acento de Windows y los aplica a los
/// recursos de la app. Se vuelve a llamar cuando Windows avisa de un cambio.
/// </summary>
public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AccentKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string DwmKey = @"Software\Microsoft\Windows\DWM";

    private static readonly Color DefaultAccent = Color.FromRgb(0x00, 0x78, 0xD4);
    private static bool _initialized;

    public static AppTheme Current { get; private set; }
    /// <summary>Acento para rellenos (botón principal, interruptores, iconos de la app).</summary>
    public static Color Accent { get; private set; }

    /// <summary>Acento para textos e iconos sobre el fondo (más claro en oscuro, más oscuro en claro).</summary>
    public static Color AccentText { get; private set; }

    /// <summary>Fuerza un tema (argumento --theme=light|dark). Null = seguir a Windows.</summary>
    public static AppTheme? Override { get; set; }

    public static event Action? ThemeChanged;

    public static void Apply()
    {
        AppTheme theme = Override ?? ReadSystemTheme();
        (Color accent, Color accentText) = ReadAccent(theme);

        if (_initialized && theme == Current && accent == Accent && accentText == AccentText) return;

        var resources = Application.Current.Resources;
        if (!_initialized || theme != Current)
        {
            // El índice 0 de MergedDictionaries es siempre la paleta del tema.
            resources.MergedDictionaries[0] = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Theme/{theme}.xaml", UriKind.Absolute)
            };
        }

        Current = theme;
        Accent = accent;
        AccentText = accentText;
        _initialized = true;
        ApplyAccent(resources, accent, accentText);

        ThemeChanged?.Invoke();
    }

    private static void ApplyAccent(ResourceDictionary resources, Color accent, Color accentText)
    {
        resources["AccentTextBrush"] = Frozen(accentText);
        Color onAccent = Luminance(accent) > 0.5 ? Color.FromRgb(0x10, 0x10, 0x10) : Colors.White;

        resources["AccentColor"] = accent;
        resources["AccentBrush"] = Frozen(accent);
        resources["AccentHoverBrush"] = Frozen(Color.FromArgb(0xE6, accent.R, accent.G, accent.B));
        resources["AccentPressedBrush"] = Frozen(Color.FromArgb(0xCC, accent.R, accent.G, accent.B));
        resources["AccentSoftBrush"] = Frozen(Color.FromArgb(0x26, accent.R, accent.G, accent.B));
        resources["OnAccentBrush"] = Frozen(onAccent);
    }

    private static AppTheme ReadSystemTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int v && v == 0 ? AppTheme.Dark : AppTheme.Light;
    }

    /// <summary>
    /// AccentPalette contiene 8 tonos RGBA: 0-2 claros, 3 base, 4-6 oscuros.
    /// Rellenos: el tono base en oscuro (con texto blanco, no el tono pálido de Fluent) y
    /// "Dark 1" en claro. Textos e iconos: "Light 1" en oscuro y "Dark 2" en claro.
    /// </summary>
    private static (Color Fill, Color Text) ReadAccent(AppTheme theme)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AccentKey))
            {
                if (key?.GetValue("AccentPalette") is byte[] p && p.Length >= 32)
                {
                    Color Tone(int i) => Color.FromRgb(p[i * 4], p[i * 4 + 1], p[i * 4 + 2]);
                    return theme == AppTheme.Dark ? (Tone(3), Tone(2)) : (Tone(4), Tone(5));
                }
            }

            using (var key = Registry.CurrentUser.OpenSubKey(DwmKey))
            {
                if (key?.GetValue("AccentColor") is int abgr)
                {
                    var c = Color.FromRgb((byte)abgr, (byte)(abgr >> 8), (byte)(abgr >> 16));
                    return (c, c);
                }
            }
        }
        catch
        {
            // Registro inaccesible: acento por defecto.
        }
        return (DefaultAccent, DefaultAccent);
    }

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
