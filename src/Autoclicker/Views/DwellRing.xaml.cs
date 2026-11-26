using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Views;

/// <summary>
/// Círculo del clic por permanencia (2.7). No roba el foco, no sale en Alt+Tab y los clics lo atraviesan
/// (WS_EX_TRANSPARENT): el clic llega a lo que haya debajo del cursor.
/// </summary>
public partial class DwellRing : Window
{
    private nint _hwnd;

    public DwellRing() => InitializeComponent();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        SetExStyle(_hwnd, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT, 0);
    }

    /// <summary>Muestra el círculo con <paramref name="progress"/> (0-1), centrado en el punto físico (x, y).</summary>
    public void ShowAt(int x, int y, double progress)
    {
        if (!IsVisible) Show();
        // Trazo discontinuo: un solo tramo tan largo como lo que lleva; unidades = grosor del trazo
        double circumference = Math.PI * (Arc.Width - Arc.StrokeThickness);
        double filled = Math.Max(0.001, Math.Min(1, progress)) * circumference / Arc.StrokeThickness;
        Arc.StrokeDashArray = new DoubleCollection { filled, 1000 };
        GetWindowRect(_hwnd, out RECT r);
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        SetWindowPos(_hwnd, HWND_TOPMOST, x - w / 2, y - h / 2, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
