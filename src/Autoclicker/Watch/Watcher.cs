using System.Diagnostics;

namespace Autoclicker.Watch;

/// <summary>
/// Vigila la pantalla en un hilo propio: unas 5 veces por segundo comprueba cada regla activa.
/// Una regla salta cuando su color o su imagen APARECE (no estaba en la revisión anterior y ahora sí):
/// así "cada vez" no repite la acción sin parar mientras siga en pantalla. Al empezar a vigilar,
/// lo que ya está a la vista cuenta como recién aparecido.
/// </summary>
public sealed class Watcher : IDisposable
{
    /// <summary>Milisegundos entre revisiones.</summary>
    public static int IntervalMs { get; set; } = 200;

    private readonly object _gate = new();
    private readonly ManualResetEvent _stop = new(false);
    private Thread? _thread;

    public bool IsRunning { get { lock (_gate) return _thread != null; } }

    /// <summary>Una regla se cumplió: (índice de la regla, punto donde actuar). Desde el hilo del vigilante.</summary>
    public event Action<int, int, int>? Triggered;

    public Exception? LastError { get; private set; }

    public void Start(IReadOnlyList<WatchRule> rules)
    {
        // Si el anterior aún está terminando (se pidió parar), darle un momento
        Thread? previous;
        lock (_gate) previous = _stop.WaitOne(0) ? _thread : null;
        previous?.Join(1000);

        lock (_gate)
        {
            if (_thread != null) return;
            _stop.Reset();
            LastError = null;
            var snapshot = rules.Select(r => r.Clone()).ToList();
            _thread = new Thread(() => Run(snapshot)) { IsBackground = true, Name = "Watcher", Priority = ThreadPriority.BelowNormal };
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
            _stop.Set();
        }
        // La búsqueda mira la señal de parar en cada fila: termina en milisegundos. Por si acaso, no
        // bloquear la ventana más de un segundo (el hilo es de fondo y acabará solo).
        if (t != Thread.CurrentThread) t.Join(1000);
    }

    private sealed class State
    {
        public bool WasVisible;
        public bool Done;           // "una vez": ya saltó
        public PixelImage? Needle;
    }

    private void Run(List<WatchRule> rules)
    {
        try
        {
            var states = rules.Select(r => new State { Needle = r.Kind == WatchKind.Image ? WatchImages.Load(r.Image) : null }).ToList();
            var sw = new Stopwatch();
            do
            {
                sw.Restart();
                (PixelImage Image, int Left, int Top)? desktop = null; // una sola captura completa por revisión
                for (int i = 0; i < rules.Count; i++)
                {
                    WatchRule rule = rules[i];
                    State s = states[i];
                    if (!rule.Enabled || s.Done) continue;

                    if (_stop.WaitOne(0)) return;
                    (int X, int Y)? hit = rule.Kind == WatchKind.Color ? CheckColor(rule) : CheckImage(rule, s.Needle, ref desktop, Stopping);
                    bool visible = hit != null;
                    if (visible && !s.WasVisible)
                    {
                        if (!rule.EveryTime) s.Done = true;
                        Triggered?.Invoke(i, hit!.Value.X, hit.Value.Y);
                    }
                    s.WasVisible = visible;
                }
                int rest = IntervalMs - (int)sw.ElapsedMilliseconds;
                if (_stop.WaitOne(Math.Max(20, rest))) break;
            }
            while (true);
        }
        catch (Exception ex)
        {
            LastError = ex;
        }
        finally
        {
            lock (_gate) _thread = null;
        }
    }

    private static (int X, int Y)? CheckColor(WatchRule rule) =>
        ScreenCapture.Near(ScreenCapture.PixelAt(rule.X, rule.Y), rule.Color, ScreenCapture.ChannelTolerance(rule.Tolerance))
            ? (rule.X, rule.Y) : null;

    /// <summary>Busca la imagen en su zona (o en toda la pantalla) y devuelve su centro.</summary>
    private bool Stopping() => _stop.WaitOne(0);

    private static (int X, int Y)? CheckImage(WatchRule rule, PixelImage? needle, ref (PixelImage Image, int Left, int Top)? desktop, Func<bool> cancel)
    {
        if (needle == null) return null;
        PixelImage hay;
        int left, top;
        if (rule.HasZone)
        {
            hay = ScreenCapture.Capture(rule.ZoneX, rule.ZoneY, rule.ZoneW, rule.ZoneH);
            left = rule.ZoneX;
            top = rule.ZoneY;
        }
        else
        {
            desktop ??= ScreenCapture.CaptureDesktop();
            (hay, left, top) = desktop.Value;
        }
        return ScreenCapture.Find(hay, needle, rule.Tolerance, cancel) is { } at
            ? (left + at.X + needle.Width / 2, top + at.Y + needle.Height / 2) : null;
    }

    public void Dispose()
    {
        Stop();
        _stop.Dispose();
    }
}
