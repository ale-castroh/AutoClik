using System.Diagnostics;

namespace Autoclicker.Engine;

public enum StopReason { Requested, Completed, Error }

/// <summary>
/// Motor de clics. Corre en un hilo dedicado de alta prioridad y programa cada clic
/// sobre una línea de tiempo absoluta (Stopwatch), así los errores no se acumulan:
/// a 100 ms se consiguen 10,00 clics/s reales, no 9,8.
///
/// Espera: duerme con un temporizador de alta resolución hasta poco antes del
/// instante objetivo y apura los últimos microsegundos girando (spin), lo que da
/// precisión sub-milisegundo con un uso de CPU mínimo.
/// </summary>
public sealed class ClickEngine : IDisposable
{
    private readonly object _gate = new();
    private readonly ManualResetEvent _stopEvent = new(false);
    private Thread? _thread;
    private volatile bool _stopRequested;
    private volatile bool _paused;
    private volatile bool _ownWindowActive; // acción Tecla con AutoClik delante: se espera a otra ventana
    private nint _lockedWindow;
    private long _clickCount;

    /// <summary>true mientras los clics están en pausa porque la ventana activa no es la fijada.</summary>
    public bool IsPaused => _paused;

    /// <summary>true si aún no hay ventana fijada (se espera a que se active una que no sea esta app).</summary>
    public bool IsWaitingForWindow => (_paused && Volatile.Read(ref _lockedWindow) == IntPtr.Zero) || _ownWindowActive;

    /// <summary>Margen final que se apura girando en vez de durmiendo.</summary>
    public double SpinMarginMs { get; set; } = 0.25;

    /// <summary>Sustituye el clic real (pruebas). Devuelve true si cuenta como clic.</summary>
    public Func<ClickSettings, bool>? ClickOverride { get; set; }

    public bool IsRunning { get { lock (_gate) return _thread != null; } }

    /// <summary>Excepción que paró la última sesión (StopReason.Error); null si no hubo.</summary>
    public Exception? LastError { get; private set; }

    /// <summary>Clics hechos en la sesión actual (o la última).</summary>
    public long ClickCount => Interlocked.Read(ref _clickCount);

    /// <summary>
    /// Se lanza desde el hilo del motor al terminar. Los manejadores de UI deben usar
    /// Dispatcher.InvokeAsync (nunca Invoke) para no bloquearse con Stop().
    /// </summary>
    public event Action<StopReason>? Stopped;

