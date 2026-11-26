using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Autoclicker.Engine;
using Autoclicker.Input;
using Autoclicker.Localization;
using Autoclicker.Views;
using Autoclicker.Watch;

namespace Autoclicker;

/// <summary>
/// Sección Vigilar (2.4, experimental): reglas "cuando aparezca este color o esta imagen, haz esto".
/// Las reglas son del perfil; el vigilante corre en su hilo y aquí se ejecuta la acción.
/// </summary>
public partial class MainWindow
{
    private readonly Watcher _watcher = new();
    private List<WatchRule> _watchRules = [];
    private List<WatchRule> _runningRules = [];   // copia con la que se empezó a vigilar
    private HotkeyManager? _watchHotkeys;          // vigilar (F10)
    private int _watchCount;
    private string? _watchLast;
    private DateTime _watchLastAt;
    private string? _watchNotice;
    private DispatcherTimer? _watchTimer;

    private static readonly ClickSettings WatchClick = new() { Button = MouseButtonKind.Left, Kind = ClickKind.Single };

    private void InitWatch()
    {
        _watcher.Triggered += (index, x, y) => Dispatcher.InvokeAsync(() => OnRuleTriggered(index, x, y));
        _watchTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _watchTimer.Tick += (_, _) =>
        {
            if (WatchToggle.IsChecked == true && !_watcher.IsRunning) StopWatchingAfterError();
            UpdateWatchUi();
        };
    }

    /// <summary>Al cargar o cambiar de perfil.</summary>
    private void LoadWatchRules(IEnumerable<WatchRule> rules)
    {
        _watchRules = rules.Select(r => r.Clone()).ToList();
        RebuildRuleList();
        RestartWatcherIfRunning();
    }

    private List<WatchRule> CaptureWatchRules() => _watchRules.Select(r => r.Clone()).ToList();

    // ================= Lista de reglas =================

    private void RebuildRuleList()
    {
        if (WatchRuleList == null) return;
        WatchRuleList.Children.Clear();
        for (int i = 0; i < _watchRules.Count; i++) WatchRuleList.Children.Add(RuleRow(_watchRules[i], i));
        WatchEmptyText.Visibility = _watchRules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateWatchUi();
    }

    private UIElement RuleRow(WatchRule rule, int index)
    {
        var toggle = new CheckBox
        {
            IsChecked = rule.Enabled,
            Style = (Style)FindResource("ToggleSwitchStyle"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 6, 0),
        };
        AutomationProperties.SetAutomationId(toggle, $"RuleSwitch{index}");
        toggle.Checked += (_, _) => SetRuleEnabled(rule, true);
        toggle.Unchecked += (_, _) => SetRuleEnabled(rule, false);
        DockPanel.SetDock(toggle, Dock.Right);

        FrameworkElement icon;
        if (rule.Kind == WatchKind.Color)
        {
            icon = new Border
            {
                Width = 18, Height = 18, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb((byte)(rule.Color >> 16), (byte)(rule.Color >> 8), (byte)rule.Color)),
                BorderBrush = (Brush)FindResource("ControlStrongBorderBrush"),
            };
        }
        else
        {
            icon = new Image
            {
                Source = WatchImages.Preview(rule.Image), Width = 30, Height = 20, Stretch = Stretch.Uniform,
            };
        }
        var iconBox = new Grid { Width = 30, Height = 22, VerticalAlignment = VerticalAlignment.Center };
        iconBox.Children.Add(icon);

