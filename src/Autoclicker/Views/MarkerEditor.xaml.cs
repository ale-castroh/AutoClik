using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Autoclicker.Engine;
using Autoclicker.Markers;
using static Autoclicker.Interop.NativeMethods;

using Autoclicker.Localization;

namespace Autoclicker.Views;

/// <summary>Panel flotante junto a un marcador para editar su botón, tipo, espera y orden.</summary>
public partial class MarkerEditor : Window
{
    private readonly MarkerPoint _point;
    private readonly nint _markerHwnd;
    private bool _loading = true;
    private bool _closing;

    public event Action<int>? MoveRequested;   // -1 antes, +1 después
    public event Action? DeleteRequested;
    public event Action? Edited;

    public MarkerEditor(MarkerPoint point, int number, nint markerHwnd)
    {
        InitializeComponent();
        _point = point;
        _markerHwnd = markerHwnd;

        TitleText.Text = Strings.F("PointN", number);
        CoordsText.Text = $"X {point.X} · Y {point.Y}";

        (point.Button switch
        {
            MouseButtonKind.Left => ButtonLeft,
            MouseButtonKind.Right => ButtonRight,
            MouseButtonKind.Middle => ButtonMiddle,
            _ => ButtonGeneral,
        }).IsChecked = true;

        (point.Kind switch
        {
            ClickKind.Single => KindSingle,
            ClickKind.Double => KindDouble,
            ClickKind.Hold => KindHold,
            _ => KindGeneral,
        }).IsChecked = point.Kind != ClickKind.Drag; // un arrastre no marca ningún tipo

        if (point.IsDrag)
        {
            DragText.Text = Strings.F("DragInfo", point.EndX!, point.EndY!, point.HoldMs ?? 0);
            DragText.Visibility = Visibility.Visible;
        }

        DelayBox.Text = point.DelayMs?.ToString() ?? "";
        DelayBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(Compat.IsDigit);

        foreach (var radio in new[] { ButtonGeneral, ButtonLeft, ButtonRight, ButtonMiddle, KindGeneral, KindSingle, KindDouble, KindHold })
            radio.Checked += (_, _) => Save();
        DelayBox.TextChanged += (_, _) => Save();

        _loading = false;
        Loaded += (_, _) => PlaceNextToMarker();
    }

    private void Save()
    {
        if (_loading) return;
        _point.Button = ButtonLeft.IsChecked == true ? MouseButtonKind.Left
                      : ButtonRight.IsChecked == true ? MouseButtonKind.Right
                      : ButtonMiddle.IsChecked == true ? MouseButtonKind.Middle
                      : null;
        ClickKind? kind = KindSingle.IsChecked == true ? ClickKind.Single
                        : KindDouble.IsChecked == true ? ClickKind.Double
                        : KindHold.IsChecked == true ? ClickKind.Hold
                        : KindGeneral.IsChecked == true ? null
                        : _point.Kind; // ningún tipo marcado: sigue siendo un arrastre
        if (kind != ClickKind.Drag && _point.Kind == ClickKind.Drag)
        {
            // Se eligió otro tipo: deja de ser arrastre.
            _point.EndX = null;
            _point.EndY = null;
            DragText.Visibility = Visibility.Collapsed;
        }
        _point.Kind = kind;
        _point.DelayMs = int.TryParse(DelayBox.Text, out int ms) ? ms : null;
        Edited?.Invoke();
    }

    /// <summary>A la derecha del marcador; si no cabe, a la izquierda. Siempre dentro del escritorio.</summary>
    private void PlaceNextToMarker()
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        GetWindowRect(_markerHwnd, out RECT m);
        GetWindowRect(hwnd, out RECT me);
        int w = me.Right - me.Left, h = me.Bottom - me.Top;

        int left = GetSystemMetrics(SM_XVIRTUALSCREEN), top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int right = left + GetSystemMetrics(SM_CXVIRTUALSCREEN), bottom = top + GetSystemMetrics(SM_CYVIRTUALSCREEN);

        int x = m.Right;
        if (x + w > right) x = m.Left - w;
        int y = Compat.Clamp(m.Top - 20, top, Math.Max(top, bottom - h));
        SetWindowPos(hwnd, 0, Math.Max(x, left), y, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
        Activate();
    }

    private void Earlier_Click(object sender, RoutedEventArgs e) { MoveRequested?.Invoke(-1); SafeClose(); }

    private void Later_Click(object sender, RoutedEventArgs e) { MoveRequested?.Invoke(+1); SafeClose(); }

    private void Delete_Click(object sender, RoutedEventArgs e) { DeleteRequested?.Invoke(); SafeClose(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Enter) SafeClose();
        base.OnKeyDown(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        SafeClose(); // clic fuera: cerrar (los cambios ya están guardados)
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    public void SafeClose()
    {
        if (!_closing) Close();
    }
}
