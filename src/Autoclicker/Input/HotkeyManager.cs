using System.Windows.Threading;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Input;

/// <summary>
/// Atajo global. Teclado: RegisterHotKey (funciona con la app en segundo plano o en la bandeja).
/// Botones laterales del ratón: sondeo ligero de GetAsyncKeyState (100 Hz, solo si se usan).
/// Lanza Pressed y, al soltar, Released (necesario para el modo "mantener").
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly int _id;
    private readonly nint _hwnd;
    private readonly DispatcherTimer _poll;
    private bool _registered;
    private bool _down;
    private bool _suspended;

    public Hotkey Current { get; private set; }

    public event Action? Pressed;
    public event Action? Released;

    /// <param name="id">Identificador del atajo en la ventana (uno distinto por atajo: 1 clics, 3 grabar, 4 reproducir).</param>
    public HotkeyManager(nint hwnd, int id = 1)
    {
        _hwnd = hwnd;
        _id = id;
        _poll = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(10) };
        _poll.Tick += (_, _) => Poll();
    }

    /// <summary>Asigna un atajo. Devuelve false si otra aplicación ya lo tiene (y no cambia nada).</summary>
    public bool TrySet(Hotkey hotkey)
    {
        Unregister();
        Current = hotkey;
        if (!Register())
        {
            return false;
        }
        return true;
    }

    /// <summary>Desactiva el atajo temporalmente (p. ej. mientras se elige uno nuevo).</summary>
    public bool Suspended
    {
        get => _suspended;
        set
        {
            if (_suspended == value) return;
            _suspended = value;
            if (value) Unregister();
            else Register();
        }
    }

    /// <summary>Llamar desde el WndProc de la ventana al recibir WM_HOTKEY.</summary>
    public bool HandleMessage(int msg, nint wParam)
    {
        if (msg != WM_HOTKEY || wParam != _id) return false;
        if (!_down)
        {
            _down = true;
            Pressed?.Invoke();
            _poll.Start(); // vigilar cuándo se suelta
        }
        return true;
    }

    private bool Register()
    {
        if (_suspended) return true;
        _down = false;

        if (Current.IsMouse)
        {
            _registered = true;
            _poll.Start();
            return true;
        }

        _registered = RegisterHotKey(_hwnd, _id, Current.Modifiers | MOD_NOREPEAT, (uint)Current.VirtualKey) != 0;
        return _registered;
    }

    private void Unregister()
    {
        _poll.Stop();
        if (_registered && !Current.IsMouse) UnregisterHotKey(_hwnd, _id);
        _registered = false;
        if (_down)
        {
            _down = false;
            Released?.Invoke();
        }
    }

    private void Poll()
    {
        bool isDown = Current.IsDown();

        if (Current.IsMouse && isDown && !_down)
        {
            _down = true;
            Pressed?.Invoke();
        }
        else if (!isDown && _down)
        {
            _down = false;
            Released?.Invoke();
            if (!Current.IsMouse) _poll.Stop();
        }
    }

    public void Dispose() => Unregister();
}
