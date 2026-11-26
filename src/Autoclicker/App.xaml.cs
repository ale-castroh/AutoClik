using System.Windows;
using Autoclicker.Theme;

namespace Autoclicker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        bool startInTray = false;

        // --theme=light | --theme=dark fuerza un tema (útil para pruebas).
        foreach (string arg in e.Args)
        {
            // --tray: arrancar oculto en la bandeja (lo usa "Iniciar con Windows").
            if (arg.Equals(Interop.Autostart.TrayArgument, StringComparison.OrdinalIgnoreCase))
            {
                startInTray = true;
                continue;
            }

            if (arg.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse(arg.Substring("--theme=".Length), ignoreCase: true, out AppTheme forced))
            {
                ThemeManager.Override = forced;
            }
            // --record-injected: la grabadora acepta clics simulados (solo para pruebas automáticas).
            else if (arg.Equals("--record-injected", StringComparison.OrdinalIgnoreCase))
            {
                Input.ClickRecorder.AcceptInjected = true;
                Macros.MacroRecorder.AcceptInjected = true;
            }
            // --antiafk-seconds=N: el anti-AFK cuenta en segundos (solo pruebas automáticas).
            else if (arg.StartsWith("--antiafk-seconds=", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(arg.Substring("--antiafk-seconds=".Length), out int seconds))
            {
                Input.AntiAfk.TestIntervalSeconds = seconds;
            }
            // Solo pruebas automáticas (2.5): "Dentro de" en segundos, apagar/suspender de mentira y
            // cuenta atrás más corta.
            else if (arg.Equals("--schedule-seconds", StringComparison.OrdinalIgnoreCase))
            {
                Scheduling.Schedule.TestSeconds = true;
            }
            else if (arg.Equals("--power-test", StringComparison.OrdinalIgnoreCase))
            {
                Scheduling.Power.TestMode = true;
            }
            else if (arg.StartsWith("--countdown-seconds=", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(arg.Substring("--countdown-seconds=".Length), out int countdown))
            {
                Scheduling.Power.CountdownSeconds = Math.Max(1, countdown);
            }
            // --update-test=<archivo.json>: "Buscar actualizaciones" lee ese archivo en vez de preguntar a GitHub (pruebas).
            else if (arg.StartsWith("--update-test=", StringComparison.OrdinalIgnoreCase))
            {
                Interop.UpdateChecker.TestFile = arg.Substring("--update-test=".Length).Trim('"');
            }
            // --data=<carpeta> guarda la configuración en otra carpeta (pruebas).
            else if (arg.StartsWith("--data=", StringComparison.OrdinalIgnoreCase))
            {
                Persistence.SettingsStore.DirectoryOverride = arg.Substring("--data=".Length).Trim('"');
            }
            // --wait-pid=N: al reiniciar (modo portátil), esperar a que se cierre la copia anterior.
            else if (arg.StartsWith("--wait-pid=", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(arg.Substring("--wait-pid=".Length), out int pid))
            {
                try { using var old = System.Diagnostics.Process.GetProcessById(pid); old.WaitForExit(8000); }
                catch (ArgumentException) { /* ya se cerró */ }
                continue;
            }
            // Al reiniciar se repiten los ajustes de arranque (no las órdenes)
            if (StartupPrefixes.Any(p => arg.StartsWith(p, StringComparison.OrdinalIgnoreCase))) _restartArgs.Add(arg);
        }

        // Órdenes (2.6): --play "Macro", --start, --stop...
        var commands = CommandLine.Parse(e.Args, out bool help, out var badArgs);
        if (help || badArgs.Count > 0)
        {
            string text = Localization.Strings.T("CmdHelp");
            if (badArgs.Count > 0) text = Localization.Strings.F("CmdBadArg", string.Join(" ", badArgs)) + "\n\n" + text;
            MessageBox.Show(text, "AutoClik", MessageBoxButton.OK, badArgs.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Una sola copia: si ya hay una abierta, se la trae al frente y esta se cierra
        // (salvo al arrancar en la bandeja: entonces no se molesta a la que ya está abierta).
        // Con órdenes, se le pasan a esa copia en vez de traerla al frente.
        if (!SingleInstance.TryAcquire(Persistence.SettingsStore.DirectoryOverride, notifyExisting: !startInTray && commands.Count == 0))
        {
            if (commands.Count > 0) SingleInstance.SendCommands(CommandLine.Serialize(commands));
            Shutdown();
            return;
        }
        // No estaba abierto: parar o salir no tienen nada que hacer.
        if (CommandLine.OnlyStopOrExit(commands))
        {
            SingleInstance.Release();
            Shutdown();
            return;
        }
        if (CommandLine.StartsHidden(commands)) startInTray = true;

        // Errores inesperados: se registran en error.log en vez de cerrar la app sin dejar rastro.
        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) ErrorLog.Write("hilo", ex);
        };

        ThemeManager.Apply();
        base.OnStartup(e);

        MainWindow window;
        try
        {
            window = new MainWindow();
        }
        catch (Exception ex)
        {
            // Sin ventana no hay forma de usar ni cerrar AutoClik: avisar y salir, en vez de quedarse invisible
            // (si no, cada vez que se abre el .exe se avisaría a esta copia y no aparecería nada).
            ErrorLog.Write("arranque", ex);
            MessageBox.Show(Localization.Strings.F("StartupError", ErrorLog.FilePath), "AutoClik",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SingleInstance.Release();
            Shutdown();
            return;
        }
        MainWindow = window;
        if (startInTray) new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle(); // atajo y bandeja, sin ventana
        else window.Show();
        SingleInstance.ListenForShow(() => window.Dispatcher.InvokeAsync(window.BringToFront));
        SingleInstance.ListenForCommands(text =>
            window.Dispatcher.InvokeAsync(() => window.RunCommands(CommandLine.Deserialize(text))));
        if (commands.Count > 0)
            window.Dispatcher.InvokeAsync(() => window.RunCommands(commands), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // ================= Reiniciar (modo portátil, 2.6) =================

    private readonly List<string> _restartArgs = [];

    private static readonly string[] StartupPrefixes =
    [
        "--theme=", "--lang=", "--data=", "--record-injected", "--antiafk-seconds=", "--schedule-seconds",
        "--power-test", "--countdown-seconds=", "--update-test=",
    ];

    /// <summary>Abre una copia nueva (que espera a que esta se cierre) con los mismos argumentos de arranque.</summary>
    public void Restart()
    {
        var args = _restartArgs.Where(a => !a.Equals(Interop.Autostart.TrayArgument, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Contains(' ') ? "\"" + a.Replace("\"", "") + "\"" : a)
            .Append("--wait-pid=" + Compat.ProcessId);
        try
        {
            System.Diagnostics.Process.Start(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName, string.Join(" ", args));
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            ErrorLog.Write("reiniciar", ex);
        }
    }

    private bool _errorShown;

    /// <summary>
    /// Error en la interfaz: se registra y la app sigue abierta. El aviso sale una sola vez
    /// por sesión para no encadenar ventanas si el error se repite.
    /// </summary>
    private void OnUiException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorLog.Write("interfaz", e.Exception);
        e.Handled = true;
        if (_errorShown) return;
        _errorShown = true;
        MessageBox.Show(Localization.Strings.F("UnexpectedError", ErrorLog.FilePath), "AutoClik",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstance.Release();
        base.OnExit(e);
    }
}
