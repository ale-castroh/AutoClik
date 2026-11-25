using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Autoclicker.Engine;
using Autoclicker.Markers;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Input;

/// <summary>
/// Graba los clics reales del usuario (posición, botón, cuándo y cuánto se mantuvieron)
/// con un hook de ratón de bajo nivel. Los clics no se bloquean: llegan a las apps como siempre.
/// Se ignoran los clics inyectados por programas y los hechos sobre ventanas de esta app.
/// </summary>
public static unsafe class ClickRecorder
{
    /// <summary>Pulsación más larga que esto = "mantener" al reproducir.</summary>
    private const int HoldThresholdMs = 300;

    /// <summary>Si al soltar el ratón está a más de esto del punto donde se pulsó = arrastre.</summary>
    private const int DragThresholdPx = 12;

    private sealed class Press
    {
        public MouseButtonKind Button;
        public int X, Y;
        public int EndX, EndY;
        public long Down, Up;
    }

    private static nint _hook;
    private static Dispatcher? _dispatcher;
    private static readonly List<Press> _presses = [];
    private static readonly Press?[] _open = new Press?[3];

    /// <summary>Solo para pruebas automáticas: aceptar también clics inyectados.</summary>
    public static bool AcceptInjected { get; set; }

    public static bool IsRecording => _hook != 0;

    public static int Count => _presses.Count;

    /// <summary>Se lanza (en el hilo de UI) cada vez que se graba un clic.</summary>
    public static event Action? ClickRecorded;

    public static void Start()
    {
        if (_hook != 0) return;
        _presses.Clear();
        Array.Clear(_open, 0, _open.Length);
        _dispatcher = Dispatcher.CurrentDispatcher;
        _hook = SetWindowsHookExW(WH_MOUSE_LL, Proc, GetModuleHandleW(0), 0);
    }

    /// <summary>Termina y convierte la grabación en puntos con sus esperas reales.</summary>
    public static List<MarkerPoint> Stop()
    {
        if (_hook != 0) UnhookWindowsHookEx(_hook);
        _hook = 0;

        long freq = Stopwatch.Frequency;
        var done = _presses.Where(p => p.Up != 0).OrderBy(p => p.Down).ToList();
        var points = new List<MarkerPoint>(done.Count);

        for (int i = 0; i < done.Count; i++)
        {
            Press p = done[i];
            int holdMs = Math.Max(1, (int)((p.Up - p.Down) * 1000 / freq));
            bool drag = Math.Abs(p.EndX - p.X) + Math.Abs(p.EndY - p.Y) >= DragThresholdPx;
            bool hold = !drag && holdMs >= HoldThresholdMs;
            points.Add(new MarkerPoint
            {
                X = p.X,
                Y = p.Y,
                Button = p.Button,
                Kind = drag ? ClickKind.Drag : hold ? ClickKind.Hold : ClickKind.Single,
                HoldMs = drag || hold ? holdMs : null,
                EndX = drag ? p.EndX : null,
                EndY = drag ? p.EndY : null,
                // Espera hasta el siguiente clic; el último usa el intervalo general.
                DelayMs = i + 1 < done.Count ? Math.Max(1, (int)((done[i + 1].Down - p.Down) * 1000 / freq)) : null,
            });
        }
        _presses.Clear();
        return points;
    }

    // Referencia estática: si el recolector liberase el delegado, Windows llamaría a memoria inválida.
    private static readonly LowLevelHookProc Proc = HookProc;

    private static nint HookProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var info = (MSLLHOOKSTRUCT*)lParam;
            bool injected = (info->flags & LLMHF_INJECTED) != 0;
            if (!injected || AcceptInjected)
            {
                long now = Stopwatch.GetTimestamp();
                switch ((int)wParam)
                {
                    case WM_LBUTTONDOWN: OnDown(MouseButtonKind.Left, info->pt, now); break;
                    case WM_RBUTTONDOWN: OnDown(MouseButtonKind.Right, info->pt, now); break;
                    case WM_MBUTTONDOWN: OnDown(MouseButtonKind.Middle, info->pt, now); break;
                    case WM_LBUTTONUP: OnUp(MouseButtonKind.Left, info->pt, now); break;
                    case WM_RBUTTONUP: OnUp(MouseButtonKind.Right, info->pt, now); break;
                    case WM_MBUTTONUP: OnUp(MouseButtonKind.Middle, info->pt, now); break;
                }
            }
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static void OnDown(MouseButtonKind button, POINT pt, long now)
    {
        // Los clics sobre la propia app (p. ej. el botón "Terminar") no se graban.
        nint hwnd = WindowFromPoint(pt);
        if (hwnd != 0)
        {
            GetWindowThreadProcessId(GetAncestor(hwnd, GA_ROOT), out uint pid);
            if (pid == (uint)Compat.ProcessId) return;
        }

        var press = new Press { Button = button, X = pt.X, Y = pt.Y, Down = now };
        _open[(int)button] = press;
        _presses.Add(press);
    }

    private static void OnUp(MouseButtonKind button, POINT pt, long now)
    {
        Press? press = _open[(int)button];
        if (press == null) return;
        press.Up = now;
        press.EndX = pt.X;
        press.EndY = pt.Y;
        _open[(int)button] = null;
        _dispatcher?.InvokeAsync(() => ClickRecorded?.Invoke());
    }
}
