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
    private nint _lockedWindow;
    private long _clickCount;

    /// <summary>true mientras los clics están en pausa porque la ventana activa no es la fijada.</summary>
    public bool IsPaused => _paused;

    /// <summary>true si aún no hay ventana fijada (se espera a que se active una que no sea esta app).</summary>
    public bool IsWaitingForWindow => _paused && Volatile.Read(ref _lockedWindow) == IntPtr.Zero;

    /// <summary>Margen final que se apura girando en vez de durmiendo.</summary>
    public double SpinMarginMs { get; set; } = 0.25;

    /// <summary>Sustituye el clic real (pruebas). Devuelve true si cuenta como clic.</summary>
    public Func<ClickSettings, bool>? ClickOverride { get; set; }

    public bool IsRunning { get { lock (_gate) return _thread != null; } }

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
            _lockedWindow = 0;
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
                index = (index + 1) % steps.Length;

                if (DoClick(current, random, timer, handles, freq, spinTicks))
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
        catch
        {
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
            Points = null,
        }).ToArray();
    }

    private bool DoClick(ClickSettings s, Random random, HighResTimer timer, WaitHandle[] handles, long freq, long spinTicks)
    {
        if (s.LockToForegroundWindow && !ForegroundAllowed()) return false;
        if (ClickOverride != null) return ClickOverride(s);
        if (!MouseInput.TryGetTarget(s, random, out var p)) return false;

        if (s.Kind != ClickKind.Hold) return MouseInput.Send(s, p, down: true, up: true);

        // Mantener pulsado: si se pide parar durante la pulsación, se suelta enseguida
        // (nunca se deja un botón "atascado").
        MouseInput.Send(s, p, down: true, up: false);
        WaitUntil(Stopwatch.GetTimestamp() + ToTicks(s.HoldDuration, freq), timer, handles, freq, spinTicks);
        MouseInput.Send(s, p, down: false, up: true);
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
