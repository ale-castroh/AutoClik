using System.Diagnostics;
using System.Runtime.InteropServices;
using Autoclicker.Engine;

namespace Autoclicker.Macros;

public enum MacroRepeat { Once, Count, Forever, Time }

public sealed record MacroPlayOptions(double Speed, MacroRepeat Repeat, int Count, TimeSpan TimeLimit);

/// <summary>
/// Reproduce una macro en un hilo dedicado de alta prioridad, sobre una línea de tiempo absoluta
/// (como el motor de clics): cada evento sale en su instante, dividido por la velocidad.
/// - Teclas por código de escaneo (lo que leen los juegos), con el bit de tecla extendida.
/// - Ratón con movimientos absolutos sobre todo el escritorio virtual (varios monitores).
/// - Lleva la cuenta de teclas y botones pulsados y los suelta al terminar cada vuelta y al parar:
///   nunca se queda nada "pegado".
/// </summary>
public sealed unsafe class MacroPlayer : IDisposable
{
    private readonly object _gate = new();
    private readonly ManualResetEvent _stopEvent = new(false);
    private Thread? _thread;
    private volatile bool _stopRequested;
    private long _position;
    private int _loop;

    public bool IsPlaying { get { lock (_gate) return _thread != null; } }

    /// <summary>Vuelta actual (1, 2, ...).</summary>
    public int Loop => Volatile.Read(ref _loop);

    /// <summary>Posición dentro de la vuelta actual, en microsegundos de la macro (sin aplicar la velocidad).</summary>
    public long PositionMicros => Interlocked.Read(ref _position);

    public Exception? LastError { get; private set; }

    /// <summary>Al terminar (true = acabó sola; false = se paró o hubo un error). Desde el hilo del reproductor.</summary>
    public event Action<bool>? Finished;

