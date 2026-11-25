using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Macros;

/// <summary>
/// Graba ratón (movimientos, botones, rueda) y teclado con hooks de bajo nivel, con tiempos en
/// microsegundos. Nada se bloquea: las apps reciben la entrada como siempre.
/// - Ignora la entrada simulada (la de AutoClik y otros programas), salvo en pruebas.
/// - Ignora los clics sobre ventanas de AutoClik (p. ej. "Terminar") y las teclas de los atajos.
/// - Los movimientos se guardan como mucho cada 8 ms (≈125 por segundo), y siempre el último antes de un clic.
/// - Al terminar se quitan la espera inicial y las teclas modificadoras sueltas del final.
/// Los hooks se instalan desde el hilo de la interfaz (necesitan su cola de mensajes).
/// </summary>
public static unsafe class MacroRecorder
{
    private const long MoveIntervalMicros = 8_000;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;
    private const int WM_MOUSEWHEEL = 0x020A, WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C, WM_MOUSEHWHEEL = 0x020E;
    private const uint LLKHF_EXTENDED = 0x01, LLKHF_INJECTED = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public nint dwExtraInfo;
    }

    private static nint _mouseHook, _keyboardHook;
    private static readonly List<MacroEvent> _events = [];
    private static readonly HashSet<int> _ignoredKeys = [];
    private static readonly HashSet<int> _keysDown = [];
    private static readonly bool[] _ignoredButton = new bool[5];
    private static long _startTicks;
    private static long _lastMoveMicros = -MoveIntervalMicros;
    private static MacroEvent? _pendingMove;
    private static Dispatcher? _dispatcher;

    /// <summary>Solo para pruebas automáticas: aceptar también la entrada simulada.</summary>
    public static bool AcceptInjected { get; set; }

    public static bool IsRecording => _mouseHook != 0 || _keyboardHook != 0;

    public static int ClickCount { get; private set; }
    public static int KeyCount { get; private set; }

    /// <summary>Se lanza (en el hilo de la interfaz) al grabar un clic o una tecla.</summary>
    public static event Action? Changed;

    /// <param name="ignoredKeys">Teclas virtuales que no se graban (las de los atajos de AutoClik).</param>
    public static void Start(MacroRecordMode mode, IEnumerable<int> ignoredKeys)
    {
        if (IsRecording) return;
        _events.Clear();
        _keysDown.Clear();
        _ignoredKeys.Clear();
        foreach (int vk in ignoredKeys) _ignoredKeys.Add(vk);
        Array.Clear(_ignoredButton, 0, _ignoredButton.Length);
        _pendingMove = null;
        _lastMoveMicros = -MoveIntervalMicros;
        ClickCount = KeyCount = 0;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _startTicks = Stopwatch.GetTimestamp();

        if (mode != MacroRecordMode.Keys) _mouseHook = SetWindowsHookExW(WH_MOUSE_LL, MouseProc, GetModuleHandleW(0), 0);
        if (mode != MacroRecordMode.Mouse) _keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardProc, GetModuleHandleW(0), 0);
    }

    /// <summary>Termina y devuelve los eventos ya limpios (sin espera inicial). Vacío = no se grabó nada.</summary>
    public static List<MacroEvent> Stop()
    {
        if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
        _mouseHook = _keyboardHook = 0;

        var events = new List<MacroEvent>(_events);
        _events.Clear();

        // Teclas modificadoras pulsadas justo al final (p. ej. el Ctrl de "Ctrl+F7" para terminar)
        while (events.Count > 0 && events[events.Count - 1].Kind == MacroEventKind.KeyDown && IsModifier(events[events.Count - 1].Data))
            events.RemoveAt(events.Count - 1);
        // Movimientos sueltos al final no aportan nada
        while (events.Count > 0 && events[events.Count - 1].Kind == MacroEventKind.Move)
            events.RemoveAt(events.Count - 1);
        if (events.Count == 0) return events;

        // Sin la espera del principio: el primer evento ocurre en el instante 0
        long t0 = events[0].Micros;
        return events.Select(e => e with { Micros = e.Micros - t0 }).ToList();
    }

    private static bool IsModifier(int vk) => vk is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;

    private static long Now() => (Stopwatch.GetTimestamp() - _startTicks) * 1_000_000 / Stopwatch.Frequency;

    private static void Notify() => _dispatcher?.InvokeAsync(() => Changed?.Invoke());

    // Referencias estáticas: si el recolector liberase los delegados, Windows llamaría a memoria inválida.
    private static readonly LowLevelHookProc MouseProc = MouseHook;
    private static readonly LowLevelHookProc KeyboardProc = KeyboardHook;

    private static nint MouseHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var info = (MSLLHOOKSTRUCT*)lParam;
            if ((info->flags & LLMHF_INJECTED) == 0 || AcceptInjected) OnMouse((int)wParam, info);
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static void OnMouse(int msg, MSLLHOOKSTRUCT* info)
    {
        long t = Now();
        int x = info->pt.X, y = info->pt.Y;

        if (msg == (int)WM_MOUSEMOVE)
        {
            var move = new MacroEvent(t, MacroEventKind.Move, x, y, 0, 0);
            if (t - _lastMoveMicros >= MoveIntervalMicros)
            {
                _events.Add(move);
                _lastMoveMicros = t;
                _pendingMove = null;
            }
            else
            {
                _pendingMove = move; // se guarda si luego hay un clic, para que caiga en su sitio exacto
            }
            return;
        }

        (MacroEventKind kind, int data)? ev = msg switch
        {
            WM_LBUTTONDOWN => (MacroEventKind.Down, (int)MacroButton.Left),
            WM_LBUTTONUP => (MacroEventKind.Up, (int)MacroButton.Left),
            WM_RBUTTONDOWN => (MacroEventKind.Down, (int)MacroButton.Right),
            WM_RBUTTONUP => (MacroEventKind.Up, (int)MacroButton.Right),
            WM_MBUTTONDOWN => (MacroEventKind.Down, (int)MacroButton.Middle),
            WM_MBUTTONUP => (MacroEventKind.Up, (int)MacroButton.Middle),
            WM_XBUTTONDOWN => (MacroEventKind.Down, (info->mouseData >> 16) == 2 ? (int)MacroButton.X2 : (int)MacroButton.X1),
            WM_XBUTTONUP => (MacroEventKind.Up, (info->mouseData >> 16) == 2 ? (int)MacroButton.X2 : (int)MacroButton.X1),
            WM_MOUSEWHEEL => (MacroEventKind.Wheel, (short)(info->mouseData >> 16)),
            WM_MOUSEHWHEEL => (MacroEventKind.HWheel, (short)(info->mouseData >> 16)),
            _ => null,
        };
        if (ev is not { } e) return;

        if (e.kind == MacroEventKind.Down)
        {
            // Clic sobre AutoClik (p. ej. "Terminar"): ni la pulsación ni su suelta se graban.
            if (IsOwnWindowAt(info->pt)) { _ignoredButton[e.data] = true; return; }
            ClickCount++;
            Notify();
        }
        else if (e.kind == MacroEventKind.Up && _ignoredButton[e.data])
        {
            _ignoredButton[e.data] = false;
            return;
        }

        if (_pendingMove is { } pending) { _events.Add(pending); _pendingMove = null; _lastMoveMicros = pending.Micros; }
        _events.Add(new MacroEvent(t, e.kind, x, y, e.data, 0));
    }

    private static nint KeyboardHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var info = (KBDLLHOOKSTRUCT*)lParam;
            if ((info->flags & LLKHF_INJECTED) == 0 || AcceptInjected) OnKey((int)wParam, info);
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static void OnKey(int msg, KBDLLHOOKSTRUCT* info)
    {
        int vk = (int)info->vkCode;
        if (_ignoredKeys.Contains(vk)) return;

        bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        bool up = msg is WM_KEYUP or WM_SYSKEYUP;
        if (!down && !up) return;

        if (down)
        {
            // Mantener una tecla genera repeticiones: basta la primera pulsación (al reproducir sigue pulsada).
            if (!_keysDown.Add(vk)) return;
            KeyCount++;
            Notify();
        }
        else if (!_keysDown.Remove(vk))
        {
            return; // suelta de una tecla pulsada antes de empezar a grabar
        }

        int flags = (info->flags & LLKHF_EXTENDED) != 0 ? 1 : 0;
        _events.Add(new MacroEvent(Now(), down ? MacroEventKind.KeyDown : MacroEventKind.KeyUp, (int)info->scanCode, 0, vk, flags));
    }

    private static bool IsOwnWindowAt(POINT pt)
    {
        nint hwnd = WindowFromPoint(pt);
        if (hwnd == 0) return false;
        GetWindowThreadProcessId(GetAncestor(hwnd, GA_ROOT), out uint pid);
        return pid == (uint)Compat.ProcessId;
    }
}
