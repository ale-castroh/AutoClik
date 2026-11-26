using System.Media;
using System.Windows;
using System.Windows.Threading;
using Autoclicker.Engine;
using Autoclicker.Localization;
using Autoclicker.Macros;
using Autoclicker.Scheduling;
using Autoclicker.Views;

namespace Autoclicker;

/// <summary>
/// Programar (2.5): los clics y las macros pueden empezar a una hora o dentro de un rato, y al terminar
/// solos avisar, suspender o apagar el PC. Lo programado vive solo en memoria (cerrar AutoClik lo
/// pierde; ocultarlo en la bandeja, no). Empezar a mano cancela lo programado de esa sección.
/// </summary>
public partial class MainWindow
{
    private sealed record Pending(DateTime Start, EndAction End, Macro? Macro);

    private Pending? _clickPending, _macroPending;          // esperando para empezar
    private EndAction _clickEnd = EndAction.None;           // qué hacer cuando terminen los clics en curso
    private EndAction _macroEnd = EndAction.None;           // ...y la macro en curso
    private Schedule _lastClickSchedule = new(), _lastMacroSchedule = new();
    private bool _fromSchedule;                             // el arranque lo hace la programación
    private bool _macroCompleted;                           // la última reproducción terminó sola
    private DispatcherTimer? _scheduleTimer;

    private void InitSchedule()
    {
        _scheduleTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        _scheduleTimer.Tick += (_, _) => CheckSchedules();
        _scheduleTimer.Start();
        Loaded += (_, _) => RefreshSchedule();
    }

    // ================= Botones de reloj =================

