using System.Windows;

namespace Autoclicker.Theme;

/// <summary>
/// Propiedades extra para las plantillas de los controles (se usan en XAML como
/// <c>t:Ui.CheckedText="Detener"</c>).
/// </summary>
public static class Ui
{
    /// <summary>Texto del botón principal mientras está activo (Content es el texto en reposo).</summary>
    public static readonly DependencyProperty CheckedTextProperty = DependencyProperty.RegisterAttached(
        "CheckedText", typeof(string), typeof(Ui), new PropertyMetadata(""));

    public static string GetCheckedText(DependencyObject o) => (string)o.GetValue(CheckedTextProperty);
    public static void SetCheckedText(DependencyObject o, string value) => o.SetValue(CheckedTextProperty, value);

    /// <summary>Tecla que se muestra dentro del botón (p. ej. "F6"). Vacío = sin tecla.</summary>
    public static readonly DependencyProperty KeyTextProperty = DependencyProperty.RegisterAttached(
        "KeyText", typeof(string), typeof(Ui), new PropertyMetadata(""));

    public static string GetKeyText(DependencyObject o) => (string)o.GetValue(KeyTextProperty);
    public static void SetKeyText(DependencyObject o, string value) => o.SetValue(KeyTextProperty, value);
}
