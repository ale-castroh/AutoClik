using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Autoclicker.Theme;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Interop;

/// <summary>
/// Fondo translúcido (acrílico: deja ver, borroso, lo que hay detrás) o Mica, esquinas redondeadas
/// y barra de título clara/oscura. En sistemas sin estos fondos (Windows 10 / Win11 21H2) usa uno sólido.
/// Windows lo muestra opaco por su cuenta si la ventana no está activa, si están desactivados los
/// "Efectos de transparencia" o con el ahorro de batería.
/// </summary>
internal static class WindowBackdrop
{
    /// <summary>DWMWA_SYSTEMBACKDROP_TYPE existe desde Windows 11 22H2 (build 22621).</summary>
    public static bool MicaSupported { get; } = Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22621;

    /// <summary>true = acrílico (por defecto); false = Mica, como en las versiones 1.x.</summary>
    public static bool Translucent { get; private set; } = true;

    public static void Attach(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;

        // Quita los botones de título que dibuja DWM: usamos los nuestros.
        nint style = GetWindowLongPtr(hwnd, GWL_STYLE);
        SetWindowLongPtr(hwnd, GWL_STYLE, (nint)((long)style & ~WS_SYSMENU));

        SetDwmInt(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);

        if (MicaSupported)
        {
            HwndSource.FromHwnd(hwnd)!.CompositionTarget!.BackgroundColor = Colors.Transparent;
            var margins = new MARGINS(-1);
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
            SetDwmInt(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, Translucent ? DWMSBT_TRANSIENTWINDOW : DWMSBT_MAINWINDOW);
            window.Background = Brushes.Transparent;
        }
        else
        {
            window.SetResourceReference(Window.BackgroundProperty, "WindowFallbackBrush");
        }

        ApplyTheme(window);
    }

    /// <summary>Cambia entre acrílico y Mica (también antes de crear la ventana).</summary>
    public static void SetTranslucent(Window window, bool on)
    {
        Translucent = on;
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != 0 && MicaSupported)
            SetDwmInt(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, on ? DWMSBT_TRANSIENTWINDOW : DWMSBT_MAINWINDOW);
    }

    /// <summary>Sincroniza la barra de título y el tinte de Mica con el tema actual.</summary>
    public static void ApplyTheme(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0) return;
        SetDwmInt(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ThemeManager.Current == AppTheme.Dark ? 1 : 0);
    }
}
