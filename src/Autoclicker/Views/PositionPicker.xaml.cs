using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Views;

/// <summary>
/// Capa a pantalla completa (todos los monitores) para elegir un punto con el ratón.
/// Devuelve coordenadas físicas reales, las mismas que usa el motor.
/// </summary>
public partial class PositionPicker : Window
{
    public (int X, int Y)? Result { get; private set; }

    /// <summary>Modo varios puntos: cada clic llama a esto y el selector sigue abierto.</summary>
    private Action<int, int>? _onEachPoint;

    public PositionPicker()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            UpdateAt(Mouse.GetPosition(this));
        };
    }

    /// <summary>Muestra el selector y devuelve el punto elegido, o null si se canceló.</summary>
    public static (int X, int Y)? Pick(Window? owner)
    {
        var picker = new PositionPicker { Owner = owner };
        picker.ShowDialog();
        return picker.Result;
    }

    /// <summary>Selector continuo: cada clic añade un punto; Esc o clic derecho terminan.</summary>
    public static void PickMany(Action<int, int> onEachPoint)
    {
        var picker = new PositionPicker { _onEachPoint = onEachPoint };
        picker.HintText.Text = "Clic para añadir punto · Esc para terminar";
        picker.ShowDialog();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Cubrir el escritorio virtual entero, en píxeles físicos.
        SetWindowPos(new WindowInteropHelper(this).Handle, 0,
            GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN),
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    protected override void OnMouseMove(MouseEventArgs e) => UpdateAt(e.GetPosition(this));

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        GetCursorPos(out POINT p);
        if (_onEachPoint != null)
        {
            _onEachPoint(p.X, p.Y);
            Activate(); // el marcador nuevo no roba el foco, pero por si acaso
            return;
        }
        Result = (p.X, p.Y);
        Close();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private bool _closing;

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (!_closing) Close(); // Alt+Tab o similar: cancelar
    }

    private void UpdateAt(Point p)
    {
        GetCursorPos(out POINT c);
        Coords.Text = $"X {c.X}   Y {c.Y}";

        HLine.Width = ActualWidth;
        Canvas.SetTop(HLine, p.Y);
        VLine.Height = ActualHeight;
        Canvas.SetLeft(VLine, p.X);

        Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double x = p.X + 18, y = p.Y + 18;
        if (x + Label.DesiredSize.Width > ActualWidth) x = p.X - 18 - Label.DesiredSize.Width;
        if (y + Label.DesiredSize.Height > ActualHeight) y = p.Y - 18 - Label.DesiredSize.Height;
        Canvas.SetLeft(Label, x);
        Canvas.SetTop(Label, y);
    }
}
