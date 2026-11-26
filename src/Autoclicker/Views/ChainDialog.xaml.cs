using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Autoclicker.Localization;
using Autoclicker.Macros;

namespace Autoclicker.Views;

/// <summary>Una fila de la lista de la cadena. ToString = texto, para UI Automation.</summary>
public sealed class ChainRow(ChainItem item, int index)
{
    public ChainItem Item { get; } = item;
    public string Number { get; } = (index + 1).ToString(CultureInfo.CurrentCulture);
    public string Text => Strings.F("ChainItem", Item.Macro, Item.Times);
    public string Pause => Strings.F("ChainPauseLine", MacroSteps.Seconds(Item.PauseMicros));
    public Visibility PauseVisibility => Item.PauseMicros > 0 ? Visibility.Visible : Visibility.Collapsed;
    public override string ToString() => Text;
}

/// <summary>Crear o editar una cadena de macros: nombre y lista (macro, veces, pausa después).</summary>
public partial class ChainDialog : Window
{
    private readonly IReadOnlyList<string> _macros;
    private readonly Func<string, string?> _validateName;
    private readonly List<ChainItem> _items;
    private bool _accepted;
    private bool _loading;

    private ChainDialog(string title, string name, IEnumerable<ChainItem> items, IReadOnlyList<string> macros, Func<string, string?> validateName)
    {
        InitializeComponent();
        TitleText.Text = title;
        NameBox.Text = name;
        _macros = macros;
        _validateName = validateName;
        _items = items.ToList();
        foreach (var box in new[] { TimesBox, PauseBox })
        {
            box.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(c => char.IsDigit(c) || (box == PauseBox && c is ',' or '.'));
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
        }
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not ButtonBase and not TextBox) DragMove();
        };
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
        Rebuild(_items.Count > 0 ? 0 : -1);
    }

    /// <summary>Devuelve (nombre, elementos) o null si se canceló.</summary>
    public static (string Name, List<ChainItem> Items)? Ask(Window owner, string title, string name, IEnumerable<ChainItem> items,
                                                             IReadOnlyList<string> macros, Func<string, string?> validateName)
    {
        var dialog = new ChainDialog(title, name, items, macros, validateName) { Owner = owner };
        dialog.ShowDialog();
        return dialog._accepted ? (dialog.NameBox.Text.Trim(), dialog._items) : null;
    }

    private void Rebuild(int select)
    {
        ItemList.ItemsSource = _items.Select((item, i) => new ChainRow(item, i)).ToList();
        if (select >= 0 && select < _items.Count) ItemList.SelectedIndex = select;
        RefreshFields();
    }

    private void RefreshFields()
    {
        int i = ItemList.SelectedIndex;
        bool any = i >= 0;
        ItemFields.IsEnabled = any;
        RemoveButton.IsEnabled = any;
        UpButton.IsEnabled = any && i > 0;
        DownButton.IsEnabled = any && i < _items.Count - 1;
        _loading = true;
        TimesBox.Text = any ? _items[i].Times.ToString(CultureInfo.InvariantCulture) : "";
        PauseBox.Text = any ? MacroSteps.Seconds(_items[i].PauseMicros) : "";
        _loading = false;
    }

    private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshFields();

    private void Fields_Changed(object sender, TextChangedEventArgs e)
    {
        int i = ItemList.SelectedIndex;
        if (_loading || i < 0) return;
        ChainItem item = _items[i];
        if (sender == TimesBox && int.TryParse(TimesBox.Text, out int times) && times >= 1)
            item = item with { Times = Math.Min(times, 9999) };
        else if (sender == PauseBox && double.TryParse(PauseBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double s) && s >= 0)
            item = item with { PauseMicros = (long)Math.Round(Math.Min(s, 3600) * 1_000_000) };
        else return;
        _items[i] = item;
        // Actualizar el texto de la fila sin perder el foco del campo
        ((List<ChainRow>)ItemList.ItemsSource)[i] = new ChainRow(item, i);
        ItemList.Items.Refresh();
        _loading = true;
        ItemList.SelectedIndex = i;
        _loading = false;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = AddButton, Placement = PlacementMode.Bottom };
        foreach (string name in _macros)
        {
            var item = new MenuItem { Header = name };
            string chosen = name;
            item.Click += (_, _) =>
            {
                int at = ItemList.SelectedIndex >= 0 ? ItemList.SelectedIndex + 1 : _items.Count;
                _items.Insert(at, new ChainItem(chosen, 1, 0));
                ErrorText.Visibility = Visibility.Collapsed;
                Rebuild(at);
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        int i = ItemList.SelectedIndex;
        if (i < 0) return;
        _items.RemoveAt(i);
        Rebuild(Math.Min(i, _items.Count - 1));
    }

    private void Up_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void Down_Click(object sender, RoutedEventArgs e) => Move(+1);

    private void Move(int delta)
    {
        int i = ItemList.SelectedIndex, j = i + delta;
        if (i < 0 || j < 0 || j >= _items.Count) return;
        (_items[i], _items[j]) = (_items[j], _items[i]);
        Rebuild(j);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string? error = name.Length == 0 ? Strings.T("NameRequired")
            : _items.Count == 0 ? Strings.T("ChainEmpty")
            : _validateName(name);
        if (error != null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        _accepted = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
