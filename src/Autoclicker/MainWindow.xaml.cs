using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Autoclicker.Engine;
using Autoclicker.Input;
using Autoclicker.Interop;
using Autoclicker.Markers;
using Autoclicker.Theme;
using Autoclicker.Views;
using static Autoclicker.Interop.NativeMethods;

using Autoclicker.Localization;

namespace Autoclicker;

public partial class MainWindow : Window
{
    private readonly ClickEngine _engine = new();
    private readonly DispatcherTimer _statusTimer;
    private readonly Queue<(long Time, long Count)> _rateSamples = new();
    private readonly MarkerManager _markers = new();
    private readonly AntiAfk _antiAfk = new();
    private bool _picking; // eligiendo un punto en pantalla (ventana oculta)
    private string? _notice; // aviso puntual en el estado (p. ej. "Añade al menos un punto")

    private HotkeyManager? _hotkeys;        // iniciar/detener clics (F6)
    private HotkeyManager? _recordHotkeys;  // grabar macro (F7)
    private HotkeyManager? _playHotkeys;    // reproducir macro (F8)
    private TrayIcon? _tray;
    private nint _hwnd;

    private bool _themeRefreshQueued;
    private ToggleButton? _captureButton; // atajo que se está eligiendo ("Pulsa una tecla…")
    private bool _capturingHotkey => _captureButton != null;
    private bool _exiting;
    private string? _hotkeyError;
    private long _runStarted;
    private TimeSpan _runDelay;
    private StopReason? _lastStop;

    public MainWindow()
    {
        InitializeComponent();
        ThemeManager.ThemeChanged += OnThemeChanged;

        _engine.Stopped += reason => Dispatcher.InvokeAsync(() => OnEngineStopped(reason));
        _markers.Changed += UpdatePointsSummary;

        // La UI se refresca 10 veces por segundo, no en cada clic: no frena al motor.
        _statusTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        int ticks = 0;
        _statusTimer.Tick += (_, _) =>
        {
            UpdateStatus();
            // Una vez por segundo, que los marcadores sigan visibles aunque otra ventana
            // "siempre visible" se haya puesto encima (son traspasables, no molestan).
            if (++ticks % 10 == 0 && _markers.Visible) _markers.BringToFront();
        };

        foreach (TextBox box in NumberBoxes())
        {
            // X e Y admiten un "-" inicial: los monitores a la izquierda o encima del principal
            // tienen coordenadas negativas.
            bool signed = box == XBox || box == YBox;
            box.PreviewTextInput += (_, e) => e.Handled = !AcceptsInput(box, e.Text, signed);
            DataObject.AddPastingHandler(box, (_, e) =>
            {
                if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !AcceptsInput(box, text, signed))
                    e.CancelCommand();
            });
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
            box.LostKeyboardFocus += (_, _) => { if (box.Text.Length == 0 || box.Text == "-") box.Text = "0"; };
        }

        EmergencyCornerWatcher.Triggered += OnEmergencyCorner;

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        AboutVersionText.Text = Strings.F("AboutVersion", $"{version.Major}.{version.Minor}.{version.Build}");

        // El anti-AFK espera mientras se clica, se graba o se elige un punto.
        _antiAfk.ShouldWait = () => StartButton.IsChecked == true || _recording || _picking || MacroActive;
        _antiAfk.StatusChanged += () =>
        {
            AntiAfkStatusText.Text = _antiAfk.Status;
            AfkCountdownText.Text = _antiAfk.Countdown;
            AfkDetailText.Text = _antiAfk.Detail;
        };

