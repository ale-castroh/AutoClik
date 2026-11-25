using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autoclicker.Interop;
using Autoclicker.Localization;
using Autoclicker.Macros;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Views;

/// <summary>Una fila de la lista de pasos (lo que se ve). ToString = descripción, para UI Automation.</summary>
public sealed class StepRow : INotifyPropertyChanged
{
    public StepRow(MacroStep step, int index) { Step = step; Number = (index + 1).ToString(CultureInfo.CurrentCulture); Update(); }

    public MacroStep Step { get; }
    public string Number { get; }
    public string Glyph { get; private set; } = "";
    public string Text { get; private set; } = "";
    public string Wait { get; private set; } = "";
    public Visibility WaitVisibility { get; private set; }

    public void Update()
    {
        Glyph = MacroSteps.Glyph(Step.Kind);
        Text = MacroSteps.Describe(Step);
        Wait = Strings.F("EditorWaitLine", MacroSteps.Seconds(Step.WaitBefore));
        // Las esperas muy cortas (menos de 0,05 s) no se enseñan: solo ensuciarían la lista
        WaitVisibility = Step.WaitBefore >= 50_000 ? Visibility.Visible : Visibility.Collapsed;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public override string ToString() => Text;

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Editor de macros (2.3): la macro como lista de pasos legibles. Borrar, mover, cambiar esperas,
/// teclas, posiciones y textos; insertar esperas, teclas, textos y clics; limpiar (acortar esperas,
/// quitar movimientos) y posiciones relativas a una ventana. Nada se guarda hasta pulsar Guardar.
/// </summary>
public partial class MacroEditor : Window
{
    private readonly Macro _macro;
    private List<MacroStep> _steps;
    private readonly List<Snapshot> _undo = [];
    private bool _loading;        // rellenando los campos desde código: no aplicar
    private bool _fieldSnapshot;  // ya se guardó el "deshacer" de esta edición de campos
    private bool _dirty;
    private bool _saved;

    private string? _process, _windowTitle;
    private int _windowX, _windowY;

    private sealed record Snapshot(List<MacroStep> Steps, string? Process, string? Title, int X, int Y);

    /// <summary>Probar: la macro tal como está en el editor (sin guardar).</summary>
    public event Action<Macro>? TestRequested;

    /// <summary>Guardar: la macro editada (mismo nombre, eventos y posiciones nuevos).</summary>
    public event Action<Macro>? Saved;

    public MacroEditor(Macro macro)
    {
        InitializeComponent();
        _macro = macro;
        _steps = MacroSteps.FromEvents(macro.Events);
        _process = macro.WindowProcess;
        _windowTitle = macro.WindowTitle;
        _windowX = macro.WindowX;
        _windowY = macro.WindowY;

        TitleText.Text = Strings.F("EditorTitle", macro.Name);
        _loading = true;
        (_process != null ? RelativeRadio : ScreenRadio).IsChecked = true;
        _loading = false;

        foreach (var box in new[] { WaitBox, HoldBox, ShortenBox }) AllowNumber(box, decimals: true);
        foreach (var box in new[] { XBox, YBox }) AllowNumber(box, decimals: false);

        Rebuild(_steps.Count > 0 ? 0 : null);
        Loaded += (_, _) => StepList.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowBackdrop.Attach(this);
    }

    // ================= Lista =================

    private void Rebuild(int? select = null, int count = 1)
    {
        StepList.ItemsSource = _steps.Select((s, i) => new StepRow(s, i)).ToList();
        if (select is int i && _steps.Count > 0)
        {
            i = Compat.Clamp(i, 0, _steps.Count - 1);
            StepList.SelectedIndex = i;
            for (int k = 1; k < count && i + k < _steps.Count; k++) StepList.SelectedItems.Add(StepList.Items[i + k]);
            StepList.ScrollIntoView(StepList.Items[i]);
        }
        RefreshChrome();
        ShowFields();
    }

    private void RefreshChrome()
    {
        long total = MacroSteps.ToEvents(_steps).LastOrDefault().Micros;
        SummaryText.Text = Strings.F("EditorSummary", _steps.Count, MacroSteps.Seconds(total));
        bool any = SelectedIndices.Count > 0;
        DeleteButton.IsEnabled = any;
        UpButton.IsEnabled = any && SelectedIndices[0] > 0;
        DownButton.IsEnabled = any && SelectedIndices[SelectedIndices.Count - 1] < _steps.Count - 1;
        UndoButton.IsEnabled = _undo.Count > 0;
        TestButton.IsEnabled = _steps.Count > 0;
        RemoveMovesButton.IsEnabled = _steps.Any(s => s.Kind == StepKind.Moves);

        bool relative = RelativeRadio.IsChecked == true;
        WindowButton.Visibility = relative ? Visibility.Visible : Visibility.Collapsed;
        WindowText.Text = _process == null ? Strings.T("EditorWindowNone")
            : Strings.F("EditorWindowValue", string.IsNullOrWhiteSpace(_windowTitle) ? _process : _windowTitle!);
        RelativeHintText.Text = Strings.T(relative ? "EditorRelativeHint" : "EditorScreenHint");
    }

    /// <summary>Índices elegidos, en orden.</summary>
    private List<int> SelectedIndices =>
        StepList.SelectedItems.Cast<StepRow>().Select(r => _steps.IndexOf(r.Step)).Where(i => i >= 0).OrderBy(i => i).ToList();

    private MacroStep? SingleSelected => StepList.SelectedItems.Count == 1 ? ((StepRow)StepList.SelectedItem).Step : null;

    private void StepList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _fieldSnapshot = false;
        RefreshChrome();
        ShowFields();
    }

    private void StepList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && DeleteButton.IsEnabled) { Delete_Click(sender, e); e.Handled = true; }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && e.OriginalSource is not TextBox)
        {
            Undo_Click(this, e);
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    // ================= Panel del paso =================

    private void ShowFields()
    {
        _loading = true;
        MacroStep? step = SingleSelected;
        NoSelectionText.Visibility = step == null ? Visibility.Visible : Visibility.Collapsed;
        NoSelectionText.Text = Strings.T("EditorNoSelection");
        StepTitleText.Text = step == null ? "" : MacroSteps.Describe(step);
        StepTitleText.Visibility = step == null ? Visibility.Collapsed : Visibility.Visible;

        bool key = step?.Kind is StepKind.KeyPress or StepKind.KeyDown or StepKind.KeyUp;
        bool position = step?.Kind is StepKind.Click or StepKind.ButtonDown or StepKind.ButtonUp;
        Show(WaitRow, step != null);
        Show(KeyRow, key);
        Show(HoldRow, step?.Kind == StepKind.KeyPress);
        Show(PositionRow, position);
        Show(TextRow, step?.Kind == StepKind.Text);

        if (step != null)
        {
            WaitBox.Text = MacroSteps.Seconds(step.WaitBefore);
            if (key) KeyButton.Content = Input.Hotkey.KeyName(step.Events.First(e => e.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp).Data);
            if (step.Kind == StepKind.KeyPress) HoldBox.Text = MacroSteps.Seconds(step.Duration);
            if (position && step.FirstPointer is { } p)
            {
                XBox.Text = p.X.ToString(CultureInfo.InvariantCulture);
                YBox.Text = p.Y.ToString(CultureInfo.InvariantCulture);
            }
            if (step.Kind == StepKind.Text) StepTextBox.Text = MacroSteps.TextOf(step);
        }
        _loading = false;
    }

    private static void Show(FrameworkElement element, bool visible) =>
        element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Los campos se aplican al escribir (un solo "deshacer" por paso y edición).</summary>
    private void Field_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || SingleSelected is not { } step) return;
        if (!_fieldSnapshot) { PushUndo(); _fieldSnapshot = true; }

        if (sender == WaitBox && ParseSeconds(WaitBox.Text) is long wait) step.WaitBefore = wait;
        else if (sender == HoldBox && ParseSeconds(HoldBox.Text) is long hold) MacroSteps.SetHold(step, hold);
        else if ((sender == XBox || sender == YBox)
                 && int.TryParse(XBox.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)
                 && int.TryParse(YBox.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y))
            MacroSteps.SetPosition(step, x, y);
        else if (sender == StepTextBox && StepTextBox.Text.Length > 0) MacroSteps.SetText(step, StepTextBox.Text);
        else return;

        MarkDirty();
        ((StepRow)StepList.SelectedItem).Update();
        StepTitleText.Text = MacroSteps.Describe(step);
        RefreshChrome();
    }

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        KeyButton.IsChecked = false;
        if (SingleSelected is not { } step) return;
        var (accepted, key) = KeyDialog.Ask(this, Strings.T("EditorKey"), null, _ => null);
        if (!accepted || key is not { } k) return;
        PushUndo();
        MacroSteps.SetKey(step, k.VirtualKey);
        Changed(_steps.IndexOf(step));
    }

    private void PickPosition_Click(object sender, RoutedEventArgs e)
    {
        if (SingleSelected is not { } step || PickOnScreen() is not { } p) return;
        PushUndo();
        MacroSteps.SetPosition(step, p.X, p.Y);
        Changed(_steps.IndexOf(step));
    }

    // ================= Barra de herramientas =================

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        int at = SelectedIndices.Count > 0 ? SelectedIndices[SelectedIndices.Count - 1] + 1 : _steps.Count;
        var menu = new ContextMenu { PlacementTarget = InsertButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        menu.Items.Add(MenuAction("EditorInsertWait", "", () => InsertWait(at), at < _steps.Count));
        menu.Items.Add(MenuAction("EditorInsertKey", "", () => InsertKey(at), true));
        menu.Items.Add(MenuAction("EditorInsertText", "", () => InsertText(at), true));
        menu.Items.Add(MenuAction("EditorInsertClick", "", () => InsertClick(at), true));
        menu.IsOpen = true;
    }

    private static MenuItem MenuAction(string key, string icon, Action action, bool enabled)
    {
        var item = new MenuItem { Header = Strings.T(key), Icon = icon, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Una espera es el tiempo antes de un paso: se suma 1 s al siguiente y se elige para ajustarla.</summary>
    private void InsertWait(int at)
    {
        PushUndo();
        _steps[at].WaitBefore += 1_000_000;
        Changed(at);
        WaitBox.Focus();
        WaitBox.SelectAll();
    }

    private void InsertKey(int at)
    {
        var (accepted, key) = KeyDialog.Ask(this, Strings.T("EditorInsertKey"), null, _ => null);
        if (!accepted || key is not { } k) return;
        Insert(at, MacroSteps.NewKeyPress(k.VirtualKey, 100_000, 300_000));
    }

    private void InsertText(int at)
    {
        string? text = NameDialog.AskName(this, Strings.T("EditorTextTitle"), "", _ => null, maxLength: 500);
        if (string.IsNullOrEmpty(text)) return;
        Insert(at, MacroSteps.NewText(text!, 300_000));
    }

    private void InsertClick(int at)
    {
        if (PickOnScreen() is not { } p) return;
        Insert(at, MacroSteps.NewClick(MacroButton.Left, p.X, p.Y, 300_000));
    }

    private void Insert(int at, MacroStep step)
    {
        PushUndo();
        _steps.Insert(at, step);
        Changed(at);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var indices = SelectedIndices;
        if (indices.Count == 0) return;
        PushUndo();
        for (int k = indices.Count - 1; k >= 0; k--) _steps.RemoveAt(indices[k]);
        Changed(Math.Min(indices[0], _steps.Count - 1));
    }

    private void Up_Click(object sender, RoutedEventArgs e) => MoveSelection(-1);

    private void Down_Click(object sender, RoutedEventArgs e) => MoveSelection(+1);

    /// <summary>Mueve el bloque elegido un puesto (cada paso se lleva su espera).</summary>
    private void MoveSelection(int delta)
    {
        var indices = SelectedIndices;
        if (indices.Count == 0) return;
        int first = indices[0], last = indices[indices.Count - 1];
        if (first + delta < 0 || last + delta >= _steps.Count) return;
        PushUndo();
        var block = indices.Select(i => _steps[i]).ToList();
        for (int k = indices.Count - 1; k >= 0; k--) _steps.RemoveAt(indices[k]);
        int at = first + delta;
        _steps.InsertRange(at, block);
        MarkDirty();
        Rebuild(at, block.Count);
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undo.Count == 0) return;
        Snapshot s = _undo[_undo.Count - 1];
        _undo.RemoveAt(_undo.Count - 1);
        int keep = SelectedIndices.FirstOrDefault();
        _steps = s.Steps;
        _process = s.Process;
        _windowTitle = s.Title;
        _windowX = s.X;
        _windowY = s.Y;
        _loading = true;
        (_process != null ? RelativeRadio : ScreenRadio).IsChecked = true;
        _loading = false;
        _fieldSnapshot = false;
        StatusText.Text = "";
        Rebuild(keep);
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        if (_steps.Count == 0) return;
        if (RelativeRadio.IsChecked == true && _process == null) { StatusText.Text = Strings.T("EditorWindowNone"); return; }
        TestRequested?.Invoke(BuildMacro());
    }

    // ================= Limpiar =================

    private void Shorten_Click(object sender, RoutedEventArgs e)
    {
        if (ParseSeconds(ShortenBox.Text) is not long max) return;
        var copy = CloneSteps(_steps);
        int changed = MacroSteps.ShortenWaits(_steps, max);
        Report(changed, copy);
    }

    private void RemoveMoves_Click(object sender, RoutedEventArgs e)
    {
        var copy = CloneSteps(_steps);
        int changed = MacroSteps.RemoveMoves(_steps);
        Report(changed, copy);
    }

    private void Report(int changed, List<MacroStep> before)
    {
        if (changed == 0) { StatusText.Text = Strings.T("EditorNothingChanged"); return; }
        _undo.Add(new Snapshot(before, _process, _windowTitle, _windowX, _windowY));
        MarkDirty();
        StatusText.Text = Strings.F("EditorChanged", changed);
        Rebuild(SelectedIndices.FirstOrDefault());
    }

    // ================= Posiciones relativas =================

    private void Positions_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || !IsLoaded) return;
        if (RelativeRadio.IsChecked == true && _process == null)
        {
            if (!PickWindow())
            {
                _loading = true;
                ScreenRadio.IsChecked = true;
                _loading = false;
            }
        }
        else if (ScreenRadio.IsChecked == true && _process != null)
        {
            PushUndo();
            _process = null;
            _windowTitle = null;
            MarkDirty();
        }
        RefreshChrome();
    }

    private void PickWindow_Click(object sender, RoutedEventArgs e)
    {
        PickWindow();
        RefreshChrome();
    }

    /// <summary>Con la cruz, la ventana a la que se refieren las posiciones. Guarda dónde está su esquina.</summary>
    private bool PickWindow()
    {
        if (PickOnScreen(Strings.T("EditorWindowPickHint")) is not { } p) return false;
        nint hwnd = GetAncestor(WindowFromPoint(new POINT { X = p.X, Y = p.Y }), GA_ROOT);
        if (hwnd == 0) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == (uint)Compat.ProcessId) return false;
        string name;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            name = process.ProcessName;
        }
        catch (ArgumentException)
        {
            return false;
        }
        var origin = new POINT();
        ClientToScreen(hwnd, ref origin);
        var title = new System.Text.StringBuilder(256);
        GetWindowTextW(hwnd, title, title.Capacity);

        PushUndo();
        _process = name;
        _windowTitle = title.ToString().Trim();
        _windowX = origin.X;
        _windowY = origin.Y;
        MarkDirty();
        return true;
    }

    /// <summary>Elegir un punto con la cruz, con el editor y AutoClik ocultos (para ver lo de debajo).</summary>
    private (int X, int Y)? PickOnScreen(string? hint = null)
    {
        Window? owner = Owner;
        Hide();
        owner?.Hide();
        var picked = PositionPicker.Pick(null, hint);
        owner?.Show();
        Show();
        Activate();
        return picked;
    }

    // ================= Guardar / cerrar =================

    private Macro BuildMacro()
    {
        var m = _macro.Clone(_macro.Name);
        m.Created = _macro.Created;
        m.HotkeyVk = _macro.HotkeyVk;
        m.HotkeyModifiers = _macro.HotkeyModifiers;
        m.Events = MacroSteps.ToEvents(_steps);
        bool relative = RelativeRadio.IsChecked == true && _process != null;
        m.WindowProcess = relative ? _process : null;
        m.WindowTitle = relative ? _windowTitle : null;
        m.WindowX = relative ? _windowX : 0;
        m.WindowY = relative ? _windowY : 0;
        return m;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_steps.Count == 0) { StatusText.Text = Strings.T("EditorEmpty"); return; }
        Saved?.Invoke(BuildMacro());
        _saved = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_saved || !_dirty) return;
        if (!NameDialog.Confirm(this, Strings.T("EditorDiscard"), Strings.F("EditorDiscardConfirm", _macro.Name), Strings.T("EditorDiscardOk")))
            e.Cancel = true;
    }

    // ================= Utilidades =================

    private void Changed(int select)
    {
        MarkDirty();
        StatusText.Text = "";
        Rebuild(select);
    }

    private void MarkDirty() => _dirty = true;

    private void PushUndo()
    {
        _undo.Add(new Snapshot(CloneSteps(_steps), _process, _windowTitle, _windowX, _windowY));
        if (_undo.Count > 100) _undo.RemoveAt(0);
        UndoButton.IsEnabled = true;
    }

    private static List<MacroStep> CloneSteps(List<MacroStep> steps) => steps.Select(s => s.Clone()).ToList();

    /// <summary>Segundos escritos (acepta 1,5 y 1.5) a microsegundos; null si no es un número válido.</summary>
    private static long? ParseSeconds(string text) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= 0 && v <= 86_400
            ? (long)Math.Round(v * 1_000_000) : null;

    private static void AllowNumber(TextBox box, bool decimals)
    {
        box.PreviewTextInput += (_, e) =>
            e.Handled = !e.Text.All(c => Compat.IsDigit(c) || (decimals && c is ',' or '.') || (!decimals && c == '-'));
        box.GotKeyboardFocus += (_, _) => box.SelectAll();
    }
}
