using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Input;

/// <summary>
/// Parada de emergencia: si el usuario lleva el ratón a una esquina de cualquier monitor,
/// se lanza <see cref="Triggered"/>. Usa un hook de ratón de bajo nivel que solo mira el
/// movimiento físico (ignora los eventos inyectados por el propio autoclicker), así funciona
/// aunque el motor esté moviendo el cursor a un punto fijo. Solo está activo mientras clica.
/// </summary>
public static unsafe class EmergencyCornerWatcher
{
    private const int CornerMargin = 2; // píxeles

    private static nint _hook;
    private static bool _fired;
    private static Dispatcher? _dispatcher;

    public static event Action? Triggered;

    public static bool IsActive => _hook != 0;

    /// <summary>Instalar desde el hilo de la UI (el hook necesita su cola de mensajes).</summary>
    public static void Start()
    {
        if (_hook != 0) return;
        _fired = false;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _hook = SetWindowsHookExW(WH_MOUSE_LL, Proc, GetModuleHandleW(0), 0);
    }

    public static void Stop()
    {
        if (_hook == 0) return;
        UnhookWindowsHookEx(_hook);
        _hook = 0;
    }

    // Referencia estática: si el recolector liberase el delegado, Windows llamaría a memoria inválida.
    private static readonly LowLevelHookProc Proc = HookProc;

    private static nint HookProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && wParam == WM_MOUSEMOVE && !_fired)
        {
            var info = (MSLLHOOKSTRUCT*)lParam;
            if ((info->flags & LLMHF_INJECTED) == 0 && IsInCorner(info->pt))
            {
                _fired = true;
                // Fuera del hook: los hooks de bajo nivel deben volver enseguida.
                _dispatcher?.InvokeAsync(() => Triggered?.Invoke());
            }
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static bool IsInCorner(POINT p)
    {
        nint monitor = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (GetMonitorInfoW(monitor, &mi) == 0) return false;

        RECT r = mi.rcMonitor;
        bool left = p.X <= r.Left + CornerMargin;
        bool right = p.X >= r.Right - 1 - CornerMargin;
        bool top = p.Y <= r.Top + CornerMargin;
        bool bottom = p.Y >= r.Bottom - 1 - CornerMargin;
        return (left || right) && (top || bottom);
    }
}