    private void ClickSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (_clickPending != null || _clickEnd != EndAction.None)
        {
            // Pulsar el reloj encendido: cancelar lo programado
            _clickPending = null;
            _clickEnd = EndAction.None;
            _notice = Strings.T("ScheduleCancelled");
            RefreshSchedule();
            return;
        }
        if (StartButton.IsChecked == true) return;
        Schedule? s = ScheduleDialog.Ask(this, Strings.T("ScheduleTitleClicks"), _lastClickSchedule);
        if (s == null) return;
        _lastClickSchedule = s;
        _notice = null;
        if (s.Mode == StartMode.Now) StartClicksFromSchedule(s.End);
        else _clickPending = new Pending(s.StartTime(DateTime.Now), s.End, null);
        RefreshSchedule();
    }

    private void MacroSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (_macroPending != null || _macroEnd != EndAction.None)
        {
            _macroPending = null;
            _macroEnd = EndAction.None;
            _macroNotice = Strings.T("ScheduleCancelled");
            RefreshSchedule();
            return;
        }
        if (_macro is not { } macro || MacroActive) return;
        Schedule? s = ScheduleDialog.Ask(this, Strings.F("ScheduleTitleMacro", macro.Name), _lastMacroSchedule);
        if (s == null) return;
        _lastMacroSchedule = s;
        _macroNotice = null;
        if (s.Mode == StartMode.Now) StartMacroFromSchedule(macro, s.End);
        else _macroPending = new Pending(s.StartTime(DateTime.Now), s.End, macro);
        RefreshSchedule();
    }

    // ================= Empezar a su hora =================

    private void CheckSchedules()
    {
        DateTime now = DateTime.Now;
        if (_clickPending is { } c && now >= c.Start)
        {
            _clickPending = null;
            StartClicksFromSchedule(c.End);
        }
        if (_macroPending is { } m && now >= m.Start)
        {
            _macroPending = null;
            if (m.Macro != null && _macros.Contains(m.Macro)) StartMacroFromSchedule(m.Macro, m.End);
        }
        if (_clickPending != null || _macroPending != null) RefreshSchedule();
    }

    private void StartClicksFromSchedule(EndAction end)
    {
        _fromSchedule = true;
        StartButton.IsChecked = true;
        _fromSchedule = false;
        if (StartButton.IsChecked == true) _clickEnd = end;
        RefreshSchedule();
    }

    private void StartMacroFromSchedule(Macro macro, EndAction end)
    {
        if (MacroActive || _recording || _editor != null) return;
        SelectMacro(macro);
        _fromSchedule = true;
        SetChecked(PlayMacroButton, true, raise: true);
        _fromSchedule = false;
        if (_player.IsPlaying) _macroEnd = end;
        RefreshSchedule();
    }

    /// <summary>Al empezar a mano (botón, atajo, regla de Vigilar...): se olvida lo programado de esa sección.</summary>
    private void OnManualClickStart()
    {
        if (_fromSchedule) return;
        _clickPending = null;
        _clickEnd = EndAction.None;
    }

    private void OnManualMacroStart()
    {
        if (_fromSchedule) return;
        _macroPending = null;
        _macroEnd = EndAction.None;
    }

    // ================= Al terminar =================

    /// <summary>Los clics se pararon: si terminaron solos y había algo para el final, hacerlo.</summary>
    private void OnClicksStopped(StopReason reason)
    {
        EndAction end = _clickEnd;
        _clickEnd = EndAction.None;
        if (reason == StopReason.Completed) RunEndAction(end, Strings.F("NotifyClicksDone", Strings.Clicks(_engine.ClickCount)));
        RefreshSchedule();
    }

    private void OnMacroStopped(string macroName)
    {
        EndAction end = _macroEnd;
        _macroEnd = EndAction.None;
        bool completed = _macroCompleted;
        _macroCompleted = false;
        if (completed) RunEndAction(end, Strings.F("NotifyMacroDone", macroName));
        RefreshSchedule();
    }

    private void RunEndAction(EndAction end, string text)
    {
        switch (end)
        {
            case EndAction.Notify:
                _tray?.ShowBalloon("AutoClik", text);
                SystemSounds.Asterisk.Play();
                break;
            case EndAction.Sleep:
            case EndAction.Shutdown:
                PowerCountdown.Start(end, cancelled: () =>
                {
                    _notice = Strings.T("PowerCancelled");
                    UpdateStatus();
                });
                break;
        }
    }

    // ================= Interfaz =================

    private void RefreshSchedule()
    {
        UpdateStatus();
        UpdateMacroUi();
    }

    /// <summary>Relojes: encendidos si hay algo programado; activos si se puede programar o cancelar.</summary>
    private void UpdateScheduleButtons()
    {
        if (ClickScheduleButton == null || MacroScheduleButton == null) return;
        bool clickOn = _clickPending != null || _clickEnd != EndAction.None;
        bool macroOn = _macroPending != null || _macroEnd != EndAction.None;
        // Como texto: el disparador del estilo compara con "True"
        ClickScheduleButton.Tag = clickOn ? "True" : "False";
        MacroScheduleButton.Tag = macroOn ? "True" : "False";
        ClickScheduleButton.ToolTip = Strings.T(clickOn ? "ScheduleCancelTip" : "ScheduleTip");
        MacroScheduleButton.ToolTip = Strings.T(macroOn ? "ScheduleCancelTip" : "ScheduleTip");
        System.Windows.Automation.AutomationProperties.SetHelpText(ClickScheduleButton, clickOn ? "on" : "off");
        System.Windows.Automation.AutomationProperties.SetHelpText(MacroScheduleButton, macroOn ? "on" : "off");
        ClickScheduleButton.IsEnabled = clickOn || StartButton.IsChecked != true;
        MacroScheduleButton.IsEnabled = macroOn || (_macro != null && !MacroActive);
    }

    /// <summary>Texto de estado de los clics cuando hay algo programado (null = el de siempre).</summary>
    private string? ClickScheduleStatus(string running) =>
        _clickPending is { } p && StartButton.IsChecked != true ? Schedule.Describe(p.Start, DateTime.Now)
        : _clickEnd != EndAction.None && StartButton.IsChecked == true ? Strings.F("ScheduleThenEnd", running, EndLabel(_clickEnd))
        : null;

    private string? MacroScheduleStatus(string running) =>
        _macroPending is { } p && !_player.IsPlaying ? Schedule.Describe(p.Start, DateTime.Now)
        : _macroEnd != EndAction.None && _player.IsPlaying ? Strings.F("ScheduleThenEnd", running, EndLabel(_macroEnd))
        : null;

    private static string EndLabel(EndAction end) => Strings.T(Schedule.EndKey(end)).ToLower(System.Globalization.CultureInfo.CurrentCulture);
}
