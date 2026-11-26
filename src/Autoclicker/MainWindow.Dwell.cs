using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Autoclicker.Input;
using Autoclicker.Localization;
using Autoclicker.Views;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker;

/// <summary>
/// Clic por permanencia (2.7, Ajustes → Accesibilidad): clica al dejar el ratón quieto. Se pausa mientras
/// AutoClik clica, graba o reproduce, o se elige un punto; la parada de emergencia (esquina) lo apaga.
/// </summary>
public partial class MainWindow
{
    private readonly DwellClicker _dwell = new();
    private DispatcherTimer? _dwellTimer;
    private DwellRing? _dwellRing;
    private HotkeyManager? _dwellHotkey;
    private Hotkey? _dwellKey;

    private void InitDwell()
    {
        _dwellTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(25) };
        _dwellTimer.Tick += (_, _) => DwellTick();
        foreach (TextBox box in new[] { DwellDelayBox, DwellToleranceBox })
        {
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
            box.LostKeyboardFocus += (_, _) => { ApplyDwellOptions(); RefreshDwellFields(); };
        }
        DwellDelayBox.PreviewTextInput += (_, e) =>
        {
            string result = DwellDelayBox.Text.Remove(DwellDelayBox.SelectionStart, DwellDelayBox.SelectionLength)
                .Insert(DwellDelayBox.SelectionStart, e.Text);
            e.Handled = !(result.All(c => Compat.IsDigit(c) || c is ',' or '.') && result.Count(c => c is ',' or '.') <= 1);
        };
        DwellToleranceBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(Compat.IsDigit);
        if (DwellSwitch.IsChecked == true) _dwellTimer.Start(); // ya venía encendido de settings.json
    }

    private void ApplyDwellPrefs(Persistence.GlobalPrefs p)
    {
        _dwell.DelayMs = p.DwellMs;
        _dwell.TolerancePx = p.DwellTolerance;
        _dwell.Click = p.DwellClick;
        (p.DwellClick switch { DwellClick.Right => DwellRightRadio, DwellClick.Double => DwellDoubleRadio, _ => DwellLeftRadio }).IsChecked = true;
        _dwellKey = p.DwellVk is int vk ? new Hotkey(vk, p.DwellModifiers) : null;
        RefreshDwellFields();
        (p.DwellRepeat ? DwellForeverRadio : DwellOnceRadio).IsChecked = true;
        DwellSwitch.IsChecked = p.DwellEnabled;
    }

    private void RefreshDwellFields()
    {
        DwellDelayBox.Text = (_dwell.DelayMs / 1000.0).ToString("0.0#", CultureInfo.CurrentCulture);
        DwellToleranceBox.Text = _dwell.TolerancePx.ToString(CultureInfo.InvariantCulture);
        DwellHotkeyButton.Content = _dwellKey?.ToString() ?? Strings.T("NoHotkey");
    }

    /// <summary>Lee los campos (segundos con coma o punto, de 0,3 a 5; temblor de 1 a 50 px).</summary>
    private void ApplyDwellOptions()
    {
        if (double.TryParse(DwellDelayBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double s) && s > 0)
            _dwell.DelayMs = (int)Math.Round(Compat.Clamp(s, 0.3, 5) * 1000);
        if (int.TryParse(DwellToleranceBox.Text, out int px)) _dwell.TolerancePx = (int)Compat.Clamp(px, 1, 50);
        _dwell.Click = DwellRightRadio.IsChecked == true ? DwellClick.Right
                     : DwellDoubleRadio.IsChecked == true ? DwellClick.Double
                     : DwellClick.Left;
        ScheduleSave();
    }

    private void DwellClick_Changed(object sender, RoutedEventArgs e)
    {
        if (DwellToleranceBox == null || _applying) return; // aún cargando la ventana o aplicando los ajustes
        ApplyDwellOptions();
    }

    private void Dwell_Changed(object sender, RoutedEventArgs e)
    {
        bool on = DwellSwitch.IsChecked == true;
        _dwell.Reset(); // al encenderlo, no clica hasta que se mueva el ratón
        if (on) _dwellTimer?.Start();
        else
        {
            _dwellTimer?.Stop();
            _dwellRing?.Hide();
            if (_dwellRunning) StartButton.IsChecked = false; // apagarlo para también lo que estuviera clicando
        }
        UpdateDwellTray();
    }

    /// <summary>Atajo del clic por permanencia, o la bandeja: encender o apagar.</summary>
    private void ToggleDwell() => DwellSwitch.IsChecked = DwellSwitch.IsChecked != true;

    private bool DwellPaused =>
        StartButton.IsChecked == true || _recording || _macroRecording || _player.IsPlaying || _picking || _capturingHotkey;

    // ================= Repetir: "Sin parar" (2.7) =================
    // Al completarse el círculo arranca el autoclicker en el cursor (intervalo, botón y tipo de Clics) y
    // se para en cuanto el ratón se mueve. "Una vez" (por defecto) hace un solo clic.

    private bool _dwellRunning;              // el autoclicker lo arrancó la permanencia
    private POINT _dwellRunAnchor;
    private Engine.ClickSettings? _dwellRunSettings;

    private bool DwellRepeat => DwellForeverRadio.IsChecked == true;

    private void StartDwellRun(POINT at)
    {
        _dwellRunAnchor = at;
        _dwellRunSettings = ReadSettings() with
        {
            Action = Engine.ClickAction.Mouse,
            UseFixedPosition = false,
            Points = null,
            Speed = 1,
            RepeatCount = 0,
            TimeLimit = TimeSpan.Zero,
            StartDelay = TimeSpan.Zero,
            LockToForegroundWindow = false,
            RestoreCursor = false,
            Button = _dwell.Click == DwellClick.Right ? Engine.MouseButtonKind.Right : Engine.MouseButtonKind.Left,
            Kind = _dwell.Click == DwellClick.Double ? Engine.ClickKind.Double : Engine.ClickKind.Single,
        };
        _dwellRunning = true;
        StartButton.IsChecked = true;
        if (StartButton.IsChecked != true) { _dwellRunning = false; _dwellRunSettings = null; }
    }

    /// <summary>Al parar el autoclicker (por mover el ratón, F6, la esquina...): la permanencia vuelve a esperar a que se mueva.</summary>
    private void OnDwellRunStopped()
    {
        if (!_dwellRunning) return;
        _dwellRunning = false;
        _dwellRunSettings = null;
        _dwell.Reset();
    }

    private void DwellTick()
    {
        GetCursorPos(out POINT cursor);
        if (_dwellRunning)
        {
            // Clicando sin parar: moverse más que el temblor lo para
            if (Math.Max(Math.Abs(cursor.X - _dwellRunAnchor.X), Math.Abs(cursor.Y - _dwellRunAnchor.Y)) > _dwell.TolerancePx)
                StartButton.IsChecked = false; // -> OnDwellRunStopped
            return;
        }
        if (DwellPaused)
        {
            _dwell.Reset();
            _dwellRing?.Hide();
            return;
        }
        // Parada de emergencia: llevar el ratón a una esquina lo apaga
        if (EmergencySwitch.IsChecked == true && EmergencyCornerWatcher.IsInCorner(cursor))
        {
            DwellSwitch.IsChecked = false;
            _notice = Strings.T("DwellStoppedCorner");
            UpdateStatus();
            return;
        }

        bool buttonDown = (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0
                          || (GetAsyncKeyState(0x04) & 0x8000) != 0;
        var engineCursor = new Engine.EngineNative.POINT { X = cursor.X, Y = cursor.Y };
        long now = Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
        bool click = _dwell.Update(engineCursor, now, buttonDown, out double? progress);

        if (progress is double p)
        {
            _dwellRing ??= new DwellRing();
            _dwellRing.ShowAt(cursor.X, cursor.Y, p);
        }
        else _dwellRing?.Hide();

        if (click)
        {
            _dwellRing?.Hide();
            if (DwellRepeat) StartDwellRun(cursor);
            else _dwell.SendClick();
        }
    }

    // ================= Atajo propio (por defecto ninguno) =================

    private void RebuildDwellHotkey()
    {
        _dwellHotkey?.Dispose();
        _dwellHotkey = null;
        if (_hwnd == 0 || _dwellKey is not { } key) return;
        _dwellHotkey = new HotkeyManager(_hwnd, id: 6);
        _dwellHotkey.Pressed += ToggleDwell;
        if (!_dwellHotkey.TrySet(key))
        {
            _dwellHotkey.Dispose();
            _dwellHotkey = null;
            _hotkeyError ??= Strings.F("HotkeyTakenPick", key);
        }
    }

    private void DwellHotkey_Click(object sender, RoutedEventArgs e)
    {
        var (accepted, key) = KeyDialog.Ask(this, Strings.T("DwellHotkeyTitle"), _dwellKey, k =>
            AllHotkeys.Any(h => h.Current == k) || _macros.Any(m => m.Hotkey == k) ? Strings.F("HotkeyTaken", k) : null);
        if (!accepted) return;
        _dwellKey = key;
        RebuildDwellHotkey();
        RefreshDwellFields();
        ScheduleSave();
    }

    private bool HandleDwellHotkeyMessage(int msg, nint wParam) => _dwellHotkey?.HandleMessage(msg, wParam) == true;

    // ================= Bandeja =================

    private void TrayDwell_Click(object sender, RoutedEventArgs e) => ToggleDwell();

    private void UpdateDwellTray()
    {
        if (TryFindResource("TrayMenu") is ContextMenu menu && menu.Items[1] is MenuItem item)
            item.IsChecked = DwellSwitch.IsChecked == true;
    }

    private void DisposeDwell()
    {
        _dwellTimer?.Stop();
        _dwellRing?.Close();
        _dwellHotkey?.Dispose();
    }
}
