using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Autoclicker.Engine;

// Uso:
//   Autoclicker.Bench timing   -> precisión del intervalo (no hace clics reales)
//   Autoclicker.Bench clicks   -> clics reales sobre una ventana de prueba (mueve el cursor ~3 s)
string mode = args.Length > 0 ? args[0] : "timing";
if (mode == "timing") Timing();
else if (mode == "clicks") RealClicks();
else if (mode == "target") Target(int.Parse(args[1]));

// Ventana diana: cuenta los clics izquierdos que recibe durante N segundos (pruebas de la app completa).
static void Target(int seconds)
{
    var form = new Form
    {
        Text = "Bench target", FormBorderStyle = FormBorderStyle.None, TopMost = true,
        StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(200, 200, 240, 240),
        BackColor = Color.MediumPurple, ShowInTaskbar = false,
    };
    // Dibujo reconocible para las pruebas de "Vigilar" (2.4): cuadrado amarillo de 30 px con el centro
    // negro, en (160, 20) de la ventana; en pantalla, (360, 220)-(390, 250).
    form.Paint += (_, e) =>
    {
        e.Graphics.FillRectangle(Brushes.Gold, 160, 20, 30, 30);
        e.Graphics.FillRectangle(Brushes.Black, 170, 30, 10, 10);
    };
    int left = 0, wheel = 0;
    var hits = new List<string>();
    var keys = new List<string>();
    form.MouseDown += (_, e) =>
    {
        if (e.Button == MouseButtons.Left) left++;
        // Posición redondeada a 20 px para agrupar, con el botón.
        hits.Add($"({e.X / 20 * 20},{e.Y / 20 * 20}) {e.Button}");
    };
    // Teclas y rueda (pruebas de macros)
    form.KeyPreview = true;
    // Los caracteres escritos con "Escribir texto" (2.3) llegan como Packet + KeyPress
    var typed = new System.Text.StringBuilder();
    form.KeyDown += (_, e) => { if (e.KeyCode != Keys.Packet) keys.Add(e.KeyCode.ToString()); };
    form.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar)) typed.Append(e.KeyChar); };
    form.MouseWheel += (_, e) => wheel += e.Delta / 120;
    form.Shown += async (_, _) =>
    {
        await Task.Delay(seconds * 1000);
        Console.WriteLine($"Diana: {left} clics izquierdos");
        foreach (var g in hits.GroupBy(h => h)) Console.WriteLine($"  {g.Key} x{g.Count()}");
        Console.WriteLine($"  orden: {string.Join(" -> ", hits.Take(6))}");
        if (keys.Count > 0) Console.WriteLine($"  teclas: {string.Join(" ", keys)}");
        if (wheel != 0) Console.WriteLine($"  rueda: {wheel} pasos");
        if (typed.Length > 0) Console.WriteLine($"  texto: {typed}");
        form.Close();
    };
    Application.Run(form);
}

static void Timing()
{
    Console.WriteLine("Intervalo | pedido c/s | real c/s | error medio | p99 error | CPU | primer clic");
    foreach (double ms in new[] { 1.0, 2, 5, 10, 16, 50, 100 })
    {
        int clicks = (int)Math.Clamp(2000 / ms, 20, 1500);
        var stamps = new long[clicks];
        int i = 0;

        using var engine = new ClickEngine
        {
            ClickOverride = _ => { stamps[i++] = Stopwatch.GetTimestamp(); return true; },
        };
        var done = new ManualResetEventSlim();
        engine.Stopped += _ => done.Set();

        var proc = Process.GetCurrentProcess();
        TimeSpan cpu0 = proc.TotalProcessorTime;
        long t0 = Stopwatch.GetTimestamp();

        engine.Start(new ClickSettings { Interval = TimeSpan.FromMilliseconds(ms), RepeatCount = clicks });
        done.Wait();

        double wall = Stopwatch.GetElapsedTime(t0).TotalSeconds;
        proc.Refresh();
        double cpuPct = (proc.TotalProcessorTime - cpu0).TotalSeconds / wall * 100;

        // Error de cada clic respecto a su instante ideal. La referencia es el 2.º clic:
        // el 1.º puede llegar algo tarde por el calentamiento del código (se informa aparte).
        double tick = 1000.0 / Stopwatch.Frequency;
        var errors = new double[clicks - 1];
        for (int k = 1; k < clicks; k++)
            errors[k - 1] = Math.Abs((stamps[k] - stamps[1]) * tick - (k - 1) * ms);
        Array.Sort(errors);
        double firstLate = Math.Max(0, ms - (stamps[1] - stamps[0]) * tick);

        double realRate = (clicks - 2) / ((stamps[^1] - stamps[1]) * tick / 1000.0);
        Console.WriteLine($"{ms,6} ms | {1000 / ms,10:F1} | {realRate,8:F2} | {errors.Average(),8:F3} ms | {errors[(int)(errors.Length * 0.99)],6:F3} ms | {cpuPct,4:F1}% | 1.er clic +{firstLate:F2} ms");
    }
}

static void RealClicks()
{
    var form = new Form
    {
        Text = "Bench target", FormBorderStyle = FormBorderStyle.None, TopMost = true,
        StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(200, 200, 240, 240),
        BackColor = Color.MediumPurple, ShowInTaskbar = false,
    };
    int left = 0, right = 0, middle = 0, doubles = 0;
    form.MouseDown += (_, e) =>
    {
        if (e.Button == MouseButtons.Left) left++;
        if (e.Button == MouseButtons.Right) right++;
        if (e.Button == MouseButtons.Middle) middle++;
    };
    form.MouseDoubleClick += (_, _) => doubles++;

    Point original = Cursor.Position;
    form.Shown += async (_, _) =>
    {
        await Task.Delay(300);
        var center = form.PointToScreen(new Point(120, 120));
        var baseSettings = new ClickSettings
        {
            UseFixedPosition = true, X = center.X, Y = center.Y, SkipOwnWindows = false,
            Interval = TimeSpan.FromMilliseconds(5),
        };

        async Task RunAsync(ClickSettings s)
        {
            using var engine = new ClickEngine();
            var tcs = new TaskCompletionSource();
            engine.Stopped += _ => tcs.TrySetResult();
            engine.Start(s);
            await tcs.Task;
            await Task.Delay(500); // deja procesar la cola de mensajes antes de contar
        }

        await RunAsync(baseSettings with { Button = MouseButtonKind.Left, RepeatCount = 200 });
        await RunAsync(baseSettings with { Button = MouseButtonKind.Right, RepeatCount = 100 });
        await RunAsync(baseSettings with { Button = MouseButtonKind.Middle, RepeatCount = 100 });
        int leftBefore = left;
        int doublesBefore = doubles; // los clics rápidos anteriores también cuentan como dobles
        await RunAsync(baseSettings with { Button = MouseButtonKind.Left, Kind = ClickKind.Double, RepeatCount = 20, Interval = TimeSpan.FromMilliseconds(600) });

        Console.WriteLine($"Izquierdo: {leftBefore}/200  Derecho: {right}/100  Central: {middle}/100");
        Console.WriteLine($"Doble clic: {left - leftBefore}/40 pulsaciones, {doubles - doublesBefore}/20 dobles clics detectados");
        Cursor.Position = original;
        form.Close();
    };
    Application.Run(form);
}