    public void Start(ClickSettings settings)
    {
        lock (_gate)
        {
            if (_thread != null) return;
            _stopRequested = false;
            _paused = false;
            _ownWindowActive = false;
            _lockedWindow = 0;
            LastError = null;
            _stopEvent.Reset();
            Interlocked.Exchange(ref _clickCount, 0);

            _thread = new Thread(() => Run(settings))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "ClickEngine",
            };
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

    private void Run(ClickSettings s)
    {
        var reason = StopReason.Requested;
        try
        {
            using var timer = HighResTimer.Create();
            WaitHandle[] handles = [timer, _stopEvent];

            long freq = Stopwatch.Frequency;
            double period = Math.Max(s.Interval.TotalSeconds, 0.001) * freq;
            double jitter = Compat.Clamp(s.JitterPercent, 0, 90) / 100.0;
            long spinTicks = (long)(SpinMarginMs * freq / 1000.0);
            var random = new Random();

            // Pasos de la secuencia: uno solo (modo normal) o uno por punto (multi-punto),
            // ya resueltos para no crear objetos en el bucle. Se preparan ANTES de fijar
            // la referencia de tiempo para que el primer clic no llegue tarde.
            ClickSettings[] steps = BuildSteps(s);
            long clickLimit = s.RepeatCount > 0 ? (long)s.RepeatCount * steps.Length : 0;
            int index = 0;

            // Retardo de inicio (interrumpible).
            if (s.StartDelay > TimeSpan.Zero &&
                !WaitUntil(Stopwatch.GetTimestamp() + ToTicks(s.StartDelay, freq), timer, handles, freq, spinTicks))
                return;

            double next = Stopwatch.GetTimestamp(); // primer clic inmediato
            long deadline = s.TimeLimit > TimeSpan.Zero ? (long)next + ToTicks(s.TimeLimit, freq) : long.MaxValue;

            while (WaitUntil(Math.Min((long)next, deadline), timer, handles, freq, spinTicks))
            {
                if (Stopwatch.GetTimestamp() >= deadline)
                {
                    reason = StopReason.Completed;
                    break;
                }

                ClickSettings current = steps[index];

                // En pausa (la ventana activa no es la fijada) la secuencia no avanza: al volver
                // se sigue por el mismo punto. Un clic omitido por caer sobre esta app sí avanza
                // (si no, un punto tapado por la ventana bloquearía la secuencia).
                bool paused = current.LockToForegroundWindow && !ForegroundAllowed();
                if (!paused) index = (index + 1) % steps.Length;

                if (!paused && DoClick(current, random, timer, handles, freq, spinTicks))
                {
                    long count = Interlocked.Increment(ref _clickCount);
                    if (clickLimit > 0 && count >= clickLimit)
                    {
                        reason = StopReason.Completed;
                        break;
                    }
                }

                period = Math.Max(current.Interval.TotalSeconds, 0.001) * freq;
                double step = jitter > 0 ? period * (1 + (random.NextDouble() * 2 - 1) * jitter) : period;
                next += step;

                // Si el sistema nos retrasó más de un intervalo, no recuperar en ráfaga.
                long now = Stopwatch.GetTimestamp();
                if (now - next > period) next = now;
            }
        }
        catch (Exception ex)
        {
            LastError = ex;
            reason = StopReason.Error;
        }
        finally
        {
            lock (_gate) _thread = null;
            Stopped?.Invoke(reason);
        }
    }

    /// <summary>
    /// La primera ventana activa que no sea esta app queda fijada (así funciona también si
    /// se pulsó "Iniciar" con el ratón y luego se cambió a la ventana de destino).
    /// </summary>
    private bool ForegroundAllowed()
    {
        nint foreground = EngineNative.GetForegroundWindow();
        if (_lockedWindow == 0)
        {
            if (foreground == 0 || MouseInput.IsOwnProcessWindow(foreground))
            {
                _paused = true;
                return false;
            }
            Volatile.Write(ref _lockedWindow, foreground);
        }

        _paused = foreground != _lockedWindow;
        return !_paused;
    }

    private static ClickSettings[] BuildSteps(ClickSettings s)
    {
        if (s.Points is not { Count: > 0 } points) return [s];

        double speed = Compat.Clamp(s.Speed, 0.1, 10);
        return points.Select(p => s with
        {
            UseFixedPosition = true,
            X = p.X,
            Y = p.Y,
            Button = p.Button ?? s.Button,
            Kind = p.Kind ?? s.Kind,
            Interval = TimeSpan.FromMilliseconds(Math.Max((p.DelayMs ?? s.Interval.TotalMilliseconds) / speed, 1)),
            HoldDuration = TimeSpan.FromMilliseconds(Math.Max((p.HoldMs ?? s.HoldDuration.TotalMilliseconds) / speed, 1)),
            DragToX = p.EndX,
            DragToY = p.EndY,
            Points = null,
        }).ToArray();
    }

    private bool DoClick(ClickSettings s, Random random, HighResTimer timer, WaitHandle[] handles, long freq, long spinTicks)
    {
        if (ClickOverride != null) return ClickOverride(s);
        if (s.Action == ClickAction.Key) return DoKey(s, timer, handles, freq, spinTicks);
        if (!MouseInput.TryGetTarget(s, random, out var p)) return false;
        if (s.Action == ClickAction.Wheel) return MouseInput.SendWheel(s, p, s.WheelDelta);

        // Dónde estaba el cursor, para devolverlo ahí al soltar.
        EngineNative.POINT? restore = null;
        if (s.RestoreCursor && s.UseFixedPosition && EngineNative.GetCursorPos(out var original) != 0)
            restore = original;

        if (s.Kind is ClickKind.Single or ClickKind.Double)
            return MouseInput.Send(s, p, down: true, up: true, restore);

        // Mantener pulsado o arrastrar: si se pide parar durante la pulsación, se suelta
        // enseguida (nunca se deja un botón "atascado").
        MouseInput.Send(s, p, down: true, up: false);
        long start = Stopwatch.GetTimestamp();
        long end = start + ToTicks(s.HoldDuration, freq);

        if (s.Kind == ClickKind.Drag && s.DragToX is int toX && s.DragToY is int toY)
        {
            // El destino se desplaza igual que el origen (desvío aleatorio incluido).
            var from = p;
            var to = new EngineNative.POINT { X = toX + (p.X - s.X), Y = toY + (p.Y - s.Y) };
            int steps = Compat.Clamp((int)(s.HoldDuration.TotalMilliseconds / 10), 1, 500);
            for (int i = 1; i <= steps; i++)
            {
                if (!WaitUntil(start + (end - start) * i / steps, timer, handles, freq, spinTicks)) break;
                p = new EngineNative.POINT
                {
                    X = from.X + (int)Math.Round((to.X - from.X) * (double)i / steps),
                    Y = from.Y + (int)Math.Round((to.Y - from.Y) * (double)i / steps),
                };
                MouseInput.MoveCursor(p);
            }
        }
        else
        {
            WaitUntil(end, timer, handles, freq, spinTicks);
        }

        MouseInput.Send(s, p, down: false, up: true, restore);
        return true;
    }

    /// <summary>
    /// Acción Tecla: simple, doble o mantenida. Nunca con AutoClik delante (escribiría en sus propias
    /// casillas): entonces espera a que el usuario cambie a la ventana de destino.
    /// </summary>
    private bool DoKey(ClickSettings s, HighResTimer timer, WaitHandle[] handles, long freq, long spinTicks)
    {
        _ownWindowActive = MouseInput.IsOwnProcessWindow(EngineNative.GetForegroundWindow());
        if (_ownWindowActive) return false;

        if (s.Kind == ClickKind.Single) return KeyInput.Send(s.KeyVk, down: true, up: true);
        if (s.Kind == ClickKind.Double) return KeyInput.Send(s.KeyVk, true, true) & KeyInput.Send(s.KeyVk, true, true);

        // Mantener: si se pide parar durante la pulsación, se suelta enseguida.
        KeyInput.Send(s.KeyVk, down: true, up: false);
        WaitUntil(Stopwatch.GetTimestamp() + ToTicks(s.HoldDuration, freq), timer, handles, freq, spinTicks);
        KeyInput.Send(s.KeyVk, down: false, up: true);
        return true;
    }

    private static long ToTicks(TimeSpan t, long freq) => (long)(t.TotalSeconds * freq);

    /// <summary>Espera hasta el instante <paramref name="target"/>. false = se pidió parar.</summary>
    private bool WaitUntil(long target, HighResTimer timer, WaitHandle[] handles, long freq, long spinTicks)
    {
        while (true)
        {
            if (_stopRequested) return false;

            long remaining = target - Stopwatch.GetTimestamp();
            if (remaining <= 0) return true;

            if (remaining > spinTicks)
            {
                timer.Arm((remaining - spinTicks) * 10_000_000 / freq);
                if (WaitHandle.WaitAny(handles) == 1) return false;
            }
            else
            {
                Thread.SpinWait(20);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _stopEvent.Dispose();
    }
}
