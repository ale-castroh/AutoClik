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
        if (e.OriginalSource == StartButton || e.OriginalSource == SettingsButton || e.OriginalSource == HotkeyButton) return;
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
        (p.HotkeyHoldMode ? HoldModeRadio : ToggleModeRadio).IsChecked = true;

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
        WindowLeft = double.IsNaN(Left) ? _data.Prefs.WindowLeft : Left,
        WindowTop = double.IsNaN(Top) ? _data.Prefs.WindowTop : Top,
    };

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

        (p.Button switch
        {
            Engine.MouseButtonKind.Right => RightButtonRadio,
            Engine.MouseButtonKind.Middle => MiddleButtonRadio,
            _ => LeftButtonRadio,
        }).IsChecked = true;

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

        // Primero los puntos, luego el modo (que decide si se ven).
        _markers.Load((p.Points ?? []).Select(d => new MarkerPoint
        {
            X = d.X, Y = d.Y, Button = d.Button, Kind = d.Kind, DelayMs = d.DelayMs, HoldMs = d.HoldMs,
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
        }).ToList(),
        Speed = SelectedSpeed,
        JitterPercent = (int)Number(JitterBox),
        StartDelaySeconds = (int)Number(DelayBox),
        LockToWindow = LockWindowSwitch.IsChecked == true,
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
