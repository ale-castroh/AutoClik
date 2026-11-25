using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Autoclicker.Input;
using Autoclicker.Localization;
using Autoclicker.Macros;
using Autoclicker.Views;

namespace Autoclicker;

/// <summary>
/// Sección Macros: grabar ratón y teclado, reproducir (veces, sin fin o durante un tiempo, a varias
/// velocidades) y la biblioteca de macros (elegir, renombrar, duplicar, exportar, importar, eliminar).
/// Mientras se graba o se reproduce, la ventana se oculta: así la macro nunca clica sobre AutoClik.
/// </summary>
public partial class MainWindow
{
    private readonly MacroStore _macroStore = new();
    private readonly MacroPlayer _player = new();
    private List<Macro> _macros = [];
    private Macro? _macro;
    private bool _macroRecording;
    private bool _macroBusy;          // evita reentrar al marcar/desmarcar los botones desde código
    private bool _hiddenForMacro;     // la ventana se ocultó para grabar/reproducir (volver a mostrarla)
    private string? _macroNotice;     // aviso puntual ("Terminado", "Nada grabado"...)
    private RecordingBar? _macroBar;
    private DispatcherTimer? _macroTimer;

    private bool MacroActive => _macroRecording || _player.IsPlaying;

    /// <summary>Carga la biblioteca. Llamar tras InitPersistence (las preferencias ya están aplicadas).</summary>
    private void InitMacros()
    {
        _macros = _macroStore.LoadAll();
        _macro = _macros.FirstOrDefault(m => m.Name == _data.Prefs.MacroName) ?? _macros.FirstOrDefault();
        _player.Finished += completed => Dispatcher.InvokeAsync(() => OnPlaybackFinished(completed));
        _macroTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _macroTimer.Tick += (_, _) => UpdateMacroUi();
        MacroRecorder.Changed += UpdateMacroBar;
        InitSpeedBox();
        UpdateMacroUi();
    }

    // ================= Atajo propio de cada macro (2.2) =================

    private readonly List<(Macro Macro, HotkeyManager Manager)> _macroHotkeys = [];

    /// <summary>Vuelve a registrar los atajos de las macros (al abrir, y al asignar, renombrar o borrar).</summary>
    private void RebuildMacroHotkeys()
    {
        foreach (var (_, manager) in _macroHotkeys) manager.Dispose();
        _macroHotkeys.Clear();
        if (_hwnd == 0) return;

        int id = 100;
        foreach (var macro in _macros)
        {
            if (macro.Hotkey is not { } key) continue;
            var manager = new HotkeyManager(_hwnd, id++);
            Macro m = macro;
            manager.Pressed += () => OnMacroHotkey(m);
            if (manager.TrySet(key)) _macroHotkeys.Add((macro, manager));
            else manager.Dispose(); // otra app ya usa esa tecla
        }
    }

    private bool HandleMacroHotkeyMessage(int msg, nint wParam) => _macroHotkeys.Any(h => h.Manager.HandleMessage(msg, wParam));

    /// <summary>Atajo de una macro: la reproduce; si ya se está reproduciendo esa, la para.</summary>
    private void OnMacroHotkey(Macro macro)
    {
        if (_macroRecording || _recording) return;
        if (_player.IsPlaying)
        {
            bool same = macro == _macro;
            SetChecked(PlayMacroButton, false, raise: true);
            if (same) return;
        }
        SelectMacro(macro);
        UpdateMacroUi();
        SetChecked(PlayMacroButton, true, raise: true);
    }

    private void AssignMacroHotkey()
    {
        if (_macro is not { } macro) return;
        var (accepted, key) = KeyDialog.Ask(this, Strings.F("MacroHotkeyTitle", macro.Name), macro.Hotkey, k => MacroHotkeyConflict(k, macro));
        if (!accepted) return;
        macro.HotkeyVk = key?.VirtualKey;
        macro.HotkeyModifiers = key?.Modifiers ?? 0;
        _macroStore.Save(macro);
        RebuildMacroHotkeys();
        UpdateMacroUi();
    }

    /// <summary>Error si la tecla ya es otro atajo de AutoClik (F6, F7, F8 u otra macro).</summary>
    private string? MacroHotkeyConflict(Hotkey key, Macro self)
    {
        if (AllHotkeys.Any(h => h.Current == key)) return Strings.F("HotkeyTaken", key);
        var other = _macros.FirstOrDefault(m => m != self && m.Hotkey == key);
        return other != null ? Strings.F("MacroHotkeyUsed", key, other.Name) : null;
    }

    // ================= Grabar =================

