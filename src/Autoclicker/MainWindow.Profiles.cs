using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Autoclicker.Markers;
using Autoclicker.Persistence;
using Autoclicker.Views;

using Autoclicker.Localization;

namespace Autoclicker;

/// <summary>Guardado automático y perfiles de la ventana principal.</summary>
public partial class MainWindow
{
    private readonly SettingsStore _store = new();
    private AppData _data = new();
    private DispatcherTimer? _saveTimer;
    private bool _applying; // true mientras se vuelcan datos a los controles (no guardar)

    private ProfileData CurrentProfileData => _data.Profiles[_data.CurrentProfile];

    // ================= Carga inicial y guardado =================

    /// <summary>Carga settings.json y lo aplica. Llamar tras InitializeComponent.</summary>
    private void InitPersistence()
    {
        _data = _store.Load();

        _applying = true;
        ApplyPrefs(_data.Prefs);
        ApplyProfile(CurrentProfileData);
        _applying = false;
        RefreshProfileUi();

        // Cualquier cambio en un control programa un guardado (agrupado cada 0,5 s).
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };

        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(OnAnyInputChanged));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(OnAnyInputChanged));
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => ScheduleSave()));
        _markers.Changed += ScheduleSave;
        LocationChanged += (_, _) => ScheduleSave();
    }

    private void OnAnyInputChanged(object sender, RoutedEventArgs e)
    {
        // Iniciar/Detener, abrir ajustes o capturar el atajo no son cambios de configuración.
        if (e.OriginalSource == StartButton || e.OriginalSource == HotkeyButton
            || e.OriginalSource == SettingsButton || e.OriginalSource == NavClicksButton || e.OriginalSource == NavAntiAfkButton
            || e.OriginalSource == NavMacrosButton || e.OriginalSource == RecordMacroButton || e.OriginalSource == PlayMacroButton
            || e.OriginalSource == RecordHotkeyButton || e.OriginalSource == PlayHotkeyButton
            || e.OriginalSource == NavWatchButton || e.OriginalSource == WatchToggle || e.OriginalSource == WatchHotkeyButton) return;
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        if (_applying || _saveTimer == null) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer?.Stop();
        _data.Profiles[_data.CurrentProfile] = CaptureProfile();
        _data.Prefs = CapturePrefs();
        _store.Save(_data);
    }

    // ================= Preferencias globales =================

    private void ApplyPrefs(GlobalPrefs p)
    {
        MinimizeToTraySwitch.IsChecked = p.MinimizeToTray;
        CloseToTraySwitch.IsChecked = p.CloseToTray;
        EmergencySwitch.IsChecked = p.EmergencyCorner;
        PinButton.IsChecked = p.Pinned;
        TranslucentSwitch.IsChecked = p.Translucent;
        HudSwitch.IsChecked = p.HudEnabled;
        (p.HotkeyHoldMode ? HoldModeRadio : ToggleModeRadio).IsChecked = true;

        // Anti-AFK: primero la ventana y los minutos, luego el interruptor (que lo arranca).
        _antiAfk.SetTarget(p.AntiAfkProcess, p.AntiAfkTitle, p.AntiAfkX, p.AntiAfkY);
        AfkMinutesBox.Text = Compat.Clamp(p.AntiAfkMinutes, 1, 18).ToString();
        _antiAfk.SetMinutes(p.AntiAfkMinutes);
        RefreshAfkTarget();
        _afkKeyVk = p.AntiAfkKeyVk > 0 ? p.AntiAfkKeyVk : 0x20;
        _antiAfk.SetKey(_afkKeyVk);
        AfkKeyButton.Content = Input.Hotkey.KeyName(_afkKeyVk);
        (p.AntiAfkUseKey ? AfkKeyRadio : AfkClickRadio).IsChecked = true;
        _antiAfk.SetUseKey(p.AntiAfkUseKey);
        // Macros
        (p.MacroRepeat switch
        {
            Macros.MacroRepeat.Count => MacroCountRadio,
            Macros.MacroRepeat.Forever => MacroForeverRadio,
            Macros.MacroRepeat.Time => MacroTimeRadio,
            _ => MacroOnceRadio,
        }).IsChecked = true;
        MacroCountBox.Text = Compat.Clamp(p.MacroCount, 1, 999_999).ToString();
        MacroMinutesBox.Text = Compat.Clamp(p.MacroMinutes, 1, 9_999).ToString();
        (p.MacroSpeed switch
        {
            0.5 => MacroSpeedHalf,
            1 => MacroSpeed1,
            2 => MacroSpeed2,
            4 => MacroSpeed4,
            _ => MacroSpeedCustom,
        }).IsChecked = true;
        MacroSpeedBox.Text = FormatSpeed(p.MacroSpeed is 0.5 or 1 or 2 or 4 ? 1.5 : Compat.Clamp(p.MacroSpeed, 0.1, 20));
        (p.MacroMode switch
        {
            Macros.MacroRecordMode.Mouse => MacroModeMouse,
            Macros.MacroRecordMode.Keys => MacroModeKeys,
            _ => MacroModeAll,
        }).IsChecked = true;

        ExperimentalSwitch.IsChecked = p.Experimental;
        AntiAfkSwitch.IsChecked = p.Experimental && p.AntiAfkEnabled;

        // "Iniciar con Windows" no va en settings.json: lo que manda es el registro.
        _syncingAutostart = true;
        StartWithWindowsSwitch.IsChecked = Interop.Autostart.IsEnabled;
        _syncingAutostart = false;
        Interop.Autostart.RefreshPath();

        // Posición de la ventana, solo si sigue dentro del escritorio (p. ej. se quitó un monitor).
        if (p.WindowLeft is double left && p.WindowTop is double top &&
            left >= SystemParameters.VirtualScreenLeft && top >= SystemParameters.VirtualScreenTop &&
            left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 &&
            top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
    }

    private GlobalPrefs CapturePrefs() => new()
    {
        HotkeyVk = _hotkeys?.Current.VirtualKey ?? _data.Prefs.HotkeyVk,
        HotkeyModifiers = _hotkeys?.Current.Modifiers ?? _data.Prefs.HotkeyModifiers,
        HotkeyHoldMode = HoldModeRadio.IsChecked == true,
        MinimizeToTray = MinimizeToTraySwitch.IsChecked == true,
        CloseToTray = CloseToTraySwitch.IsChecked == true,
        EmergencyCorner = EmergencySwitch.IsChecked == true,
        Pinned = PinButton.IsChecked == true,
        Translucent = TranslucentSwitch.IsChecked == true,
        HudEnabled = HudSwitch.IsChecked == true,
        HudLeft = _data.Prefs.HudLeft,
        HudTop = _data.Prefs.HudTop,
        WindowLeft = double.IsNaN(Left) ? _data.Prefs.WindowLeft : Left,
        WindowTop = double.IsNaN(Top) ? _data.Prefs.WindowTop : Top,
        Experimental = ExperimentalSwitch.IsChecked == true,
        MacroRecordVk = _recordHotkeys?.Current.VirtualKey ?? _data.Prefs.MacroRecordVk,
        MacroRecordModifiers = _recordHotkeys?.Current.Modifiers ?? _data.Prefs.MacroRecordModifiers,
        MacroPlayVk = _playHotkeys?.Current.VirtualKey ?? _data.Prefs.MacroPlayVk,
        MacroPlayModifiers = _playHotkeys?.Current.Modifiers ?? _data.Prefs.MacroPlayModifiers,
        WatchVk = _watchHotkeys?.Current.VirtualKey ?? _data.Prefs.WatchVk,
        WatchModifiers = _watchHotkeys?.Current.Modifiers ?? _data.Prefs.WatchModifiers,
        MacroName = _macro?.Name ?? _data.Prefs.MacroName,
        MacroRepeat = MacroCountRadio.IsChecked == true ? Macros.MacroRepeat.Count
                    : MacroForeverRadio.IsChecked == true ? Macros.MacroRepeat.Forever
                    : MacroTimeRadio.IsChecked == true ? Macros.MacroRepeat.Time
                    : Macros.MacroRepeat.Once,
        MacroCount = (int)Compat.Clamp(Number(MacroCountBox), 1, 999_999),
        MacroMinutes = (int)Compat.Clamp(Number(MacroMinutesBox), 1, 9_999),
        MacroSpeed = SelectedMacroSpeed,
        MacroMode = SelectedRecordMode,
        AntiAfkEnabled = AntiAfkSwitch.IsChecked == true,
        AntiAfkMinutes = (int)Compat.Clamp(Number(AfkMinutesBox), 1, 18),
        AntiAfkProcess = _antiAfk.ProcessName,
        AntiAfkTitle = _antiAfk.DisplayName,
        AntiAfkX = _antiAfk.X,
        AntiAfkY = _antiAfk.Y,
        AntiAfkUseKey = AfkKeyRadio.IsChecked == true,
        AntiAfkKeyVk = _afkKeyVk,
    };

    private bool _syncingAutostart;

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncingAutostart) return;
        bool wanted = StartWithWindowsSwitch.IsChecked == true;
        if (!Interop.Autostart.Set(wanted))
        {
            _syncingAutostart = true;
            StartWithWindowsSwitch.IsChecked = !wanted; // no se pudo: mostrar el estado real
            _syncingAutostart = false;
        }
    }

    // ================= Perfil <-> controles =================

    private void ApplyProfile(ProfileData p)
    {
        bool wasApplying = _applying;
        _applying = true;

        IntervalBox.Text = p.Interval.ToString();
        (p.Unit switch
        {
            IntervalUnit.Seconds => UnitSRadio,
            IntervalUnit.Minutes => UnitMinRadio,
            IntervalUnit.Hours => UnitHRadio,
            _ => UnitMsRadio,
        }).IsChecked = true;

        (p.Action switch
        {
            Engine.ClickAction.Key => KeyActionRadio,
            Engine.ClickAction.Wheel => WheelActionRadio,
            _ => p.Button switch
            {
                Engine.MouseButtonKind.Right => RightButtonRadio,
                Engine.MouseButtonKind.Middle => MiddleButtonRadio,
                _ => LeftButtonRadio,
            },
        }).IsChecked = true;
        _actionKeyVk = p.KeyVk > 0 ? p.KeyVk : 0x45;
        ActionKeyButton.Content = Input.Hotkey.KeyName(_actionKeyVk);
        (p.WheelUp ? WheelUpRadio : WheelDownRadio).IsChecked = true;
        WheelStepsBox.Text = Compat.Clamp(p.WheelSteps, 1, 20).ToString();

        (p.Kind switch
        {
            Engine.ClickKind.Double => DoubleKindRadio,
            Engine.ClickKind.Hold => HoldKindRadio,
            _ => SingleKindRadio,
        }).IsChecked = true;
        HoldBox.Text = p.HoldMs.ToString();

        (p.Repeat switch
        {
            RepeatMode.Count => RepeatCountRadio,
            RepeatMode.Time => RepeatTimeRadio,
            _ => RepeatForeverRadio,
        }).IsChecked = true;
        RepeatCountBox.Text = p.RepeatCount.ToString();
        DurationBox.Text = p.DurationSeconds.ToString();

        XBox.Text = p.X.ToString();
        YBox.Text = p.Y.ToString();
        OffsetBox.Text = p.OffsetPx.ToString();
        JitterBox.Text = p.JitterPercent.ToString();
        DelayBox.Text = p.StartDelaySeconds.ToString();
        LockWindowSwitch.IsChecked = p.LockToWindow;
        RestoreCursorSwitch.IsChecked = p.RestoreCursor;
        LoadWatchRules(p.WatchRules);

        // Primero los puntos, luego el modo (que decide si se ven).
        _markers.Load((p.Points ?? []).Select(d => new MarkerPoint
        {
            X = d.X, Y = d.Y, Button = d.Button, Kind = d.Kind, DelayMs = d.DelayMs, HoldMs = d.HoldMs,
            EndX = d.EndX, EndY = d.EndY,
        }));
        (p.Speed switch
        {
            <= 0.5 => SpeedHalfRadio,
            >= 4 => Speed4Radio,
            >= 2 => Speed2Radio,
            _ => Speed1Radio,
        }).IsChecked = true;
        (p.Position switch
        {
            PositionMode.Fixed => FixedPositionRadio,
            PositionMode.Points => PointsPositionRadio,
            _ => CursorPositionRadio,
        }).IsChecked = true;
        PointsMode_Changed(this, new RoutedEventArgs());

        _applying = wasApplying;
    }

    private ProfileData CaptureProfile() => new()
    {
        Interval = Number(IntervalBox),
        Unit = UnitSRadio.IsChecked == true ? IntervalUnit.Seconds
             : UnitMinRadio.IsChecked == true ? IntervalUnit.Minutes
             : UnitHRadio.IsChecked == true ? IntervalUnit.Hours
             : IntervalUnit.Milliseconds,
        Button = RightButtonRadio.IsChecked == true ? Engine.MouseButtonKind.Right
               : MiddleButtonRadio.IsChecked == true ? Engine.MouseButtonKind.Middle
               : Engine.MouseButtonKind.Left,
        Action = SelectedAction,
        KeyVk = _actionKeyVk,
        WheelUp = WheelUpRadio.IsChecked == true,
        WheelSteps = (int)Compat.Clamp(Number(WheelStepsBox), 1, 20),
        Kind = DoubleKindRadio.IsChecked == true ? Engine.ClickKind.Double
             : HoldKindRadio.IsChecked == true ? Engine.ClickKind.Hold
             : Engine.ClickKind.Single,
        HoldMs = (int)Number(HoldBox),
        Repeat = RepeatCountRadio.IsChecked == true ? RepeatMode.Count
               : RepeatTimeRadio.IsChecked == true ? RepeatMode.Time
               : RepeatMode.Forever,
        RepeatCount = (int)Math.Min(Number(RepeatCountBox), int.MaxValue),
        DurationSeconds = (int)Number(DurationBox),
        Position = FixedPositionRadio.IsChecked == true ? PositionMode.Fixed
                 : PointsPositionRadio.IsChecked == true ? PositionMode.Points
                 : PositionMode.Cursor,
        X = (int)Number(XBox),
        Y = (int)Number(YBox),
        OffsetPx = (int)Number(OffsetBox),
        Points = _markers.Points.Select(m => new PointData
        {
            X = m.X, Y = m.Y, Button = m.Button, Kind = m.Kind, DelayMs = m.DelayMs, HoldMs = m.HoldMs,
            EndX = m.EndX, EndY = m.EndY,
        }).ToList(),
        Speed = SelectedSpeed,
        JitterPercent = (int)Number(JitterBox),
        StartDelaySeconds = (int)Number(DelayBox),
        LockToWindow = LockWindowSwitch.IsChecked == true,
        RestoreCursor = RestoreCursorSwitch.IsChecked == true,
        WatchRules = CaptureWatchRules(),
    };

    // ================= Menú de perfiles =================

    private void RefreshProfileUi()
    {
        ProfileNameText.Text = _data.CurrentProfile;
        ProfileButton.ToolTip = Strings.F("ProfileTip", _data.CurrentProfile);
    }

    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ProfileButton, Placement = PlacementMode.Bottom };

        foreach (string name in _data.Profiles.Keys.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new MenuItem { Header = name, Icon = name == _data.CurrentProfile ? "" : null };
            item.Click += (_, _) => SwitchProfile(name);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuAction(Strings.T("NewProfile"), "", NewProfile));
        menu.Items.Add(MenuAction(Strings.T("Rename"), "", RenameProfile));
        var delete = MenuAction(Strings.T("DeleteProfile"), "", DeleteProfile);
        delete.IsEnabled = _data.Profiles.Count > 1;
        menu.Items.Add(delete);
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuAction(Strings.T("ProfileExport"), "", ExportProfile));
        menu.Items.Add(MenuAction(Strings.T("ProfileImport"), "", ImportProfile));
        menu.Items.Add(MenuAction(Strings.T("ProfileShortcut"), "", ProfileShortcut));

        menu.IsOpen = true;
    }

    private static MenuItem MenuAction(string header, string icon, Action action)
    {
        var item = new MenuItem { Header = header, Icon = icon };
        item.Click += (_, _) => action();
        return item;
    }

    private void SwitchProfile(string name)
    {
        if (name == _data.CurrentProfile) return;
        StartButton.IsChecked = false;
        SaveNow();
        _data.CurrentProfile = name;
        ApplyProfile(CurrentProfileData);
        SaveNow();
        RefreshProfileUi();
    }

    private void NewProfile()
    {
        string? name = NameDialog.AskName(this, Strings.T("NewProfileTitle"), SuggestName(), ValidateNew);
        if (name == null) return;

        StartButton.IsChecked = false;
        SaveNow();
        _data.Profiles[name] = CaptureProfile();
        _data.CurrentProfile = name;
        SaveNow();
        RefreshProfileUi();
    }

    private void RenameProfile()
    {
        string current = _data.CurrentProfile;
        string? name = NameDialog.AskName(this, Strings.T("RenameTitle"), current,
            n => n == current ? null : ValidateNew(n));
        if (name == null || name == current) return;

        SaveNow();
        _data.Profiles[name] = _data.Profiles[current];
        _data.Profiles.Remove(current);
        _data.CurrentProfile = name;
        SaveNow();
        RefreshProfileUi();
    }

    private void DeleteProfile()
    {
        if (_data.Profiles.Count <= 1) return;
        string current = _data.CurrentProfile;
        if (!NameDialog.Confirm(this, Strings.T("DeleteProfile"),
                Strings.F("DeleteConfirm", current), Strings.T("Delete")))
            return;

        StartButton.IsChecked = false;
        _data.Profiles.Remove(current);
        _data.CurrentProfile = _data.Profiles.Keys.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).First();
        ApplyProfile(CurrentProfileData);
        SaveNow();
        RefreshProfileUi();
    }

    // ================= Exportar / importar perfiles (2.6) =================

    private const string ProfileExtension = ".autoclik-profile";

    private void ExportProfile_Click(object sender, RoutedEventArgs e) => ExportProfile();

    private void ImportProfile_Click(object sender, RoutedEventArgs e) => ImportProfile();

    /// <summary>Guarda el perfil actual en un archivo, con las imágenes de sus reglas de Vigilar dentro.</summary>
    private void ExportProfile()
    {
        SaveNow();
        string name = _data.CurrentProfile;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = new string(name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()),
            DefaultExt = ProfileExtension,
            Filter = Strings.T("ProfileFileFilter"),
        };
        if (dialog.ShowDialog(this) != true) return;

        ProfileData profile = CurrentProfileData;
        var images = new Dictionary<string, object?>();
        foreach (string image in profile.WatchRules.Select(r => r.Image).OfType<string>().Distinct())
        {
            string path = Watch.WatchImages.PathOf(image);
            if (File.Exists(path)) images[image] = Convert.ToBase64String(File.ReadAllBytes(path));
        }
        var json = new Dictionary<string, object?>
        {
            ["Format"] = "AutoClik profile",
            ["Version"] = 1,
            ["Name"] = name,
            ["Profile"] = profile.ToJson(),
            ["Images"] = images,
        };
        try
        {
            File.WriteAllText(dialog.FileName, MiniJson.Serialize(json), new System.Text.UTF8Encoding(false));
            _notice = Strings.T("ProfileExported");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write("exportar perfil", ex);
            _notice = Strings.T("ProfileFileError");
        }
        UpdateStatus();
    }

    /// <summary>Añade un perfil desde un archivo (con otro nombre si ya existe) y pasa a él.</summary>
    private void ImportProfile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Strings.T("ProfileFileFilter") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var j = JsonObj.From(MiniJson.Parse(File.ReadAllText(dialog.FileName, System.Text.Encoding.UTF8)));
            if (j.Str("Format") != "AutoClik profile") throw new FormatException("No es un perfil de AutoClik");
            ProfileData profile = ProfileData.FromJson(j.Obj("Profile"));
            foreach (var kv in j.Obj("Images").Entries)
            {
                // Solo nombres de archivo simples: nada de rutas que salgan de la carpeta de imágenes
                if (kv.Value is not string data || kv.Key != System.IO.Path.GetFileName(kv.Key) || !kv.Key.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;
                string path = Watch.WatchImages.PathOf(kv.Key);
                if (File.Exists(path)) continue;
                Directory.CreateDirectory(Watch.WatchImages.Folder);
                File.WriteAllBytes(path, Convert.FromBase64String(data));
            }

            string wanted = (j.Str("Name") ?? "").Trim();
            if (wanted.Length == 0) wanted = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            if (wanted.Length > 40) wanted = wanted.Substring(0, 40);
            string name = wanted;
            for (int i = 2; ValidateNew(name) != null; i++) name = $"{wanted} ({i})";

            StartButton.IsChecked = false;
            SaveNow();
            _data.Profiles[name] = profile;
            _data.CurrentProfile = name;
            ApplyProfile(profile);
            SaveNow();
            RefreshProfileUi();
            _notice = Strings.F("ProfileImported", name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException
                                       or InvalidCastException or OverflowException or ArgumentException)
        {
            ErrorLog.Write("importar perfil", ex);
            _notice = Strings.T("ProfileFileError");
        }
        UpdateStatus();
    }

    private string? ValidateNew(string name) =>
        _data.Profiles.Keys.Any(k => string.Equals(k, name, StringComparison.CurrentCultureIgnoreCase))
            ? Strings.T("ProfileExists")
            : null;

    private string SuggestName()
    {
        for (int i = _data.Profiles.Count + 1; ; i++)
        {
            string candidate = Strings.F("ProfileN", i);
            if (ValidateNew(candidate) == null) return candidate;
        }
    }
}
