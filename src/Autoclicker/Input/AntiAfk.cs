using System.Diagnostics;
using System.Windows.Threading;
using Autoclicker.Engine;
using Autoclicker.Localization;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Input;

/// <summary>
/// Anti-AFK (experimental). Cada cierto tiempo trae al frente la ventana elegida (p. ej. Roblox),
/// hace UN clic real en el punto elegido y devuelve la ventana activa y el cursor a donde estaban
/// (~150 ms). Los juegos como Roblox ignoran los clics enviados a una ventana en segundo plano,
/// por eso hace falta activarla un instante.
///
/// - Si el usuario ya está en esa ventana, no hace nada (ya está activo).
/// - Si está usando el PC, espera a que lleve 1,5 s sin tocar nada (máx. 60 s) para no robarle teclas.
/// - Se pospone mientras el autoclicker clica, se graba o se elige un punto.
/// </summary>
public sealed class AntiAfk
{
    private static readonly TimeSpan IdleNeeded = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan MaxIdleWait = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetrySoon = TimeSpan.FromSeconds(10);

    private readonly DispatcherTimer _timer;
    private bool _enabled;
    private bool _busy;
    private DateTime _next;
    private DateTime? _idleWaitStarted;
    private string _status = "";
    private string _countdown = "";
    private string _detail = "";
    private string? _transient; // aviso puntual con el anti-AFK apagado (p. ej. "Clic de prueba hecho")
    private string? _situation; // clave de texto del estado puntual, o null = cuenta atrás

    /// <summary>Solo pruebas: intervalo en segundos en vez de minutos (--antiafk-seconds=N).</summary>
    public static int? TestIntervalSeconds { get; set; }

    /// <summary>Nombre del proceso de la ventana elegida (p. ej. RobloxPlayerBeta).</summary>
    public string? ProcessName { get; private set; }

    /// <summary>Nombre para mostrar (título de la ventana o, si no tiene, el proceso).</summary>
    public string? DisplayName { get; private set; }

    /// <summary>Punto de clic en coordenadas de cliente (físicas) de esa ventana.</summary>
    public int X { get; private set; }
    public int Y { get; private set; }

    public bool IsConfigured => ProcessName != null;

    public TimeSpan Interval { get; private set; } = TimeSpan.FromMinutes(15);

    /// <summary>true = posponer (el autoclicker está clicando, se graba, etc.).</summary>
    public Func<bool>? ShouldWait { get; set; }

    /// <summary>Línea de estado (p. ej. "Activo en Roblox"). Cambia como mucho una vez por segundo.</summary>
    public string Status => _status;

    /// <summary>Cuenta atrás hasta el próximo clic, "mm:ss" (apagado: el intervalo elegido).</summary>
    public string Countdown => _countdown;

    public event Action? StatusChanged;

    /// <summary>Línea secundaria: último clic y cuántos van en esta sesión.</summary>
    public string Detail => _detail;

    /// <summary>Clics hechos desde que se abrió la app.</summary>
    public int PulseCount { get; private set; }

    /// <summary>Cuándo se hizo el último clic (null = ninguno todavía).</summary>
    public DateTime? LastPulse { get; private set; }

