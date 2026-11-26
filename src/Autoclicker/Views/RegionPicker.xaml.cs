using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Views;

/// <summary>
/// Capa a pantalla completa (todos los monitores) para marcar un rectángulo arrastrando.
/// Devuelve coordenadas físicas reales (las del cursor al pulsar y al soltar).
/// </summary>
public partial class RegionPicker : Window
{
    private POINT _start;
    private Point _startLocal;
    private bool _dragging;
    private bool _closing;

    public (int X, int Y, int Width, int Height)? Result { get; private set; }

    private RegionPicker(string hint)
    {
        InitializeComponent();
        HintText.Text = hint;
        Loaded += (_, _) => { Activate(); Focus(); UpdateLabel(Mouse.GetPosition(this)); };
    }

    /// <summary>Muestra la capa y devuelve el rectángulo (mínimo 4×4 px), o null si se canceló.</summary>
    public static (int X, int Y, int Width, int Height)? Pick(string hint)
    {
        var picker = new RegionPicker(hint);
        picker.ShowDialog();
        return picker.Result;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        SetWindowPos(new WindowInteropHelper(this).Handle, 0,
            GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN),
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        GetCursorPos(out _start);
        _startLocal = e.GetPosition(this);
        _dragging = true;
        CaptureMouse();
        Box.Visibility = Visibility.Visible;
        UpdateBox(_startLocal);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Point p = e.GetPosition(this);
        if (_dragging) UpdateBox(p);
        UpdateLabel(p);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        GetCursorPos(out POINT end);
        int x = Math.Min(_start.X, end.X), y = Math.Min(_start.Y, end.Y);
        int w = Math.Abs(end.X - _start.X), h = Math.Abs(end.Y - _start.Y);
        if (w < 4 || h < 4)
        {
            Box.Visibility = Visibility.Collapsed; // demasiado pequeño: volver a intentarlo
            return;
        }
        Result = (x, y, w, h);
        Close();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (!_closing) Close();
    }

    private void UpdateBox(Point p)
    {
        Canvas.SetLeft(Box, Math.Min(p.X, _startLocal.X));
        Canvas.SetTop(Box, Math.Min(p.Y, _startLocal.Y));
        Box.Width = Math.Abs(p.X - _startLocal.X);
        Box.Height = Math.Abs(p.Y - _startLocal.Y);
    }

    private void UpdateLabel(Point p)
    {
        GetCursorPos(out POINT c);
        SizeText.Text = _dragging ? $"{Math.Abs(c.X - _start.X)} × {Math.Abs(c.Y - _start.Y)}" : $"X {c.X}   Y {c.Y}";
        Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double x = p.X + 18, y = p.Y + 18;
        if (x + Label.DesiredSize.Width > ActualWidth) x = p.X - 18 - Label.DesiredSize.Width;
        if (y + Label.DesiredSize.Height > ActualHeight) y = p.Y - 18 - Label.DesiredSize.Height;
        Canvas.SetLeft(Label, x);
        Canvas.SetTop(Label, y);
    }
}