    private void RecordMacro_Checked(object sender, RoutedEventArgs e)
    {
        if (_macroBusy) return;
        if (_player.IsPlaying || _recording) { SetChecked(RecordMacroButton, false); return; }

        StartButton.IsChecked = false; // el autoclicker no puede clicar mientras se graba
        _macroNotice = null;
        _macroRecording = true;
        HideForMacro();

        var ignored = new[] { _recordHotkeys?.Current.VirtualKey, _playHotkeys?.Current.VirtualKey, _hotkeys?.Current.VirtualKey }
            .Where(v => v.HasValue).Select(v => v!.Value);
        MacroRecorder.Start(SelectedRecordMode, ignored);

        _macroBar = new RecordingBar(_recordHotkeys?.Current.ToString() ?? "F7", Strings.T("MacroBarPrivacy"))
        {
            InfoProvider = () => Strings.F("MacroBarCounts", MacroRecorder.ClickCount, Strings.Clicks(MacroRecorder.ClickCount),
                                           MacroRecorder.KeyCount, Strings.Keys(MacroRecorder.KeyCount)),
        };
        _macroBar.StopRequested += () => SetChecked(RecordMacroButton, false, raise: true);
        _macroBar.Show();
        UpdateMacroUi();
    }

    private void RecordMacro_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_macroBusy || !_macroRecording) return;
        _macroRecording = false;

        List<MacroEvent> events = MacroRecorder.Stop();
        _macroBar?.Close();
        _macroBar = null;

        if (events.Count == 0)
        {
            _macroNotice = Strings.T("MacroNothing");
        }
        else
        {
            var macro = new Macro
            {
                Name = MacroStore.UniqueName(_macros, Strings.F("MacroN", _macros.Count + 1)),
                Events = events,
            };
            _macroStore.Save(macro);
            _macros.Add(macro);
            SelectMacro(macro);
            _macroNotice = Strings.T("MacroSaved");
        }
        ShowAfterMacro();
        UpdateMacroUi();
    }

    private void UpdateMacroBar() => _macroBar?.Refresh();

    // ================= Reproducir =================

    private void PlayMacro_Checked(object sender, RoutedEventArgs e)
    {
        if (_macroBusy) return;
        if (_macro == null || _macro.Events.Count == 0 || _macroRecording || _recording)
        {
            if (_macro == null) _macroNotice = Strings.T("MacroNoneHint");
            SetChecked(PlayMacroButton, false);
            UpdateMacroUi();
            return;
        }

        StartButton.IsChecked = false; // un solo "piloto automático" a la vez
        _macroNotice = null;
        HideForMacro();
        if (EmergencySwitch.IsChecked == true) EmergencyCornerWatcher.Start();
        _player.Play(_macro, ReadPlayOptions());
        _macroTimer?.Start();
        MacroOptionsCard.IsEnabled = false;
        RecordMacroButton.IsEnabled = false;
        UpdateIcons();
        UpdateMacroUi();
    }

    private void PlayMacro_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_macroBusy) return;
        _player.Stop();
        if (StartButton.IsChecked != true) EmergencyCornerWatcher.Stop();
        _macroTimer?.Stop();
        MacroOptionsCard.IsEnabled = true;
        RecordMacroButton.IsEnabled = true;
        _macroNotice ??= Strings.T("Stopped");
        ShowAfterMacro();
        UpdateIcons();
        UpdateMacroUi();
    }

    private void OnPlaybackFinished(bool completed)
    {
        if (_player.LastError is { } error) ErrorLog.Write("macro", error);
        if (PlayMacroButton.IsChecked != true) return;
        _macroNotice = Strings.T(_player.LastError != null ? "ClickError" : completed ? "Completed" : "Stopped");
        PlayMacroButton.IsChecked = false; // -> PlayMacro_Unchecked
    }

    private MacroPlayOptions ReadPlayOptions()
    {
        MacroRepeat repeat = MacroCountRadio.IsChecked == true ? MacroRepeat.Count
                           : MacroForeverRadio.IsChecked == true ? MacroRepeat.Forever
                           : MacroTimeRadio.IsChecked == true ? MacroRepeat.Time
                           : MacroRepeat.Once;
        return new MacroPlayOptions(SelectedMacroSpeed, repeat,
            (int)Compat.Clamp(Number(MacroCountBox), 1, 1_000_000),
            TimeSpan.FromMinutes(Compat.Clamp(Number(MacroMinutesBox), 1, 10_000)));
    }

    private double SelectedMacroSpeed =>
        MacroSpeedHalf.IsChecked == true ? 0.5
        : MacroSpeed2.IsChecked == true ? 2
        : MacroSpeed4.IsChecked == true ? 4
        : MacroSpeedCustom.IsChecked == true ? CustomMacroSpeed
        : 1;

    /// <summary>Velocidad escrita en "Otra" (acepta 1,5 y 1.5), entre ×0,1 y ×20.</summary>
    private double CustomMacroSpeed =>
        double.TryParse(MacroSpeedBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double v) && v > 0
            ? Compat.Clamp(v, 0.1, 20) : 1;

    private static string FormatSpeed(double v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>En el campo de velocidad, solo dígitos y un separador decimal.</summary>
    private void InitSpeedBox()
    {
        MacroSpeedBox.PreviewTextInput += (_, e) =>
        {
            string result = MacroSpeedBox.Text.Remove(MacroSpeedBox.SelectionStart, MacroSpeedBox.SelectionLength)
                .Insert(MacroSpeedBox.SelectionStart, e.Text);
            e.Handled = !(result.All(c => Compat.IsDigit(c) || c is ',' or '.') && result.Count(c => c is ',' or '.') <= 1);
        };
        MacroSpeedBox.GotKeyboardFocus += (_, _) => MacroSpeedBox.SelectAll();
        MacroSpeedBox.LostKeyboardFocus += (_, _) => MacroSpeedBox.Text = FormatSpeed(CustomMacroSpeed);
    }

    private MacroRecordMode SelectedRecordMode =>
        MacroModeMouse.IsChecked == true ? MacroRecordMode.Mouse
        : MacroModeKeys.IsChecked == true ? MacroRecordMode.Keys
        : MacroRecordMode.All;

    // ================= Atajos (F7 grabar, F8 reproducir) =================

    private void OnRecordHotkey()
    {
        if (_player.IsPlaying || _recording) return;
        SetChecked(RecordMacroButton, RecordMacroButton.IsChecked != true, raise: true);
    }

    private void OnPlayHotkey()
    {
        if (_macroRecording || _recording) return;
        SetChecked(PlayMacroButton, PlayMacroButton.IsChecked != true, raise: true);
    }

    // ================= Biblioteca =================

    private void MacroPick_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = MacroPickButton, Placement = PlacementMode.Bottom };

        foreach (var macro in _macros)
        {
            var item = new MenuItem
            {
                Header = macro.Name,
                Icon = macro == _macro ? "" : null,
                InputGestureText = macro.Hotkey?.ToString() ?? "",
            };
            item.Click += (_, _) => { SelectMacro(macro); UpdateMacroUi(); };
            menu.Items.Add(item);
        }
        if (_macros.Count > 0) menu.Items.Add(new Separator());

        bool has = _macro != null;
        menu.Items.Add(MacroAction("MacroAssignHotkey", "", AssignMacroHotkey, has));
        menu.Items.Add(MacroAction("MacroRename", "", RenameMacro, has));
        menu.Items.Add(MacroAction("MacroDuplicate", "", DuplicateMacro, has));
        menu.Items.Add(MacroAction("MacroExport", "", ExportMacro, has));
        menu.Items.Add(MacroAction("MacroImport", "", ImportMacro, true));
        menu.Items.Add(MacroAction("MacroDelete", "", DeleteMacro, has));
        menu.IsOpen = true;
    }

    private static MenuItem MacroAction(string key, string icon, Action action, bool enabled)
    {
        var item = new MenuItem { Header = Strings.T(key), Icon = icon, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    private void SelectMacro(Macro? macro)
    {
        _macro = macro;
        _macroNotice = null;
        ScheduleSave();
    }

    private string? ValidateMacroName(string name, Macro? except) =>
        _macros.Any(m => m != except && string.Equals(m.Name, name, StringComparison.CurrentCultureIgnoreCase))
            ? Strings.T("MacroExists") : null;

    private void RenameMacro()
    {
        if (_macro is not { } macro) return;
        string? name = NameDialog.AskName(this, Strings.T("MacroRenameTitle"), macro.Name, n => ValidateMacroName(n, macro));
        if (name == null || name == macro.Name) return;
        if (_macroStore.Rename(macro, name))
        {
            _macros = _macros.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            RebuildMacroHotkeys();
            ScheduleSave();
        }
        UpdateMacroUi();
    }

    private void DuplicateMacro()
    {
        if (_macro is not { } macro) return;
        var copy = macro.Clone(MacroStore.UniqueName(_macros, Strings.F("MacroCopyOf", macro.Name)));
        _macroStore.Save(copy);
        _macros.Add(copy);
        _macros = _macros.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        SelectMacro(copy);
        UpdateMacroUi();
    }

    private void DeleteMacro()
    {
        if (_macro is not { } macro) return;
        if (!NameDialog.Confirm(this, Strings.T("MacroDelete"), Strings.F("MacroDeleteConfirm", macro.Name), Strings.T("Delete")))
            return;
        _macroStore.Delete(macro);
        _macros.Remove(macro);
        RebuildMacroHotkeys();
        SelectMacro(_macros.FirstOrDefault());
        UpdateMacroUi();
    }

    private void ExportMacro()
    {
        if (_macro is not { } macro) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = macro.Name,
            DefaultExt = MacroStore.ExportExtension,
            Filter = Strings.T("MacroFileFilter"),
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _macroStore.Export(macro, dialog.FileName);
            _macroNotice = Strings.T("MacroExported");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write("exportar macro", ex);
            _macroNotice = Strings.T("MacroFileError");
        }
        UpdateMacroUi();
    }

    private void ImportMacro()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Strings.T("MacroFileFilter") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            Macro macro = MacroStore.Import(dialog.FileName);
            if (macro.Hotkey is { } k && MacroHotkeyConflict(k, macro) != null) macro.HotkeyVk = null;
            macro.Name = MacroStore.UniqueName(_macros, macro.Name.Length > 0 ? macro.Name : Path.GetFileNameWithoutExtension(dialog.FileName));
            _macroStore.Save(macro);
            _macros.Add(macro);
            _macros = _macros.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            SelectMacro(macro);
            RebuildMacroHotkeys();
            _macroNotice = Strings.T("MacroImported");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException
                                       or InvalidCastException or OverflowException)
        {
            ErrorLog.Write("importar macro", ex);
            _macroNotice = Strings.T("MacroFileError");
        }
        UpdateMacroUi();
    }

    // ================= Lectura (estado, tiempo, detalle, progreso) =================

    private void UpdateMacroUi()
    {
        MacroNameText.Text = _macro?.Name ?? Strings.T("MacroNone");
        PlayMacroButton.IsEnabled = _macro != null || PlayMacroButton.IsChecked == true;

        if (_player.IsPlaying && _macro != null)
        {
            long duration = Math.Max(1, _macro.DurationMicros);
            long position = Math.Min(_player.PositionMicros, duration);
            MacroOptions loopInfo = ReadLoopInfo();
            MacroStatusText.Text = loopInfo.Total > 0
                ? Strings.F("MacroPlayingLoopOf", _player.Loop, loopInfo.Total)
                : Strings.F("MacroPlayingLoop", _player.Loop);
            MacroTimeText.Text = Clock(position);
            MacroDetailText.Text = Strings.F("MacroOfTotal", Clock(duration));
            MacroProgress.Visibility = Visibility.Visible;
            MacroProgressFill.Width = MacroProgress.ActualWidth * position / duration;
            return;
        }

        MacroProgress.Visibility = Visibility.Collapsed;
        if (_macroRecording)
        {
            MacroStatusText.Text = Strings.T("Recording");
            return;
        }
        if (_macro == null)
        {
            MacroStatusText.Text = _macroNotice ?? Strings.T("MacroEmptyState");
            MacroTimeText.Text = Clock(0);
            MacroDetailText.Text = Strings.T("MacroEmptyHint");
            return;
        }
        MacroStatusText.Text = _macroNotice ?? Strings.T("MacroReady");
        MacroTimeText.Text = Clock(_macro.DurationMicros);
        MacroDetailText.Text = Strings.F("MacroCounts",
            _macro.Clicks, Strings.Clicks(_macro.Clicks), _macro.Keys, Strings.Keys(_macro.Keys), _macro.Moves);
    }

    private readonly record struct MacroOptions(int Total);

    private MacroOptions ReadLoopInfo() =>
        new(MacroOnceRadio.IsChecked == true ? 1
            : MacroCountRadio.IsChecked == true ? (int)Compat.Clamp(Number(MacroCountBox), 1, 1_000_000)
            : 0);

    /// <summary>"mm:ss" (o "h:mm:ss" desde una hora).</summary>
    private static string Clock(long micros)
    {
        long s = (micros + 999_999) / 1_000_000;
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
    }

    // ================= Utilidades =================

    private void HideForMacro()
    {
        if (!IsVisible) return;
        _hiddenForMacro = true;
        Hide();
    }

    private void ShowAfterMacro()
    {
        if (!_hiddenForMacro) return;
        _hiddenForMacro = false;
        ShowFromTray();
    }

    /// <summary>Marca/desmarca un botón; con raise=false sin pasar por sus manejadores.</summary>
    private void SetChecked(ToggleButton button, bool value, bool raise = false)
    {
        if (raise) { button.IsChecked = value; return; }
        _macroBusy = true;
        button.IsChecked = value;
        _macroBusy = false;
    }

    private void DisposeMacros()
    {
        foreach (var (_, manager) in _macroHotkeys) manager.Dispose();
        _macroHotkeys.Clear();
        if (_macroRecording) MacroRecorder.Stop();
        MacroRecorder.Changed -= UpdateMacroBar;
        _macroBar?.Close();
        _player.Dispose();
    }
}
