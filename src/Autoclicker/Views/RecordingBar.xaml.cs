using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Views;

/// <summary>Barra flotante "Grabando" (no roba el foco, no aparece en Alt+Tab).</summary>
public partial class RecordingBar : Window
{
    private readonly DispatcherTimer _timer;
    private readonly long _started = Stopwatch.GetTimestamp();
    private int _count;

    public event Action? StopRequested;

    public RecordingBar(string stopKeys)
    {
        InitializeComponent();
        HintText.Text = $"{stopKeys} para terminar";
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Loaded += (_, _) => PlaceTopCenter();
        Closed += (_, _) => _timer.Stop();
    }

    public void SetCount(int count)
    {
        _count = count;
        Refresh();
    }

    private void Refresh()
    {
        TimeSpan t = Stopwatch.GetElapsedTime(_started);
        InfoText.Text = $"{_count} {(_count == 1 ? "clic" : "clics")} · {(int)t.TotalMinutes}:{t.Seconds:00}";
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
