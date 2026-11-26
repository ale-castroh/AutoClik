using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Autoclicker.Input;
using Autoclicker.Localization;
using Autoclicker.Watch;

namespace Autoclicker.Views;

/// <summary>
/// Crear o editar una regla de "Vigilar": qué vigilar (color en un punto o imagen en una zona),
/// con qué tolerancia, qué hacer y cuántas veces. Al elegir en la pantalla, el diálogo y AutoClik
/// se apartan para que se vea lo que hay debajo.
/// </summary>
public partial class WatchRuleDialog : Window
{
    public enum Outcome { Cancelled, Saved, Deleted }

    private readonly WatchRule _rule;
    private readonly IReadOnlyList<string> _macros;
    private Outcome _outcome = Outcome.Cancelled;
    private bool _hasColor;

    private WatchRuleDialog(WatchRule? existing, IReadOnlyList<string> macros)
    {
        InitializeComponent();
        _macros = macros;
        _rule = existing?.Clone() ?? new WatchRule();
        _hasColor = existing is { Kind: WatchKind.Color };
        TitleText.Text = Strings.T(existing == null ? "WatchNewRule" : "WatchEditRule");
        DeleteButton.Visibility = existing == null ? Visibility.Collapsed : Visibility.Visible;

        (_rule.Kind == WatchKind.Image ? ImageRadio : ColorRadio).IsChecked = true;
        (_rule.EveryTime ? EveryRadio : OnceRadio).IsChecked = true;
        ToleranceBox.Text = _rule.Tolerance.ToString(CultureInfo.InvariantCulture);
        ToleranceBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        ToleranceBox.GotKeyboardFocus += (_, _) => ToleranceBox.SelectAll();

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not ButtonBase) DragMove();
        };
        Refresh();
    }

    /// <summary>Abre el diálogo. Devuelve qué pasó y, si se guardó, la regla nueva.</summary>
    public static (Outcome Result, WatchRule? Rule) Ask(Window owner, WatchRule? existing, IReadOnlyList<string> macros)
    {
        var dialog = new WatchRuleDialog(existing, macros) { Owner = owner };
        dialog.ShowDialog();
        return (dialog._outcome, dialog._outcome == Outcome.Saved ? dialog._rule : null);
    }

    // ================= Mostrar =================

    private void Refresh()
    {
        bool image = ImageRadio.IsChecked == true;
        ColorPanel.Visibility = image ? Visibility.Collapsed : Visibility.Visible;
        ImagePanel.Visibility = image ? Visibility.Visible : Visibility.Collapsed;

        ColorSwatch.Background = _hasColor ? new SolidColorBrush(ToColor(_rule.Color)) : Brushes.Transparent;
        ColorText.Text = _hasColor ? $"#{_rule.Color:X6} · {_rule.X}, {_rule.Y}" : Strings.T("WatchNoColor");

        var preview = WatchImages.Preview(_rule.Image);
        ImagePreview.Source = preview;
        NoImageText.Visibility = preview == null ? Visibility.Visible : Visibility.Collapsed;
        ZoneText.Text = _rule.HasZone ? Strings.F("WatchZone", _rule.ZoneW, _rule.ZoneH, _rule.ZoneX, _rule.ZoneY) : Strings.T("WatchWholeScreen");
        AllScreenButton.Visibility = _rule.HasZone ? Visibility.Visible : Visibility.Collapsed;

        ActionText.Text = Strings.T(ActionKey(_rule.Action));
        ActionPointRow.Visibility = _rule.Action == WatchAction.ClickAt ? Visibility.Visible : Visibility.Collapsed;
        ActionPointText.Text = _rule.ActionX != 0 || _rule.ActionY != 0 ? $"X {_rule.ActionX} · Y {_rule.ActionY}" : Strings.T("WatchNoColor");
        ActionKeyRow.Visibility = _rule.Action == WatchAction.Key ? Visibility.Visible : Visibility.Collapsed;
        ActionKeyButton.Content = Hotkey.KeyName(_rule.KeyVk);
        ActionMacroButton.Visibility = _rule.Action == WatchAction.PlayMacro ? Visibility.Visible : Visibility.Collapsed;
        ActionMacroText.Text = _macros.Count == 0 ? Strings.T("WatchNoMacros") : _rule.MacroName ?? Strings.T("WatchNoMacro");
        ActionHintText.Text = _rule.Action switch
        {
            WatchAction.StopAll => Strings.T("WatchStopAllHint"),
            WatchAction.StartClicks => Strings.T("WatchStartClicksHint"),
            _ => "",
        };
        ActionHintText.Visibility = ActionHintText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static string ActionKey(WatchAction action) => action switch
    {
        WatchAction.ClickAt => "WatchDoClickAt",
        WatchAction.Key => "WatchDoKey",
        WatchAction.StartClicks => "WatchDoStartClicks",
        WatchAction.PlayMacro => "WatchDoPlayMacro",
        WatchAction.StopAll => "WatchDoStopAll",
        _ => "WatchDoClickOn",
    };

    private static Color ToColor(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private void Kind_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || ColorPanel == null) return;
        Refresh();
    }

    // ================= Elegir en pantalla =================

    /// <summary>Aparta el diálogo y AutoClik, ejecuta <paramref name="pick"/> y los vuelve a mostrar.</summary>
    private T Aside<T>(Func<T> pick)
    {
        Window? owner = Owner;
        Opacity = 0;
        owner?.Hide();
        try
        {
            return pick();
        }
        finally
        {
            owner?.Show();
            Opacity = 1;
            Activate();
        }
    }

    /// <summary>Tras cerrar la capa de elegir, dar tiempo a que la pantalla se redibuje sin ella.</summary>
    private static void LetScreenRepaint() => Thread.Sleep(180);

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        var result = Aside(() =>
        {
            var p = PositionPicker.Pick(null);
            if (p == null) return ((int, int, int)?)null;
            LetScreenRepaint();
            return (p.Value.X, p.Value.Y, ScreenCapture.PixelAt(p.Value.X, p.Value.Y));
        });
        if (result is not { } r) return;
        (_rule.X, _rule.Y, _rule.Color) = r;
        _hasColor = true;
        Refresh();
    }

    private void Crop_Click(object sender, RoutedEventArgs e)
    {
        string? name = Aside(() =>
        {
            var zone = RegionPicker.Pick(Strings.T("WatchCropHint"));
            if (zone is not { } z) return null;
            LetScreenRepaint();
            return WatchImages.Save(ScreenCapture.Capture(z.X, z.Y, z.Width, z.Height));
        });
        if (name == null) return;
        _rule.Image = name;
        Refresh();
    }

    private void Zone_Click(object sender, RoutedEventArgs e)
    {
        var zone = Aside(() => RegionPicker.Pick(Strings.T("WatchZoneHint")));
        if (zone is not { } z) return;
        (_rule.ZoneX, _rule.ZoneY, _rule.ZoneW, _rule.ZoneH) = z;
        Refresh();
    }

    private void AllScreen_Click(object sender, RoutedEventArgs e)
    {
        _rule.ZoneW = _rule.ZoneH = 0;
        Refresh();
    }

    private void PickActionPoint_Click(object sender, RoutedEventArgs e)
    {
        var p = Aside(() => PositionPicker.Pick(null));
        if (p is not { } point) return;
        (_rule.ActionX, _rule.ActionY) = point;
        Refresh();
    }

    // ================= Acción =================

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ActionButton, Placement = PlacementMode.Bottom, MinWidth = ActionButton.ActualWidth };
        foreach (WatchAction action in Enum.GetValues(typeof(WatchAction)))
        {
            var item = new MenuItem { Header = Strings.T(ActionKey(action)), IsChecked = action == _rule.Action };
            WatchAction chosen = action;
            item.Click += (_, _) => { _rule.Action = chosen; Refresh(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ActionKey_Click(object sender, RoutedEventArgs e)
    {
        ActionKeyButton.IsChecked = false;
        var (accepted, key) = KeyDialog.Ask(this, Strings.T("WatchDoKey"), null, _ => null);
        if (!accepted || key is not { IsMouse: false } k) return;
        _rule.KeyVk = k.VirtualKey;
        Refresh();
    }

    private void ActionMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_macros.Count == 0) return;
        var menu = new ContextMenu { PlacementTarget = ActionMacroButton, Placement = PlacementMode.Bottom, MinWidth = ActionMacroButton.ActualWidth };
        foreach (string name in _macros)
        {
            var item = new MenuItem { Header = name, IsChecked = name == _rule.MacroName };
            string chosen = name;
            item.Click += (_, _) => { _rule.MacroName = chosen; Refresh(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    // ================= Guardar =================

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _rule.Kind = ImageRadio.IsChecked == true ? WatchKind.Image : WatchKind.Color;
        _rule.EveryTime = EveryRadio.IsChecked == true;
        _rule.Tolerance = int.TryParse(ToleranceBox.Text, out int t) ? Math.Max(0, Math.Min(100, t)) : 10;

        bool complete = (_rule.Kind == WatchKind.Color ? _hasColor : WatchImages.Preview(_rule.Image) != null)
            && _rule.Action switch
            {
                WatchAction.ClickAt => _rule.ActionX != 0 || _rule.ActionY != 0,
                WatchAction.PlayMacro => !string.IsNullOrEmpty(_rule.MacroName),
                _ => true,
            };
        if (!complete)
        {
            ErrorText.Text = Strings.T("WatchRuleIncomplete");
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        _outcome = Outcome.Saved;
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        _outcome = Outcome.Deleted;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
