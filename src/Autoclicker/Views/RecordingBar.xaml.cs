using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using static Autoclicker.Interop.NativeMethods;

using Autoclicker.Localization;

namespace Autoclicker.Views;

/// <summary>Barra flotante "Grabando" (no roba el foco, no aparece en Alt+Tab).</summary>
public partial class RecordingBar : Window
{
    private readonly DispatcherTimer _timer;
    private readonly long _started = Stopwatch.GetTimestamp();
    private int _count;

    public event Action? StopRequested;

    /// <summary>Texto propio en lugar de "N clics" (macros: "8 clics, 23 teclas"). Se añade el tiempo.</summary>
    public Func<string>? InfoProvider { get; set; }

    /// <param name="note">Aviso extra tras la pista (p. ej. "no escribas contraseñas").</param>
    public RecordingBar(string stopKeys, string? note = null)
    {
        InitializeComponent();
        HintText.Text = Strings.F("ToFinish", stopKeys) + (note is null ? "" : " · " + note);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
        Loaded += (_, _) => PlaceTopCenter();
        Closed += (_, _) => _timer.Stop();
    }

    public void SetCount(int count)
    {
        _count = count;
        Refresh();
    }

    public void Refresh()
    {
        TimeSpan t = Compat.Elapsed(_started);
        string info = InfoProvider?.Invoke() ?? $"{_count} {Strings.Clicks(_count)}";
        InfoText.Text = $"{info} · {(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        SetExStyle(new WindowInteropHelper(this).Handle, WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, 0);
    }

    /// <summary>Arriba en el centro del monitor principal, en píxeles físicos.</summary>
    private void PlaceTopCenter()
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        GetWindowRect(hwnd, out RECT r);
        int w = r.Right - r.Left;
        int screenW = GetSystemMetrics(0); // SM_CXSCREEN
        SetWindowPos(hwnd, 0, (screenW - w) / 2, 8, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();
}
