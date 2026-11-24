using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Autoclicker.Markers;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Views;

/// <summary>
/// Marcador numerado sobre la pantalla. No roba el foco, no aparece en Alt+Tab,
/// se arrastra en píxeles físicos y puede volverse traspasable mientras se clica.
/// </summary>
public partial class MarkerWindow : Window
{
    private nint _hwnd;
    private bool _dragging;
    private bool _moved;
    private POINT _dragStartCursor;
    private RECT _dragStartRect;

    public MarkerPoint Point { get; }

    /// <summary>Clic sin arrastrar: abrir el editor.</summary>
    public event Action<MarkerWindow>? Clicked;

    /// <summary>Se soltó tras arrastrar: Point ya tiene las nuevas coordenadas.</summary>
    public event Action<MarkerWindow>? Moved;

    public MarkerWindow(MarkerPoint point, int number)
    {
        InitializeComponent();
        Point = point;
        SetNumber(number);

        Root.MouseLeftButtonDown += OnDown;
        Root.MouseMove += OnMove;
        Root.MouseLeftButtonUp += OnUp;
        Root.MouseRightButtonUp += (_, _) => Clicked?.Invoke(this);
    }

    public void SetNumber(int number) => NumberText.Text = number.ToString();

    public nint Handle => _hwnd;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        SetExStyle(_hwnd, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, 0);
    }

    /// <summary>Muestra el marcador centrado en su punto.</summary>
    public void ShowAtPoint()
    {
        if (!IsVisible) Show();
        GetWindowRect(_hwnd, out RECT r);
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        SetWindowPos(_hwnd, 0, Point.X - w / 2, Point.Y - h / 2, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Vuelve a ponerlo encima de todo. Otra ventana "siempre visible" (un juego, otra app)
    /// puede haberse colocado por encima al activarse.
    /// </summary>
    public void BringToFront()
    {
        if (_hwnd != 0 && IsVisible)
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Mientras clica: traspasable (el clic llega a la app de debajo) y más tenue.</summary>
    public void SetClickThrough(bool on)
    {
        if (_hwnd == 0) return;
        if (on) SetExStyle(_hwnd, WS_EX_TRANSPARENT, 0);
        else SetExStyle(_hwnd, 0, WS_EX_TRANSPARENT);
        Opacity = on ? 0.6 : 1.0;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        GetCursorPos(out _dragStartCursor);
        GetWindowRect(_hwnd, out _dragStartRect);
        _dragging = true;
        _moved = false;
        Root.CaptureMouse();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        GetCursorPos(out POINT c);
        int dx = c.X - _dragStartCursor.X, dy = c.Y - _dragStartCursor.Y;
        if (!_moved && Math.Abs(dx) + Math.Abs(dy) < 3) return;
        _moved = true;
        SetWindowPos(_hwnd, 0, _dragStartRect.Left + dx, _dragStartRect.Top + dy, 0, 0,
            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Root.ReleaseMouseCapture();

        if (_moved)
        {
            GetWindowRect(_hwnd, out RECT r);
            Point.X = (r.Left + r.Right) / 2;
            Point.Y = (r.Top + r.Bottom) / 2;
            Moved?.Invoke(this);
        }
        else
        {
            Clicked?.Invoke(this);
        }
    }
}
