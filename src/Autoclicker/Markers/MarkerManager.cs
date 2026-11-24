using Autoclicker.Engine;
using Autoclicker.Views;

namespace Autoclicker.Markers;

/// <summary>
/// Lista ordenada de puntos y sus marcadores en pantalla. Mantiene la numeración,
/// muestra/oculta todos a la vez y los vuelve traspasables mientras se clica.
/// </summary>
public sealed class MarkerManager
{
    private readonly List<MarkerWindow> _windows = [];
    private bool _visible;
    private bool _clickThrough;
    private MarkerEditor? _editor;

    /// <summary>Se añadió, quitó, movió o reordenó algún punto.</summary>
    public event Action? Changed;

    public int Count => _windows.Count;

    public IReadOnlyList<ClickPoint> Snapshot() => _windows.Select(w => w.Point.ToClickPoint()).ToList();

    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            foreach (var w in _windows)
            {
                if (value) w.ShowAtPoint();
                else w.Hide();
            }
            if (value) BringToFront();
            if (!value) _editor?.SafeClose();
        }
    }

    public bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            _clickThrough = value;
            if (value) _editor?.SafeClose();
            foreach (var w in _windows) w.SetClickThrough(value);
            if (!value) BringToFront();
        }
    }

    /// <summary>Recoloca todos los marcadores encima del resto de ventanas.</summary>
    public void BringToFront()
    {
        foreach (var w in _windows) w.BringToFront();
    }

    public IReadOnlyList<MarkerPoint> Points => _windows.Select(w => w.Point).ToList();

    public void Add(int x, int y)
    {
        AddWindow(new MarkerPoint { X = x, Y = y });
        Changed?.Invoke();
    }

    /// <summary>Sustituye todos los puntos (al cargar un perfil).</summary>
    public void Load(IEnumerable<MarkerPoint> points)
    {
        _editor?.SafeClose();
        foreach (var w in _windows) w.Close();
        _windows.Clear();
        foreach (var p in points) AddWindow(p);
        Changed?.Invoke();
    }

    private void AddWindow(MarkerPoint point)
    {
        var window = new MarkerWindow(point, _windows.Count + 1);
        window.Clicked += OpenEditor;
        window.Moved += _ => Changed?.Invoke();
        _windows.Add(window);

        if (_visible)
        {
            window.ShowAtPoint();
            window.SetClickThrough(_clickThrough);
        }
    }

    public void Clear()
    {
        _editor?.SafeClose();
        foreach (var w in _windows) w.Close();
        _windows.Clear();
        Changed?.Invoke();
    }

    private void Remove(MarkerWindow window)
    {
        window.Close();
        _windows.Remove(window);
        Renumber();
        Changed?.Invoke();
    }

    private void Move(MarkerWindow window, int delta)
    {
        int from = _windows.IndexOf(window);
        int to = Math.Clamp(from + delta, 0, _windows.Count - 1);
        if (from == to) return;
        _windows.RemoveAt(from);
        _windows.Insert(to, window);
        Renumber();
        Changed?.Invoke();
    }

    private void Renumber()
    {
        for (int i = 0; i < _windows.Count; i++) _windows[i].SetNumber(i + 1);
    }

    private void OpenEditor(MarkerWindow window)
    {
        if (_clickThrough) return;
        _editor?.SafeClose();

        var editor = new MarkerEditor(window.Point, _windows.IndexOf(window) + 1, window.Handle);
        editor.MoveRequested += delta => Move(window, delta);
        editor.DeleteRequested += () => Remove(window);
        editor.Edited += () => Changed?.Invoke();
        editor.Closed += (_, _) => { if (_editor == editor) _editor = null; };
        _editor = editor;
        editor.Show();
    }
}
