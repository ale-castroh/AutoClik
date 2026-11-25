using System.Windows;
using System.Windows.Input;
using Autoclicker.Input;
using Autoclicker.Localization;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Views;

/// <summary>
/// Pide un atajo (tecla, combinación o botón lateral del ratón). Esc cancela; "Quitar" lo borra.
/// Devuelve (true, atajo) al elegir, (true, null) al quitarlo y (false, _) al cancelar.
/// </summary>
public partial class KeyDialog : Window
{
    private readonly Func<Hotkey, string?> _validate;
    private bool _accepted;
    private Hotkey? _result;

    private KeyDialog(string title, Hotkey? current, Func<Hotkey, string?> validate)
    {
        InitializeComponent();
        TitleText.Text = title;
        _validate = validate;
        KeyText.Text = current?.ToString() ?? Strings.T("PressKey");
        RemoveButton.Visibility = current != null ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => { Activate(); Focus(); };
    }

    public static (bool Accepted, Hotkey? Key) Ask(Window owner, string title, Hotkey? current, Func<Hotkey, string?> validate)
    {
        var dialog = new KeyDialog(title, current, validate) { Owner = owner };
        dialog.ShowDialog();
        return (dialog._accepted, dialog._result);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { Close(); return; }

        // Solo modificadores: esperar a la tecla principal (y enseñar lo que se lleva).
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
            or Key.RightShift or Key.LWin or Key.RWin or Key.None or Key.ImeProcessed or Key.DeadCharProcessed)
            return;

        ModifierKeys mods = Keyboard.Modifiers;
        uint modifiers = 0;
        if (mods.HasFlag(ModifierKeys.Control)) modifiers |= MOD_CONTROL;
        if (mods.HasFlag(ModifierKeys.Alt)) modifiers |= MOD_ALT;
        if (mods.HasFlag(ModifierKeys.Shift)) modifiers |= MOD_SHIFT;
        if (mods.HasFlag(ModifierKeys.Windows)) modifiers |= MOD_WIN;
        Choose(new Hotkey(KeyInterop.VirtualKeyFromKey(key), modifiers));
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton is MouseButton.XButton1 or MouseButton.XButton2)
        {
            e.Handled = true;
            Choose(new Hotkey(e.ChangedButton == MouseButton.XButton1 ? VK_XBUTTON1 : VK_XBUTTON2, 0));
            return;
        }
        // Arrastrar el diálogo desde cualquier sitio que no sea un botón
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
        {
            base.OnPreviewMouseDown(e);
            try { DragMove(); } catch (InvalidOperationException) { }
            return;
        }
        base.OnPreviewMouseDown(e);
    }

    private void Choose(Hotkey hotkey)
    {
        KeyText.Text = hotkey.ToString();
        if (_validate(hotkey) is { } error)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        _accepted = true;
        _result = hotkey;
        Close();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        _accepted = true;
        _result = null;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