    public AntiAfk()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
    }

    public void SetTarget(string? processName, string? displayName, int x, int y)
    {
        ProcessName = string.IsNullOrWhiteSpace(processName) ? null : processName;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? ProcessName : displayName;
        X = x;
        Y = y;
        Restart();
    }

    /// <summary>true = pulsar una tecla (KeyVk) en vez de clicar en el punto (2.2).</summary>
    public bool UseKey { get; private set; }

    /// <summary>Tecla que se pulsa con UseKey (por defecto Espacio: saltar en Roblox).</summary>
    public int KeyVk { get; private set; } = 0x20;

    public void SetUseKey(bool useKey) => UseKey = useKey;

    public void SetKey(int vk) => KeyVk = vk;

    public void SetMinutes(int minutes)
    {
        var interval = TestIntervalSeconds is int s
            ? TimeSpan.FromSeconds(Math.Max(s, 1))
            : TimeSpan.FromMinutes(Compat.Clamp(minutes, 1, 18));
        if (interval == Interval) return;
        Interval = interval;
        Restart();
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            _transient = null;
            if (value) { Restart(); _timer.Start(); }
            else _timer.Stop();
            Tick();
        }
    }

    /// <summary>Vuelve a contar el intervalo completo desde ahora.</summary>
    private void Restart()
    {
        _next = DateTime.Now + Interval;
        _idleWaitStarted = null;
        _situation = null;
        Tick(); // también apagado: así se ven ya el intervalo y la explicación
    }

    private void Tick()
    {
        if (_busy) return;
        if (!_enabled) { Show(_transient ?? Strings.T("AfkStateOff"), Clock(Interval)); return; }
        if (ProcessName == null) { Show(Strings.T("AfkNotConfigured"), "--:--"); return; }

        DateTime now = DateTime.Now;
        if (now < _next) { Show(StateText(), Clock(_next - now)); return; }

        if (ShouldWait?.Invoke() == true) { Postpone(now, "AfkPaused"); return; }

        nint target = FindWindow(ProcessName);
        if (target == 0) { Postpone(now, "AfkMissing"); return; }

        // El usuario está en esa ventana: ya cuenta como actividad.
        if (GetForegroundWindow() == target)
        {
            _next = now + Interval;
            _situation = "AfkInWindow";
            Show(StateText(), Clock(Interval));
            return;
        }

        // Está usando el PC: esperar a una pausa (como mucho 60 s) para no robarle teclas.
        if (IdleTime() < IdleNeeded)
        {
            _idleWaitStarted ??= now;
            if (now - _idleWaitStarted < MaxIdleWait)
            {
                Show(Strings.T("AfkWaitingIdle"), Clock(TimeSpan.Zero));
                return;
            }
        }
        _idleWaitStarted = null;
        StartPulse(target);
    }

    /// <summary>
    /// "Probar ahora": un clic inmediato, sin esperar al intervalo ni a que el PC esté quieto
    /// (lo acaba de pedir el usuario). Si el anti-AFK está activo, la cuenta empieza de nuevo.
    /// </summary>
    public void PulseNow()
    {
        if (_busy) return;
        if (ProcessName == null) { Show(Strings.T("AfkNotConfigured"), _countdown); return; }
        nint target = FindWindow(ProcessName);
        if (target == 0) { Show(Strings.F("AfkMissing", DisplayName ?? ""), _countdown); return; }
        StartPulse(target);
    }

    /// <summary>Hace el clic fuera del hilo de la interfaz y actualiza el estado al terminar.</summary>
    private void StartPulse(nint target)
    {
        _busy = true;
        int x = X, y = Y;
        int? key = UseKey ? KeyVk : null;
        var dispatcher = Dispatcher.CurrentDispatcher;
        Task.Run(() => Pulse(target, x, y, key)).ContinueWith(t =>
        {
            dispatcher.InvokeAsync(() =>
            {
                _busy = false;
                bool ok = t.Status == TaskStatus.RanToCompletion && t.Result;
                if (t.Exception != null) ErrorLog.Write("anti-AFK", t.Exception.GetBaseException());
                if (ok)
                {
                    PulseCount++;
                    LastPulse = DateTime.Now;
                    _next = DateTime.Now + Interval;
                    _situation = null;
                }
                if (!_enabled) { _transient = Strings.T(ok ? "AfkTestDone" : "AfkFailed"); Tick(); return; }
                if (!ok) { Postpone(DateTime.Now, "AfkFailed"); return; }
                Tick();
            });
        });
    }

    private void Postpone(DateTime now, string situation)
    {
        _next = now + (Interval < RetrySoon ? Interval : RetrySoon);
        _situation = situation;
        Show(StateText(), Clock(_next - now));
    }

    /// <summary>Estado con el anti-AFK activo: normal o la situación puntual (no está abierto, etc.).</summary>
    private string StateText() => _situation switch
    {
        null => Strings.F("AfkStateActive", DisplayName ?? ""),
        "AfkMissing" => Strings.F("AfkMissing", DisplayName ?? ""),
        "AfkInWindow" => Strings.F("AfkInWindow", DisplayName ?? ""),
        _ => Strings.T(_situation),
    };

    private static string Clock(TimeSpan t)
    {
        int seconds = Math.Max(0, (int)Math.Ceiling(t.TotalSeconds));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private void Show(string state, string countdown)
    {
        string detail = LastPulse is not { } last ? Strings.T(_enabled ? "AfkNever" : "AfkOffHint")
            : (DateTime.Now - last).TotalMinutes < 1 ? Strings.F("AfkLastJust", PulseCount)
            : Strings.F("AfkLast", (int)(DateTime.Now - last).TotalMinutes, PulseCount);
        if (state == _status && countdown == _countdown && detail == _detail) return;
        _status = state;
        _countdown = countdown;
        _detail = detail;
        StatusChanged?.Invoke();
    }

    // ================= Win32 =================

    private static TimeSpan IdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = 8 };
        if (GetLastInputInfo(ref info) == 0) return TimeSpan.MaxValue;
        return TimeSpan.FromMilliseconds(unchecked(GetTickCount() - info.dwTime));
    }

    /// <summary>La ventana visible más grande del proceso (los juegos suelen tener una sola).</summary>
    public static nint FindWindow(string processName)
    {
        var pids = new HashSet<uint>();
        foreach (var p in Process.GetProcessesByName(processName))
        {
            pids.Add((uint)p.Id);
            p.Dispose();
        }
        if (pids.Count == 0) return 0;

        nint best = 0;
        long bestArea = 0;
        EnumWindows((hwnd, _) =>
        {
            if (IsWindowVisible(hwnd) == 0) return true;
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (!pids.Contains(pid)) return true;
            GetWindowRect(hwnd, out RECT r);
            long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
            if (area > bestArea) { best = hwnd; bestArea = area; }
            return true;
        }, 0);
        return best;
    }

    /// <summary>Pone <paramref name="hwnd"/> en primer plano uniendo un momento la entrada con la ventana activa.</summary>
    private static bool Activate(nint hwnd)
    {
        nint foreground = GetForegroundWindow();
        uint me = GetCurrentThreadId();
        uint other = foreground == 0 ? 0 : GetWindowThreadProcessId(foreground, out _);
        bool attached = other != 0 && other != me && AttachThreadInput(me, other, 1) != 0;
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
        if (attached) AttachThreadInput(me, other, 0);

        var sw = Stopwatch.StartNew();
        while (GetForegroundWindow() != hwnd && sw.ElapsedMilliseconds < 300) Thread.Sleep(5);
        return GetForegroundWindow() == hwnd;
    }

    private static readonly ClickSettings ClickOnly = new() { Button = MouseButtonKind.Left, Kind = ClickKind.Single };

    /// <summary>Activa, clica y devuelve todo. Corre fuera del hilo de la interfaz. true = clic hecho.</summary>
    /// <param name="key">Tecla a pulsar en vez de clicar (null = clic en el punto).</param>
    private static bool Pulse(nint target, int clientX, int clientY, int? key)
    {
        nint previous = GetForegroundWindow();
        GetCursorPos(out POINT cursor);
        bool wasMinimized = IsIconic(target) != 0;
        if (wasMinimized) ShowWindow(target, SW_RESTORE);

        bool front = Activate(target);
        if (front && key is int vk)
        {
            // Una pulsación algo larga (60 ms): los juegos leen el teclado a su ritmo y una muy
            // corta podría no contar.
            Thread.Sleep(50);
            KeyInput.Send(vk, down: true, up: false);
            Thread.Sleep(60);
            KeyInput.Send(vk, down: false, up: true);
            Thread.Sleep(50);
        }
        else if (front)
        {
            var p = new POINT { X = clientX, Y = clientY };
            ClientToScreen(target, ref p);
            var at = new EngineNative.POINT { X = p.X, Y = p.Y };

            // Con pequeñas pausas, como un clic de verdad: algunos juegos necesitan ver
            // el ratón encima antes de pulsar.
            Thread.Sleep(50);
            MouseInput.MoveCursor(at);
            Thread.Sleep(15);
            MouseInput.Send(ClickOnly, at, down: true, up: false);
            Thread.Sleep(25);
            MouseInput.Send(ClickOnly, at, down: false, up: true);
            Thread.Sleep(50);
        }

        if (wasMinimized) ShowWindow(target, SW_MINIMIZE);
        if (previous != 0 && previous != target && IsWindow(previous) != 0) Activate(previous);
        MouseInput.MoveCursor(new EngineNative.POINT { X = cursor.X, Y = cursor.Y });
        return front;
    }
}
