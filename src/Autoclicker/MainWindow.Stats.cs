using System.Diagnostics;
using System.Windows;
using Autoclicker.Localization;
using Autoclicker.Persistence;
using Autoclicker.Views;

namespace Autoclicker;

/// <summary>Estadísticas y modo portátil (2.6), en Ajustes.</summary>
public partial class MainWindow
{
    private UsageStats _stats = new();
    private Engine.ClickAction _runAction;
    private long _macroStatsStart;    // 0 = no se cuenta (prueba del editor)
    private string? _macroStatsName;

    private void InitStats()
    {
        _stats = UsageStats.Load();
        _syncingPortable = true;
        PortableSwitch.IsChecked = PortableMode.IsActive;
        _syncingPortable = false;
        RefreshStats();
    }

    // ================= Recuento =================

    /// <summary>Al parar el autoclicker: sus clics (o teclas) y el tiempo, sin contar la cuenta atrás.</summary>
    private void CountClickRun()
    {
        long count = _engine.ClickCount;
        TimeSpan time = Compat.Elapsed(_runStarted, Stopwatch.GetTimestamp()) - _runDelay;
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        _stats.AddClicks(_runAction == Engine.ClickAction.Mouse ? count : 0, _runAction == Engine.ClickAction.Key ? count : 0, time);
        _stats.Save();
        RefreshStats();
    }

    /// <summary>Al parar una macro: una reproducción más, con los clics y teclas que llegó a hacer.</summary>
    private void CountMacroRun(Macros.Macro? played)
    {
        if (_macroStatsStart == 0 || _macroStatsName == null) return;
        TimeSpan time = Compat.Elapsed(_macroStatsStart, Stopwatch.GetTimestamp());
        _macroStatsStart = 0;
        long clicks = 0, keys = 0;
        if (played != null)
        {
            // Vueltas completas + lo que llegó a hacer de la última
            int fullLoops = Math.Max(0, _player.Loop - 1);
            long position = _player.PositionMicros;
            foreach (var e in played.Events)
            {
                bool done = e.Micros <= position;
                if (e.Kind == Macros.MacroEventKind.Down) clicks += fullLoops + (done ? 1 : 0);
                else if (e.Kind is Macros.MacroEventKind.KeyDown or Macros.MacroEventKind.TypeChar) keys += fullLoops + (done ? 1 : 0);
            }
        }
        _stats.AddMacro(_macroStatsName, clicks, keys, time);
        _stats.Save();
        RefreshStats();
    }

    // ================= Ajustes → Estadísticas =================

    private void StatsPeriod_Changed(object sender, RoutedEventArgs e) => RefreshStats();

    private void RefreshStats()
    {
        if (StatClicksText == null) return; // aún cargando la ventana
        UsageTotals t = StatsTodayRadio.IsChecked == true ? _stats.TodayTotals : _stats.Total;
        StatClicksText.Text = t.Clicks.ToString("N0");
        StatKeysText.Text = t.Keys.ToString("N0");
        StatMacrosText.Text = t.MacroPlays.ToString("N0");
        StatTimeText.Text = Duration(t.Seconds);
        StatLongestText.Text = Strings.F("StatLongest", Duration(t.LongestSeconds));
        StatSinceText.Text = StatsTodayRadio.IsChecked == true ? "" : Strings.F("StatSince", _stats.Since.ToString("d"));
        UpdateMacroUsage();
    }

    /// <summary>"45 s", "12 min", "6 h 14 min".</summary>
    private static string Duration(long seconds) =>
        seconds < 60 ? $"{seconds} s"
        : seconds < 3600 ? $"{seconds / 60} min"
        : $"{seconds / 3600} h {seconds / 60 % 60} min";

    private void StatsReset_Click(object sender, RoutedEventArgs e)
    {
        if (!NameDialog.Confirm(this, Strings.T("StatsResetTitle"), Strings.T("StatsResetConfirm"), Strings.T("StatsReset")))
            return;
        _stats.Reset();
        _stats.Save();
        RefreshStats();
    }

    /// <summary>Debajo de la macro: cuántas veces se ha reproducido y cuándo fue la última.</summary>
    private void UpdateMacroUsage()
    {
        if (MacroUsageText == null) return;
        if (_macro == null) { MacroUsageText.Text = ""; return; }
        MacroUsage? usage = _stats.UsageOf(_macro.Name);
        if (usage == null || usage.Plays == 0) { MacroUsageText.Text = Strings.T("MacroNeverPlayed"); return; }
        string times = usage.Plays == 1 ? Strings.T("MacroPlayedOnce") : Strings.F("MacroPlayedTimes", usage.Plays);
        MacroUsageText.Text = usage.Last is { } last ? Strings.F("MacroPlayedLast", times, When(last)) : times;
    }

    /// <summary>"hoy a las 14:05", "ayer", "12/11/2026".</summary>
    private static string When(DateTime time) =>
        time.Date == DateTime.Today ? Strings.F("WhenToday", time.ToString("t"))
        : time.Date == DateTime.Today.AddDays(-1) ? Strings.T("WhenYesterday")
        : time.ToString("d");

    // ================= Ajustes → Modo portátil =================

    private bool _syncingPortable;
    private bool _restarting;

    private void Portable_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingPortable) return;
        bool wanted = PortableSwitch.IsChecked == true;
        if (wanted == PortableMode.IsActive) return;
        // La pregunta, después del clic: no bloquear el interruptor mientras se responde
        Dispatcher.InvokeAsync(() => AskPortable(wanted));
    }

    private void AskPortable(bool wanted)
    {
        bool confirmed = NameDialog.Confirm(this, Strings.T(wanted ? "PortableOnTitle" : "PortableOffTitle"),
            wanted ? Strings.F("PortableOnConfirm", SettingsStore.PortablePath)
                   : Strings.F("PortableOffConfirm", SettingsStore.NormalDirectory),
            Strings.T("PortableRestart"));
        if (confirmed)
        {
            StartButton.IsChecked = false;
            if (PlayMacroButton.IsChecked == true) SetChecked(PlayMacroButton, false, raise: true);
            SaveNow();
            _stats.Save();
            if (wanted ? PortableMode.Enable() : PortableMode.Disable())
            {
                ((App)Application.Current).Restart();
                _restarting = true;
                _exiting = true;
                Close();
                return;
            }
            NameDialog.Confirm(this, Strings.T(wanted ? "PortableOnTitle" : "PortableOffTitle"),
                Strings.T(wanted ? "PortableOnFailed" : "PortableOffFailed"), Strings.T("Ok"), showCancel: false);
        }
        _syncingPortable = true;
        PortableSwitch.IsChecked = PortableMode.IsActive;
        _syncingPortable = false;
    }
}
