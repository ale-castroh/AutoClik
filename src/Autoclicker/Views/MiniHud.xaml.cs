using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Views;

/// <summary>
/// Mini ventana flotante mientras AutoClik está oculto (bandeja o macro): no roba el foco, no sale
/// en Alt+Tab, se arrastra y recuerda su sitio. Con macros deja pasar los clics (WS_EX_TRANSPARENT)
/// para que la propia macro no la pulse; entonces no tiene botón y se para con el atajo.
/// </summary>
public partial class MiniHud : Window
{
    private nint _hwnd;
    private bool _clickThrough;
    private bool _placed;

    public event Action? StopRequested;

    /// <summary>Al soltarla tras arrastrar: esquina superior izquierda en píxeles físicos.</summary>
    public event Action<int, int>? Moved;

    /// <summary>Posición guardada (píxeles físicos); null = arriba en el centro.</summary>
    public int? SavedLeft { get; set; }
    public int? SavedTop { get; set; }

    public MiniHud()
    {
        InitializeComponent();
        Pill.MouseLeftButtonDown += OnDragStart;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        SetExStyle(_hwnd, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, 0);
    }

    /// <summary>Autoclicker: estado, contador y botón Detener (con su tecla).</summary>
    public void ShowClicker(string status, string count, string stopKey)
    {
        TitleText.Text = status;
        NumberText.Text = count;
        StopText.Text = $"{Localization.Strings.T("StopBtn")} · {stopKey}";
        StopButton.Visibility = Visibility.Visible;
        HintText.Visibility = Visibility.Collapsed;
        SetClickThrough(false);
        Appear();
    }

    /// <summary>Macro: nombre, progreso y la pista para parar (sin botón: los clics la atraviesan).</summary>
    public void ShowMacro(string name, string progress, string hint)
    {
        TitleText.Text = name;
        NumberText.Text = progress;
        HintText.Text = hint;
        HintText.Visibility = Visibility.Visible;
        StopButton.Visibility = Visibility.Collapsed;
        SetClickThrough(true);
        Appear();
    }

    private void Appear()
    {
        if (!IsVisible) Show();
        if (!_placed) { Place(); _placed = true; }
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>En su sitio guardado si sigue dentro del escritorio; si no, arriba en el centro.</summary>
    private void Place()
    {
        UpdateLayout();
        GetWindowRect(_hwnd, out RECT r);
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        int left = GetSystemMetrics(76), top = GetSystemMetrics(77);
        int right = left + GetSystemMetrics(78), bottom = top + GetSystemMetrics(79);

        int x = SavedLeft ?? (GetSystemMetrics(0) - w) / 2;
        int y = SavedTop ?? 8;
        if (x < left || y < top || x + 40 > right || y + 20 > bottom) { x = (GetSystemMetrics(0) - w) / 2; y = 8; }
        SetWindowPos(_hwnd, 0, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void SetClickThrough(bool on)
    {
        if (_hwnd == 0 || on == _clickThrough) return;
        _clickThrough = on;
        if (on) SetExStyle(_hwnd, WS_EX_TRANSPARENT, 0);
        else SetExStyle(_hwnd, 0, WS_EX_TRANSPARENT);
        Opacity = on ? 0.85 : 1;
    }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && IsInside(d, StopButton)) return;
        try { DragMove(); } catch (InvalidOperationException) { return; }
        GetWindowRect(_hwnd, out RECT r);
        SavedLeft = r.Left;
        SavedTop = r.Top;
        Moved?.Invoke(r.Left, r.Top);
    }

    private static bool IsInside(DependencyObject d, DependencyObject parent)
    {
        for (var x = d; x != null; x = System.Windows.Media.VisualTreeHelper.GetParent(x))
            if (x == parent) return true;
        return false;
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();
}