        var texts = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = RuleTitle(rule), TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13 });
        texts.Children.Add(new TextBlock
        {
            Text = RuleSummary(rule), TextTrimming = TextTrimming.CharacterEllipsis,
            Style = (Style)FindResource("RowHintStyle"), TextWrapping = TextWrapping.NoWrap,
        });

        var content = new DockPanel();
        DockPanel.SetDock(iconBox, Dock.Left);
        content.Children.Add(iconBox);
        content.Children.Add(texts);

        var button = new Button { Content = content, Style = (Style)FindResource("RuleRowButtonStyle") };
        AutomationProperties.SetAutomationId(button, $"Rule{index}");
        AutomationProperties.SetName(button, RuleTitle(rule));
        button.Click += (_, _) => EditRule(rule);

        var row = new DockPanel { Opacity = rule.Enabled ? 1 : 0.55 };
        row.Children.Add(toggle);
        row.Children.Add(button);
        return row;
    }

    private static string RuleTitle(WatchRule rule) => rule.Kind == WatchKind.Color
        ? Strings.F("WatchColorAt", rule.X, rule.Y)
        : Strings.F("WatchImageIn", rule.HasZone
            ? Strings.F("WatchZone", rule.ZoneW, rule.ZoneH, rule.ZoneX, rule.ZoneY)
            : Strings.T("WatchWholeScreen"));

    private static string RuleSummary(WatchRule rule)
    {
        string action = rule.Action switch
        {
            WatchAction.ClickAt => Strings.F("WatchActClickAt", rule.ActionX, rule.ActionY),
            WatchAction.Key => Strings.F("WatchActKey", Hotkey.KeyName(rule.KeyVk)),
            WatchAction.StartClicks => Strings.T("WatchActStartClicks"),
            WatchAction.PlayMacro => Strings.F("WatchActPlayMacro", rule.MacroName ?? ""),
            WatchAction.StopAll => Strings.T("WatchActStopAll"),
            _ => Strings.T("WatchActClickOn"),
        };
        return Strings.F("WatchSummary", action, Strings.T(rule.EveryTime ? "WatchEvery" : "WatchOnce"));
    }

    private void SetRuleEnabled(WatchRule rule, bool enabled)
    {
        if (rule.Enabled == enabled) return;
        rule.Enabled = enabled;
        RulesChanged();
    }

    private void AddRule_Click(object sender, RoutedEventArgs e) => EditRule(null);

    private void EditRule(WatchRule? rule)
    {
        var (result, edited) = WatchRuleDialog.Ask(this, rule, _macros.Select(m => m.Name).ToList());
        switch (result)
        {
            case WatchRuleDialog.Outcome.Saved when rule == null:
                _watchRules.Add(edited!);
                break;
            case WatchRuleDialog.Outcome.Saved:
                _watchRules[_watchRules.IndexOf(rule!)] = edited!;
                break;
            case WatchRuleDialog.Outcome.Deleted:
                _watchRules.Remove(rule!);
                break;
            default:
                return;
        }
        RulesChanged();
    }

    private void RulesChanged()
    {
        _watchNotice = null;
        RebuildRuleList();
        RestartWatcherIfRunning();
        ScheduleSave();
    }

    // ================= Vigilar =================

    private void Watch_Checked(object sender, RoutedEventArgs e)
    {
        if (!_watchRules.Any(r => r.Enabled))
        {
            _watchNotice = Strings.T("WatchNoActive");
            Dispatcher.InvokeAsync(() => WatchToggle.IsChecked = false);
            UpdateWatchUi();
            return;
        }
        _watchNotice = null;
        _watchCount = 0;
        _watchLast = null;
        StartWatcher();
        _watchTimer?.Start();
        UpdateWatchUi();
    }

    private void Watch_Unchecked(object sender, RoutedEventArgs e)
    {
        _watcher.Stop();
        _watchTimer?.Stop();
        UpdateWatchUi();
    }

    private void StartWatcher()
    {
        _runningRules = _watchRules.Select(r => r.Clone()).ToList();
        _watcher.Start(_runningRules);
    }

    private void RestartWatcherIfRunning()
    {
        if (WatchToggle?.IsChecked != true) return;
        _watcher.Stop();
        if (_watchRules.Any(r => r.Enabled)) StartWatcher();
        else WatchToggle.IsChecked = false;
    }

    private void StopWatchingAfterError()
    {
        if (_watcher.LastError is { } error) ErrorLog.Write("vigilar", error);
        _watchNotice = Strings.T("ClickError");
        WatchToggle.IsChecked = false;
    }

    private void OnWatchHotkey()
    {
        if (ExperimentalSwitch.IsChecked != true) return;
        WatchToggle.IsChecked = WatchToggle.IsChecked != true;
    }

    /// <summary>Una regla se cumplió: hacer su acción (en el hilo de la interfaz).</summary>
    private void OnRuleTriggered(int index, int x, int y)
    {
        if (WatchToggle.IsChecked != true || index >= _runningRules.Count) return;
        WatchRule rule = _runningRules[index];
        _watchCount++;
        _watchLast = RuleTitle(rule);
        _watchLastAt = DateTime.Now;

        switch (rule.Action)
        {
            case WatchAction.ClickOn:
                ClickAt(x, y);
                break;
            case WatchAction.ClickAt:
                ClickAt(rule.ActionX, rule.ActionY);
                break;
            case WatchAction.Key:
                int vk = rule.KeyVk;
                Task.Run(() =>
                {
                    WaitForMouseRelease();
                    KeyInput.Send(vk, down: true, up: false);
                    Thread.Sleep(50); // algo larga: los juegos leen el teclado a su ritmo
                    KeyInput.Send(vk, down: false, up: true);
                });
                break;
            case WatchAction.StartClicks:
                if (StartButton.IsChecked != true && !MacroActive) StartButton.IsChecked = true;
                break;
            case WatchAction.PlayMacro:
                var macro = _macros.FirstOrDefault(m => m.Name == rule.MacroName);
                if (macro == null) { _watchNotice = Strings.F("WatchMacroMissing", rule.MacroName ?? ""); break; }
                if (MacroActive || _recording || _editor != null) break;
                SelectMacro(macro);
                UpdateMacroUi();
                SetChecked(PlayMacroButton, true, raise: true);
                break;
            case WatchAction.StopAll:
                if (StartButton.IsChecked == true) StartButton.IsChecked = false;
                if (PlayMacroButton.IsChecked == true) PlayMacroButton.IsChecked = false;
                break;
        }
        UpdateWatchUi();
    }

    /// <summary>Un clic izquierdo en ese punto, fuera del hilo de la interfaz (como el anti-AFK).</summary>
    private static void ClickAt(int x, int y)
    {
        var at = new EngineNative.POINT { X = x, Y = y };
        Task.Run(() =>
        {
            WaitForMouseRelease();
            MouseInput.MoveCursor(at);
            Thread.Sleep(15);
            MouseInput.Send(WatchClick, at, down: true, up: false);
            Thread.Sleep(25);
            MouseInput.Send(WatchClick, at, down: false, up: true);
        });
    }

    /// <summary>
    /// Si tienes un botón del ratón pulsado (p. ej. arrastrando una ventana), esperar a que lo sueltes,
    /// como mucho 5 s: durante un arrastre Windows descarta las teclas y un clic lo estropearía.
    /// </summary>
    private static void WaitForMouseRelease()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
        while (sw.ElapsedMilliseconds < 5000 && (Down(0x01) || Down(0x02) || Down(0x04)))
            Thread.Sleep(20);
        if (sw.ElapsedMilliseconds > 0) Thread.Sleep(60); // que termine de soltarse la ventana
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vk);

    private void UpdateWatchUi()
    {
        if (WatchStatusText == null) return;
        bool on = WatchToggle.IsChecked == true;
        int active = (on ? _runningRules : _watchRules).Count(r => r.Enabled);
        WatchStatusText.Text = on
            ? Strings.F("WatchOn", active == 1 ? Strings.T("WatchRules1") : Strings.F("WatchRulesN", active))
            : _watchNotice ?? Strings.T("WatchOff");
        WatchCountText.Text = _watchCount.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        if (_watchLast == null)
        {
            WatchDetailText.Text = Strings.T("WatchTimes");
        }
        else
        {
            double minutes = (DateTime.Now - _watchLastAt).TotalMinutes;
            WatchDetailText.Text = Strings.F("WatchLast", _watchLast,
                minutes < 1 ? Strings.T("WatchJustNow") : Strings.F("WatchMinutesAgo", (int)minutes));
        }
    }

    private void DisposeWatch()
    {
        _watchTimer?.Stop();
        _watcher.Dispose();
    }
}
