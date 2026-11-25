using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Autoclicker.Theme;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Interop;

/// <summary>
/// Fondo Mica, esquinas redondeadas y barra de título clara/oscura.
/// En sistemas sin Mica (Windows 10 / Win11 21H2) usa un fondo sólido.
/// </summary>
internal static class WindowBackdrop
{
    /// <summary>DWMWA_SYSTEMBACKDROP_TYPE existe desde Windows 11 22H2 (build 22621).</summary>
    public static bool MicaSupported { get; } = Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22621;

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
            SetDwmInt(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_MAINWINDOW);
            window.Background = Brushes.Transparent;
        }
        else
        {
            window.SetResourceReference(Window.BackgroundProperty, "WindowFallbackBrush");
        }

        ApplyTheme(window);
    }

    /// <summary>Sincroniza la barra de título y el tinte de Mica con el tema actual.</summary>
    public static void ApplyTheme(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0) return;
        SetDwmInt(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ThemeManager.Current == AppTheme.Dark ? 1 : 0);
    }
}