        InitPersistence();
        InitMacros();
    }

    private IEnumerable<TextBox> NumberBoxes() =>
    [
        IntervalBox, HoldBox, RepeatCountBox, DurationBox, XBox, YBox, OffsetBox, JitterBox, DelayBox, AfkMinutesBox,
        MacroCountBox, MacroMinutesBox,
    ];

    // ================= Secciones (barra lateral) =================

    private bool _navigating;

    /// <summary>Secciones en el orden de la barra lateral, con su botón.</summary>
    private (FrameworkElement Page, ToggleButton Button)[] Sections =>
    [
        (MainPage, NavClicksButton),
        (MacrosPage, NavMacrosButton),
        (AntiAfkPage, NavAntiAfkButton),
        (SettingsPage, SettingsButton),
    ];

    private FrameworkElement CurrentPage => Sections.First(s => s.Page.Visibility == Visibility.Visible).Page;

    // Mientras se carga la ventana, "Clics" ya llega marcado antes de que existan las secciones.
    private bool SectionsReady => MainPage != null && MacrosPage != null && AntiAfkPage != null && SettingsPage != null;

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_navigating || !SectionsReady) return;
        ShowPage(Sections.First(s => s.Button == sender).Page);
    }

    /// <summary>
    /// Pulsar la sección en la que ya estás: Ajustes vuelve a Clics (como antes el engranaje);
    /// las demás se quedan donde están.
    /// </summary>
    private void Nav_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_navigating || !SectionsReady) return;
        var page = Sections.First(s => s.Button == sender).Page;
        if (page != CurrentPage) return;
        if (page == SettingsPage) ShowPage(MainPage);
        else Dispatcher.InvokeAsync(() => ((ToggleButton)sender).IsChecked = true);
    }

    private void ShowPage(FrameworkElement incoming)
    {
        if (_capturingHotkey) EndHotkeyCapture(null);

        // Marcar solo el botón de esa sección.
        _navigating = true;
        foreach (var (page, button) in Sections) button.IsChecked = page == incoming;
        _navigating = false;

        FrameworkElement outgoing = CurrentPage;
        if (incoming == outgoing) return;

        // Todas las secciones miden al menos lo que Clics (la ventana no salta de tamaño);
        // Ajustes, si no cabe, se desplaza.
        if (outgoing == MainPage)
        {
            double height = MainPage.ActualHeight;
            AntiAfkPage.MinHeight = height;
            MacrosPage.MinHeight = height;
            SettingsScroll.MaxHeight = Math.Max(height, 400);
        }

        int from = Array.FindIndex(Sections, s => s.Page == outgoing);
        int to = Array.FindIndex(Sections, s => s.Page == incoming);
        outgoing.Visibility = Visibility.Collapsed;
        incoming.Visibility = Visibility.Visible;

        // Entrada suave: fundido + desplazamiento corto desde el lado de la barra del que viene.
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(180);
        incoming.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        ((System.Windows.Media.TranslateTransform)incoming.RenderTransform).BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(to > from ? 14 : -14, 0, duration) { EasingFunction = ease });
    }

    private void SetOptionsEnabled(bool enabled)
    {
        OptionsCard.IsEnabled = enabled;
        HotkeyCard.IsEnabled = enabled;
        ClicksCard.IsEnabled = enabled;
    }

    // ================= Ciclo de vida de la ventana =================

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowBackdrop.Attach(this);
        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProc);

        _hotkeys = new HotkeyManager(_hwnd);
        _hotkeys.Pressed += OnHotkeyPressed;
        _hotkeys.Released += OnHotkeyReleased;
        var savedHotkey = new Hotkey(_data.Prefs.HotkeyVk, _data.Prefs.HotkeyModifiers);
        if (!_hotkeys.TrySet(savedHotkey))
            _hotkeyError = Strings.F("HotkeyTakenPick", savedHotkey);

        _recordHotkeys = new HotkeyManager(_hwnd, id: 3);
        _recordHotkeys.Pressed += OnRecordHotkey;
        var recordKey = new Hotkey(_data.Prefs.MacroRecordVk, _data.Prefs.MacroRecordModifiers);
        if (!_recordHotkeys.TrySet(recordKey)) _hotkeyError ??= Strings.F("HotkeyTakenPick", recordKey);

        _playHotkeys = new HotkeyManager(_hwnd, id: 4);
        _playHotkeys.Pressed += OnPlayHotkey;
        var playKey = new Hotkey(_data.Prefs.MacroPlayVk, _data.Prefs.MacroPlayModifiers);
        if (!_playHotkeys.TrySet(playKey)) _hotkeyError ??= Strings.F("HotkeyTakenPick", playKey);

        _tray = new TrayIcon(_hwnd);
        _tray.LeftClick += ToggleWindowFromTray;
        _tray.RightClick += ShowTrayMenu;

        UpdateIcons();
        RefreshHotkeyUi();
        UpdateStatus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && CloseToTraySwitch.IsChecked == true)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        SaveNow();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_recording) ClickRecorder.Stop();
        _recordingBar?.Close();
        _engine.Dispose();
        _antiAfk.Enabled = false;
        EmergencyCornerWatcher.Stop();
        EmergencyCornerWatcher.Triggered -= OnEmergencyCorner;
        _markers.Changed -= UpdatePointsSummary;
        _markers.Clear();
        DisposeMacros();
        _hotkeys?.Dispose();
        _recordHotkeys?.Dispose();
        _playHotkeys?.Dispose();
        _tray?.Dispose();
        ThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // Minimizado desde la barra de tareas (Win+D, clic en el icono...).
        if (WindowState == WindowState.Minimized && MinimizeToTraySwitch.IsChecked == true)
            HideToTray();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (_capturingHotkey) EndHotkeyCapture(null);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam == EscHotkeyId)
        {
            StopRecording();
            handled = true;
            return 0;
        }

        if (_hotkeys?.HandleMessage(msg, wParam) == true || _recordHotkeys?.HandleMessage(msg, wParam) == true
            || _playHotkeys?.HandleMessage(msg, wParam) == true || _tray?.HandleMessage(msg, lParam) == true)
        {
            handled = true;
            return 0;
        }

        // Windows avisa al cambiar el modo claro/oscuro ("ImmersiveColorSet") o el color de acento.
        if (msg == WM_DWMCOLORIZATIONCOLORCHANGED ||
            (msg == WM_SETTINGCHANGE && lParam != 0 && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet"))
        {
            QueueThemeRefresh();
        }
        return 0;
    }

    /// <summary>Windows envía varios mensajes seguidos; se agrupan en una sola actualización.</summary>
    private void QueueThemeRefresh()
    {
        if (_themeRefreshQueued) return;
        _themeRefreshQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            _themeRefreshQueued = false;
            ThemeManager.Apply();
        }, DispatcherPriority.Background);
    }

    private void OnThemeChanged()
    {
        WindowBackdrop.ApplyTheme(this);
        ApplyCardBrush();
        UpdateIcons();
    }

    /// <summary>
    /// Con fondo translúcido, las tarjetas usan un fondo más opaco (del tema actual) para que
    /// el menú no se pierda sobre lo que hay detrás; sin él, el normal.
    /// </summary>
    private void ApplyCardBrush()
    {
        Resources.Remove("CardBackgroundBrush");
        if (WindowBackdrop.Translucent && WindowBackdrop.MicaSupported
            && Application.Current.TryFindResource("CardTranslucentBrush") is System.Windows.Media.Brush brush)
            Resources["CardBackgroundBrush"] = brush;
    }

    private void UpdateIcons()
    {
        bool running = StartButton.IsChecked == true || _player.IsPlaying;
        Icon = IconFactory.Render(64, active: false);
        _tray?.Update(running, Strings.T(running ? "TrayRunning" : "TrayStopped"));
    }

    // ================= Barra de título y bandeja =================

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (MinimizeToTraySwitch.IsChecked == true) HideToTray();
        else WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void HideToTray()
    {
        Hide();
        WindowState = WindowState.Normal; // al volver, que aparezca normal

        // En la bandeja la app apenas hace nada: devolver al sistema la memoria que no usa.
        // Si está clicando no se toca nada para no meter pausas en el motor.
        if (StartButton.IsChecked != true && !_recording)
            Dispatcher.InvokeAsync(TrimMemory, DispatcherPriority.ApplicationIdle);
    }

    private static void TrimMemory()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// Otra copia de AutoClik pidió mostrarse: aparecer (aunque esté en la bandeja) y ponerse
    /// delante de todas las ventanas. No se hace nada mientras se graba o hay un selector abierto.
    /// </summary>
    public void BringToFront()
    {
        if (_recording || !IsEnabled) return;

        ShowFromTray();
        // Subir encima de todo y, si no está fijada, volver a ser una ventana normal.
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        if (PinButton.IsChecked != true)
            SetWindowPos(_hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        SetForegroundWindow(_hwnd);
        Activate();
    }

    private void ToggleWindowFromTray()
    {
        if (IsVisible) HideToTray();
        else ShowFromTray();
    }

    private void ShowTrayMenu()
    {
        var menu = (ContextMenu)FindResource("TrayMenu");
        var startItem = (MenuItem)menu.Items[0];
        bool running = StartButton.IsChecked == true;
        startItem.Header = Strings.T(running ? "TrayStop" : "TrayStart");
        startItem.Icon = running ? "" : "";

        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        // Sin esto el menú no se cierra al hacer clic fuera.
        if (PresentationSource.FromVisual(menu) is HwndSource source) SetForegroundWindow(source.Handle);
    }

    private void TrayStart_Click(object sender, RoutedEventArgs e) => StartButton.IsChecked = StartButton.IsChecked != true;

    private void TrayShow_Click(object sender, RoutedEventArgs e) => ShowFromTray();

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _exiting = true;
        Close();
    }

    // ================= Motor =================

    private void StartButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_capturingHotkey) EndHotkeyCapture(null);
        _notice = null;

        if (_recording)
        {
            Dispatcher.InvokeAsync(() => StartButton.IsChecked = false);
            return;
        }

        if (_player.IsPlaying) PlayMacroButton.IsChecked = false; // un solo "piloto automático" a la vez
        if (_macroRecording)
        {
            Dispatcher.InvokeAsync(() => StartButton.IsChecked = false);
            return;
        }

        if (PointsPositionRadio.IsChecked == true && _markers.Count == 0)
        {
            _notice = Strings.T("NeedPoint");
            Dispatcher.InvokeAsync(() => StartButton.IsChecked = false);
            return;
        }

        ClickSettings settings = ReadSettings();
        _markers.ClickThrough = settings.Points != null;
        _runStarted = Stopwatch.GetTimestamp();
        _runDelay = settings.StartDelay;
        _lastStop = null;
        _rateSamples.Clear();

        _engine.Start(settings);
        if (EmergencySwitch.IsChecked == true) EmergencyCornerWatcher.Start();
        SetOptionsEnabled(false);
        _statusTimer.Start();
        UpdateStatus();
        UpdateIcons();
    }

    private void StartButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _engine.Stop();
        EmergencyCornerWatcher.Stop();
        _markers.ClickThrough = false;
        _lastStop ??= StopReason.Requested;
        SetOptionsEnabled(true);
        _statusTimer.Stop();
        UpdateStatus();
        UpdateIcons();
    }

    /// <summary>Parada de emergencia: el usuario llevó el ratón a una esquina.</summary>
    private void OnEmergencyCorner()
    {
        if (PlayMacroButton.IsChecked == true)
        {
            _macroNotice = Strings.T("StoppedCorner");
            PlayMacroButton.IsChecked = false;
        }
        if (StartButton.IsChecked != true) return;
        StartButton.IsChecked = false;
        _notice = Strings.T("StoppedCorner");
        UpdateStatus();
    }

    /// <summary>El motor terminó por sí solo (nº de clics o tiempo alcanzado, o error).</summary>
    private void OnEngineStopped(StopReason reason)
    {
        if (reason == StopReason.Error && _engine.LastError is { } error) ErrorLog.Write("motor", error);
        if (StartButton.IsChecked != true) return;
        _lastStop = reason;
        StartButton.IsChecked = false;
    }

    private ClickSettings ReadSettings()
    {
        long unitMs = UnitHRadio.IsChecked == true ? 3_600_000
                    : UnitMinRadio.IsChecked == true ? 60_000
                    : UnitSRadio.IsChecked == true ? 1_000
                    : 1;
        long intervalMs = Number(IntervalBox) * unitMs;

        return new ClickSettings
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 1)),
            JitterPercent = Math.Min(Number(JitterBox), 90),
            Button = RightButtonRadio.IsChecked == true ? MouseButtonKind.Right
                   : MiddleButtonRadio.IsChecked == true ? MouseButtonKind.Middle
                   : MouseButtonKind.Left,
            Kind = DoubleKindRadio.IsChecked == true ? ClickKind.Double
                 : HoldKindRadio.IsChecked == true ? ClickKind.Hold
                 : ClickKind.Single,
            HoldDuration = TimeSpan.FromMilliseconds(Math.Max(Number(HoldBox), 1)),
            RepeatCount = RepeatCountRadio.IsChecked == true ? (int)Compat.Clamp(Number(RepeatCountBox), 1, int.MaxValue) : 0,
            TimeLimit = RepeatTimeRadio.IsChecked == true ? TimeSpan.FromSeconds(Math.Max(Number(DurationBox), 1)) : TimeSpan.Zero,
            StartDelay = TimeSpan.FromSeconds(Number(DelayBox)),
            UseFixedPosition = FixedPositionRadio.IsChecked == true,
            Points = PointsPositionRadio.IsChecked == true ? _markers.Snapshot() : null,
            Speed = PointsPositionRadio.IsChecked == true ? SelectedSpeed : 1,
            X = (int)Number(XBox),
            Y = (int)Number(YBox),
            RandomOffsetPx = (int)Number(OffsetBox),
            LockToForegroundWindow = LockWindowSwitch.IsChecked == true,
            RestoreCursor = RestoreCursorSwitch.IsChecked == true,
        };
    }

    private void UpdateStatus()
    {
        long now = Stopwatch.GetTimestamp();
        long count = _engine.ClickCount;
        bool running = StartButton.IsChecked == true;

        // Clics por segundo reales, medidos sobre el último segundo.
        double rate = 0;
        if (running)
        {
            _rateSamples.Enqueue((now, count));
            while (_rateSamples.Count > 1 && Compat.Elapsed(_rateSamples.Peek().Time, now).TotalSeconds > 1.0)
                _rateSamples.Dequeue();
            var oldest = _rateSamples.Peek();
            double span = Compat.Elapsed(oldest.Time, now).TotalSeconds;
            if (span > 0.2) rate = (count - oldest.Count) / span;
        }

        CountText.Text = count.ToString("N0");
        RateText.Text = running ? Strings.F("RateRunning", Strings.Clicks(count), rate) : Strings.Clicks(count);

        double waitLeft = (_runDelay - Compat.Elapsed(_runStarted, now)).TotalSeconds;
        StatusText.Text = running
            ? waitLeft > 0 ? Strings.F("StartsIn", waitLeft)
              : _engine.IsWaitingForWindow ? Strings.T("WaitingWindow")
              : _engine.IsPaused ? Strings.T("PausedWindow")
              : Strings.T("Clicking")
            : _notice ?? Strings.T(_lastStop switch
            {
                StopReason.Completed => "Completed",
                StopReason.Error => "ClickError",
                _ => "Stopped",
            });

        // La tecla va dentro del botón; debajo solo se explica el modo "Mantener".
        string key = _hotkeys?.Current.ToString() ?? "F6";
        Theme.Ui.SetKeyText(StartButton, key);
        bool hold = HoldModeRadio.IsChecked == true;
        HotkeyHintText.Text = hold ? Strings.F("HoldHint", key) : "";
        HotkeyHintText.Visibility = hold ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetHelpText(StartButton,
            hold ? Strings.F("HoldHint", key) : Strings.F(running ? "KeyToStop" : "KeyToStart", key));
    }

    // ================= Atajo =================

    private void OnHotkeyPressed()
    {
        if (_recording)
        {
            StopRecording();
            return;
        }
        if (HoldModeRadio.IsChecked == true) StartButton.IsChecked = true;
        else StartButton.IsChecked = StartButton.IsChecked != true;
    }

    private void OnHotkeyReleased()
    {
        if (HoldModeRadio.IsChecked == true) StartButton.IsChecked = false;
    }

    // Checked/Unchecked (y no Click) para que funcione igual con ratón, teclado o lectores de pantalla.
    // Los tres atajos (clics, grabar macro, reproducir macro) comparten esta captura.
    private void AnyHotkeyButton_Checked(object sender, RoutedEventArgs e)
    {
        var button = (ToggleButton)sender;
        if (_captureButton == button) return;
        if (_captureButton != null) EndHotkeyCapture(null);
        BeginHotkeyCapture(button);
    }

    private void AnyHotkeyButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_captureButton == sender) EndHotkeyCapture(null);
    }

    private HotkeyManager? ManagerFor(ToggleButton? button) =>
        button == RecordHotkeyButton ? _recordHotkeys : button == PlayHotkeyButton ? _playHotkeys : _hotkeys;

    private IEnumerable<HotkeyManager> AllHotkeys =>
        new[] { _hotkeys, _recordHotkeys, _playHotkeys }.Where(h => h != null)!;

    private void BeginHotkeyCapture(ToggleButton button)
    {
        _captureButton = button;
        foreach (var h in AllHotkeys) h.Suspended = true; // que ningún atajo salte mientras eliges
        button.IsChecked = true;
        button.Content = Strings.T("PressKey");
        HotkeyNoteText.Text = Strings.T("PressKeyHint");
    }

    private void EndHotkeyCapture(Hotkey? chosen)
    {
        ToggleButton? button = _captureButton;
        HotkeyManager? manager = ManagerFor(button);
        _captureButton = null;
        if (button != null) button.IsChecked = false;
        foreach (var h in AllHotkeys) h.Suspended = false;
        if (manager == null) return;

        if (chosen is { } hotkey && hotkey != manager.Current)
        {
            Hotkey previous = manager.Current;
            bool usedHere = AllHotkeys.Any(h => h != manager && h.Current == hotkey);
            if (!usedHere && manager.TrySet(hotkey))
            {
                _hotkeyError = null;
            }
            else
            {
                manager.TrySet(previous);
                _hotkeyError = Strings.F("HotkeyTaken", hotkey);
            }
        }
        RefreshHotkeyUi();
        UpdateStatus();
        ScheduleSave();
    }

    private void RefreshHotkeyUi()
    {
        HotkeyButton.Content = _hotkeys?.Current.ToString() ?? "F6";
        RecordHotkeyButton.Content = _recordHotkeys?.Current.ToString() ?? "F7";
        PlayHotkeyButton.Content = _playHotkeys?.Current.ToString() ?? "F8";
        Theme.Ui.SetKeyText(RecordMacroButton, RecordHotkeyButton.Content.ToString()!);
        Theme.Ui.SetKeyText(PlayMacroButton, PlayHotkeyButton.Content.ToString()!);
        HotkeyNoteText.Text = _hotkeyError ?? Strings.T("HotkeyWorksHidden");
        HotkeyNoteText.Foreground = (System.Windows.Media.Brush)FindResource(_hotkeyError != null ? "AccentBrush" : "TextSecondaryBrush");
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_capturingHotkey)
        {
            if (e.Key == Key.Escape && !MainPage.IsVisible)
            {
                e.Handled = true;
                ShowPage(MainPage);
                return;
            }
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            EndHotkeyCapture(null);
            return;
        }

        // Solo modificadores: esperar a la tecla principal.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
            or Key.RightShift or Key.LWin or Key.RWin or Key.None or Key.ImeProcessed or Key.DeadCharProcessed)
            return;

        ModifierKeys mods = Keyboard.Modifiers;
        uint modifiers = 0;
        if (mods.HasFlag(ModifierKeys.Control)) modifiers |= MOD_CONTROL;
        if (mods.HasFlag(ModifierKeys.Alt)) modifiers |= MOD_ALT;
        if (mods.HasFlag(ModifierKeys.Shift)) modifiers |= MOD_SHIFT;
        if (mods.HasFlag(ModifierKeys.Windows)) modifiers |= MOD_WIN;

        EndHotkeyCapture(new Hotkey(KeyInterop.VirtualKeyFromKey(key), modifiers));
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        if (_capturingHotkey && e.ChangedButton is MouseButton.XButton1 or MouseButton.XButton2)
        {
            e.Handled = true;
            EndHotkeyCapture(new Hotkey(e.ChangedButton == MouseButton.XButton1 ? VK_XBUTTON1 : VK_XBUTTON2, 0));
            return;
        }
        base.OnPreviewMouseDown(e);
    }

    // ================= Posición =================

    private void PickPosition_Click(object sender, RoutedEventArgs e)
    {
        // Se oculta la ventana para poder elegir un punto que esté debajo de ella.
        Hide();
        _picking = true;
        var point = PositionPicker.Pick(null);
        _picking = false;
        ShowFromTray();

        if (point is { } p)
        {
            XBox.Text = p.X.ToString();
            YBox.Text = p.Y.ToString();
        }
    }

    // ================= Anti-AFK (experimental) =================

    private void AntiAfk_Changed(object sender, RoutedEventArgs e) => _antiAfk.Enabled = AntiAfkSwitch.IsChecked == true;

    private void AfkMinutes_Changed(object sender, TextChangedEventArgs e) => _antiAfk.SetMinutes((int)Number(AfkMinutesBox));

    private void AfkTest_Click(object sender, RoutedEventArgs e) => _antiAfk.PulseNow();

    /// <summary>
    /// Funciones experimentales: la barra muestra u oculta el Anti-AFK (por enlace en el XAML).
    /// Al apagarlas se detiene el anti-AFK y, si estabas en su sección, se vuelve a Clics.
    /// </summary>
    private void Experimental_Changed(object sender, RoutedEventArgs e)
    {
        if (ExperimentalSwitch.IsChecked == true) return;
        AntiAfkSwitch.IsChecked = false;
        if (SectionsReady && CurrentPage == AntiAfkPage) ShowPage(MainPage);
    }

    // ================= Ajustes: fondo y "Acerca de" =================

    private void Translucent_Changed(object sender, RoutedEventArgs e)
    {
        WindowBackdrop.SetTranslucent(this, TranslucentSwitch.IsChecked == true);
        ApplyCardBrush();
    }

    /// <summary>Abre la carpeta de ajustes (settings.json y error.log) en el Explorador.</summary>
    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        string folder = Path.GetDirectoryName(ErrorLog.FilePath)!;
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", "\"" + folder + "\"");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ErrorLog.Write("abrir carpeta", ex);
        }
    }

    /// <summary>Elegir la ventana y el punto de clic del anti-AFK con la cruz.</summary>
    private void AfkPick_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _picking = true;
        var picked = PositionPicker.Pick(null);
        _picking = false;
        ShowFromTray();
        if (picked is not { } screen) return;

        var pt = new POINT { X = screen.X, Y = screen.Y };
        nint hwnd = GetAncestor(WindowFromPoint(pt), GA_ROOT);
        if (hwnd == 0) return;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Compat.ProcessId) return; // no tiene sentido sobre la propia app

        string processName;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            processName = process.ProcessName;
        }
        catch (ArgumentException)
        {
            return; // el programa se cerró mientras tanto
        }

        ScreenToClient(hwnd, ref pt);
        _antiAfk.SetTarget(processName, WindowTitle(hwnd), pt.X, pt.Y);
        RefreshAfkTarget();
        ScheduleSave();
    }

    private static string WindowTitle(nint hwnd)
    {
        var text = new System.Text.StringBuilder(256);
        GetWindowTextW(hwnd, text, text.Capacity);
        return text.ToString().Trim();
    }

    private void RefreshAfkTarget()
    {
        AfkTargetText.Text = _antiAfk.IsConfigured
            ? Strings.F("AfkTargetValue", _antiAfk.DisplayName ?? "", _antiAfk.X, _antiAfk.Y)
            : Strings.T("AfkNoTarget");
        AfkTargetText.ToolTip = _antiAfk.ProcessName;
    }

    // ================= Multi-punto =================

    private void PointsMode_Changed(object sender, RoutedEventArgs e)
    {
        bool on = PointsPositionRadio.IsChecked == true;
        _markers.Visible = on;
        RepeatUnitText.Text = Strings.T(on ? "UnitLaps" : "UnitClicks");
        UpdatePointsSummary();
    }

    private void AddPoints_Click(object sender, RoutedEventArgs e)
    {
        // Se oculta la ventana para poder marcar puntos que estén debajo de ella.
        Hide();
        _picking = true;
        PositionPicker.PickMany((x, y) => _markers.Add(x, y));
        _picking = false;
        ShowFromTray();
        _markers.BringToFront();
    }

    private void ClearPoints_Click(object sender, RoutedEventArgs e) => _markers.Clear();

    // ================= Grabadora =================

    private const int EscHotkeyId = 2;
    private const int VK_ESCAPE = 0x1B;
    private bool _recording;
    private RecordingBar? _recordingBar;

    private void RecordPoints_Click(object sender, RoutedEventArgs e) => StartRecording();

    private void StartRecording()
    {
        if (_recording) return;
        StartButton.IsChecked = false;
        _recording = true;

        Hide();
        _markers.Visible = false;

        // Esc termina la grabación (solo mientras se graba; luego se libera).
        bool escOk = RegisterHotKey(_hwnd, EscHotkeyId, MOD_NOREPEAT, VK_ESCAPE) != 0;
        string key = _hotkeys?.Current.ToString() ?? "F6";

        ClickRecorder.ClickRecorded += OnClickRecorded;
        ClickRecorder.Start();

        _recordingBar = new RecordingBar(escOk ? Strings.F("EscOr", key) : key);
        _recordingBar.StopRequested += StopRecording;
        _recordingBar.Show();
    }

    private void OnClickRecorded() => _recordingBar?.SetCount(ClickRecorder.Count);

    private void StopRecording()
    {
        if (!_recording) return;
        _recording = false;

        List<MarkerPoint> points = ClickRecorder.Stop();
        ClickRecorder.ClickRecorded -= OnClickRecorded;
        UnregisterHotKey(_hwnd, EscHotkeyId);
        _recordingBar?.Close();
        _recordingBar = null;

        if (points.Count > 0)
        {
            _markers.Load(points);
            Speed1Radio.IsChecked = true;
            PointsPositionRadio.IsChecked = true;
        }
        PointsMode_Changed(this, new RoutedEventArgs()); // vuelve a mostrar los marcadores si toca
        if (points.Count > 0) _notice = Strings.F("Recorded", points.Count, Strings.Clicks(points.Count));

        ShowFromTray();
        _markers.BringToFront();
        UpdateStatus();
    }

    private double SelectedSpeed =>
        SpeedHalfRadio.IsChecked == true ? 0.5
        : Speed2Radio.IsChecked == true ? 2
        : Speed4Radio.IsChecked == true ? 4
        : 1;

    private void UpdatePointsSummary()
    {
        int n = _markers.Count;
        PointsSummaryText.Text = n switch
        {
            0 => Strings.T("NoPoints"),
            1 => Strings.T("OnePoint"),
            _ => Strings.F("NPoints", n),
        };
        ClearPointsButton.IsEnabled = n > 0;
        if (n > 0 && _notice != null && StartButton.IsChecked != true)
        {
            _notice = null;
            UpdateStatus();
        }
    }

    // ================= Campos numéricos =================

    private static long Number(TextBox box) => long.TryParse(box.Text, out long v) ? v : 0;

    /// <summary>¿Seguiría siendo un número válido el campo tras escribir o pegar <paramref name="input"/>?</summary>
    private static bool AcceptsInput(TextBox box, string input, bool signed)
    {
        string result = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, input);
        int start = signed && result.StartsWith("-", StringComparison.Ordinal) ? 1 : 0;
        for (int i = start; i < result.Length; i++)
            if (!Compat.IsDigit(result[i])) return false;
        return true;
    }
}
