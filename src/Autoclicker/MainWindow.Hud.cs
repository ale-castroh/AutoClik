using System.Windows;
using System.Windows.Threading;
using Autoclicker.Localization;
using Autoclicker.Views;

namespace Autoclicker;

/// <summary>
/// Mini ventana flotante (2.2): solo aparece si AutoClik está oculto (bandeja o macro) y hay algo en
/// marcha. Con el autoclicker tiene Detener; con macros, la pista del atajo (los clics la atraviesan).
/// </summary>
public partial class MainWindow
{
    private MiniHud? _hud;
    private DispatcherTimer? _hudTimer;

    private void InitHud()
    {
        _hudTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
        _hudTimer.Tick += (_, _) => UpdateHud();
        _hudTimer.Start();
    }

    private void UpdateHud()
    {
        bool clicking = StartButton.IsChecked == true;
        bool playing = _player.IsPlaying;
        bool show = HudSwitch.IsChecked == true && !IsVisible && (clicking || playing);

        if (!show)
        {
            if (_hud?.IsVisible == true) _hud.Hide();
            return;
        }

        if (_hud == null)
        {
            _hud = new MiniHud { SavedLeft = _data.Prefs.HudLeft, SavedTop = _data.Prefs.HudTop };
            _hud.StopRequested += () => StartButton.IsChecked = false;
            _hud.Moved += (x, y) =>
            {
                _data.Prefs.HudLeft = x;
                _data.Prefs.HudTop = y;
                ScheduleSave();
            };
        }

        if (playing && _macro != null)
        {
            string loops = ReadLoopInfo().Total > 0 ? $"{_player.Loop}/{ReadLoopInfo().Total}" : $"{_player.Loop}";
            _hud.ShowMacro(_macro.Name, $"{loops} · {Clock(_player.PositionMicros)}",
                Strings.F("HudStopMacro", _playHotkeys?.Current.ToString() ?? "F8"));
        }
        else
        {
            _hud.ShowClicker(StatusText.Text, CountText.Text, _hotkeys?.Current.ToString() ?? "F6");
        }
    }

    private void CloseHud()
    {
        _hudTimer?.Stop();
        _hud?.Close();
        _hud = null;
    }
}
