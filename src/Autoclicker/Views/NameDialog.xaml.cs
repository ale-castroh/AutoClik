using System.Windows;
using System.Windows.Input;

namespace Autoclicker.Views;

/// <summary>Diálogo pequeño para escribir un nombre, o para confirmar una acción.</summary>
public partial class NameDialog : Window
{
    private readonly Func<string, string?>? _validate;
    private readonly bool _confirmOnly;

    public string ResultName => NameBox.Text.Trim();

    private NameDialog(string title, string? message, string initial, string okText,
                       Func<string, string?>? validate, bool confirmOnly)
    {
        InitializeComponent();
        TitleText.Text = title;
        OkButton.Content = okText;
        _validate = validate;
        _confirmOnly = confirmOnly;

        if (message != null)
        {
            MessageText.Text = message;
            MessageText.Visibility = Visibility.Visible;
        }

        if (confirmOnly)
        {
            NameBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            NameBox.Text = initial;
            Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
        }

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    /// <summary>Pide un nombre. <paramref name="validate"/> devuelve un mensaje de error o null si vale.</summary>
    public static string? AskName(Window owner, string title, string initial, Func<string, string?> validate)
    {
        var dialog = new NameDialog(title, null, initial, "Guardar", validate, confirmOnly: false) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.ResultName : null;
    }

    public static bool Confirm(Window owner, string title, string message, string okText)
    {
        var dialog = new NameDialog(title, message, "", okText, null, confirmOnly: true) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!_confirmOnly)
        {
            string? error = ResultName.Length == 0 ? "Escribe un nombre" : _validate?.Invoke(ResultName);
            if (error != null)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                NameBox.Focus();
                return;
            }
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