    public void Play(Macro macro, MacroPlayOptions options)
    {
        lock (_gate)
        {
            if (_thread != null || macro.Events.Count == 0) return;
            _stopRequested = false;
            _stopEvent.Reset();
            LastError = null;
            Interlocked.Exchange(ref _position, 0);
            Volatile.Write(ref _loop, 1);
            var events = macro.Events.ToArray();
            _thread = new Thread(() => Run(events, options)) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "MacroPlayer" };
            _thread.Start();
        }
    }

    public void Stop()
    {
        Thread? t;
        lock (_gate)
        {
            t = _thread;
            if (t == null) return;
            _stopRequested = true;
            _stopEvent.Set();
        }
        if (t != Thread.CurrentThread) t.Join();
    }

    private void Run(MacroEvent[] events, MacroPlayOptions o)
    {
        bool completed = false;
        var pressedKeys = new Dictionary<int, MacroEvent>();
        var pressedButtons = new HashSet<int>();
        try
        {
            using var timer = HighResTimer.Create();
            WaitHandle[] handles = [timer, _stopEvent];
            long freq = Stopwatch.Frequency;
            double speed = Compat.Clamp(o.Speed, 0.1, 20);
            long deadline = o.Repeat == MacroRepeat.Time ? Stopwatch.GetTimestamp() + (long)(o.TimeLimit.TotalSeconds * freq) : long.MaxValue;
            int loops = o.Repeat switch { MacroRepeat.Once => 1, MacroRepeat.Count => Math.Max(1, o.Count), _ => int.MaxValue };

            for (int loop = 1; loop <= loops; loop++)
            {
                Volatile.Write(ref _loop, loop);
                long start = Stopwatch.GetTimestamp();
                foreach (var e in events)
                {
                    long target = start + (long)(e.Micros / speed * freq / 1_000_000);
                    if (target > deadline) target = deadline;
                    if (!WaitUntil(target, timer, handles, freq)) return; // se pidió parar
                    if (Stopwatch.GetTimestamp() >= deadline) { completed = true; return; }
                    Interlocked.Exchange(ref _position, e.Micros);
                    Send(e, pressedKeys, pressedButtons);
                }
                ReleaseAll(pressedKeys, pressedButtons);
                if (Stopwatch.GetTimestamp() >= deadline) break;
            }
            completed = true;
        }
        catch (Exception ex)
        {
            LastError = ex;
        }
        finally
        {
            ReleaseAll(pressedKeys, pressedButtons);
            lock (_gate) _thread = null;
            Finished?.Invoke(completed && LastError == null);
        }
    }

    private bool WaitUntil(long target, HighResTimer timer, WaitHandle[] handles, long freq)
    {
        long spin = freq / 4000; // 0,25 ms finales girando
        while (true)
        {
            if (_stopRequested) return false;
            long remaining = target - Stopwatch.GetTimestamp();
            if (remaining <= 0) return true;
            if (remaining > spin)
            {
                timer.Arm((remaining - spin) * 10_000_000 / freq);
                if (WaitHandle.WaitAny(handles) == 1) return false;
            }
            else
            {
                Thread.SpinWait(20);
            }
        }
    }

    // ================= Envío (SendInput) =================

    private const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_XDOWN = 0x0080, MOUSEEVENTF_XUP = 0x0100;
    private const uint MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_HWHEEL = 0x1000;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_UNICODE = 0x0004, KEYEVENTF_SCANCODE = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public EngineNative.MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT* inputs, int size);

    private static void Send(MacroEvent e, Dictionary<int, MacroEvent> keys, HashSet<int> buttons)
    {
        switch (e.Kind)
        {
            case MacroEventKind.Move:
                MouseInput.MoveCursor(new EngineNative.POINT { X = e.X, Y = e.Y });
                break;
            case MacroEventKind.Down:
                // Primero el ratón a su sitio exacto, luego el botón
                MouseInput.MoveCursor(new EngineNative.POINT { X = e.X, Y = e.Y });
                SendMouse(ButtonFlags(e.Data, down: true), XData(e.Data));
                buttons.Add(e.Data);
                break;
            case MacroEventKind.Up:
                MouseInput.MoveCursor(new EngineNative.POINT { X = e.X, Y = e.Y });
                SendMouse(ButtonFlags(e.Data, down: false), XData(e.Data));
                buttons.Remove(e.Data);
                break;
            case MacroEventKind.Wheel:
                SendMouse(MOUSEEVENTF_WHEEL, (uint)e.Data);
                break;
            case MacroEventKind.HWheel:
                SendMouse(MOUSEEVENTF_HWHEEL, (uint)e.Data);
                break;
            case MacroEventKind.KeyDown:
                SendKey(e, up: false);
                keys[e.Data] = e;
                break;
            case MacroEventKind.KeyUp:
                SendKey(e, up: true);
                keys.Remove(e.Data);
                break;
            case MacroEventKind.TypeChar:
                SendChar((char)e.Data);
                break;
        }
    }

    /// <summary>Suelta todo lo que siga pulsado (teclas y botones).</summary>
    private static void ReleaseAll(Dictionary<int, MacroEvent> keys, HashSet<int> buttons)
    {
        foreach (var key in keys.Values.ToList()) SendKey(key, up: true);
        keys.Clear();
        foreach (int b in buttons.ToList()) SendMouse(ButtonFlags(b, down: false), XData(b));
        buttons.Clear();
    }

    private static uint ButtonFlags(int button, bool down) => (MacroButton)button switch
    {
        MacroButton.Right => down ? EngineNative.MOUSEEVENTF_RIGHTDOWN : EngineNative.MOUSEEVENTF_RIGHTUP,
        MacroButton.Middle => down ? EngineNative.MOUSEEVENTF_MIDDLEDOWN : EngineNative.MOUSEEVENTF_MIDDLEUP,
        MacroButton.X1 or MacroButton.X2 => down ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP,
        _ => down ? EngineNative.MOUSEEVENTF_LEFTDOWN : EngineNative.MOUSEEVENTF_LEFTUP,
    };

    private static uint XData(int button) => (MacroButton)button switch
    {
        MacroButton.X1 => 1,
        MacroButton.X2 => 2,
        _ => 0,
    };

    private static void SendMouse(uint flags, uint data)
    {
        var input = new INPUT { type = INPUT_MOUSE };
        input.u.mi = new EngineNative.MOUSEINPUT { dwFlags = flags, mouseData = data, dwExtraInfo = MouseInput.Signature };
        SendInput(1, &input, sizeof(INPUT));
    }

    /// <summary>Por código de escaneo si lo hay (juegos); si no (teclas multimedia), por tecla virtual.</summary>
    private static void SendKey(MacroEvent e, bool up)
    {
        var input = new INPUT { type = INPUT_KEYBOARD };
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (e.IsExtendedKey) flags |= KEYEVENTF_EXTENDEDKEY;
        if (e.X != 0)
            input.u.ki = new KEYBDINPUT { wScan = (ushort)e.X, dwFlags = flags | KEYEVENTF_SCANCODE, dwExtraInfo = MouseInput.Signature };
        else
            input.u.ki = new KEYBDINPUT { wVk = (ushort)e.Data, dwFlags = flags, dwExtraInfo = MouseInput.Signature };
        SendInput(1, &input, sizeof(INPUT));
    }

    /// <summary>Escribe un carácter tal cual (KEYEVENTF_UNICODE): no depende de la distribución del teclado.</summary>
    private static void SendChar(char c)
    {
        if (c == '\n' || c == '\r')
        {
            // Salto de línea = Intro de verdad (las apps no siempre aceptan el carácter)
            KeyInput.Send(0x0D, down: true, up: true);
            return;
        }
        INPUT* inputs = stackalloc INPUT[2];
        inputs[0] = new INPUT { type = INPUT_KEYBOARD };
        inputs[0].u.ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE, dwExtraInfo = MouseInput.Signature };
        inputs[1] = inputs[0];
        inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
        SendInput(2, inputs, sizeof(INPUT));
    }

    public void Dispose()
    {
        Stop();
        _stopEvent.Dispose();
    }
}
