using System.Windows;
using System.Windows.Threading;
using Autoclicker.Localization;
using Autoclicker.Scheduling;

namespace Autoclicker.Views;

/// <summary>
/// Antes de suspender o apagar el PC: cuenta atrás (60 s) siempre visible, con Cancelar (el botón por
/// defecto: Intro o Esc cancelan) y "Apagar ya". Si nadie lo cancela, lo hace al llegar a cero.
/// </summary>
public partial class PowerCountdown : Window
{
    private readonly EndAction _action;
    private readonly DispatcherTimer _timer;
    private int _left;
    private bool _done;

    private PowerCountdown(EndAction action)
    {
        InitializeComponent();
        _action = action;
        _left = Power.CountdownSeconds;
        NowButton.Content = Strings.T(action == EndAction.Shutdown ? "PowerShutdownNow" : "PowerSleepNow");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _left--;
            if (_left <= 0) Finish(run: true);
            else Refresh();
        };
        Refresh();
        Loaded += (_, _) => { _timer.Start(); Activate(); };
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
    }

    /// <summary>Muestra la cuenta atrás; <paramref name="cancelled"/> se llama si el usuario la cancela.</summary>
    public static void Start(EndAction action, Action cancelled)
    {
        var window = new PowerCountdown(action);
        window.Closed += (_, _) => { if (!window._ran) cancelled(); };
        window.Show();
    }

    private bool _ran;

    private void Refresh() =>
        TitleText.Text = Strings.F(_action == EndAction.Shutdown ? "PowerShutdownIn" : "PowerSleepIn", _left);

    private void Finish(bool run)
    {
        if (_done) return;
        _done = true;
        _timer.Stop();
        _ran = run;
        Close();
        if (run) Power.Run(_action);
    }

    private void Now_Click(object sender, RoutedEventArgs e) => Finish(run: true);

    private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(run: false);
}
