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
            }
            // --antiafk-seconds=N: el anti-AFK cuenta en segundos (solo pruebas automáticas).
            else if (arg.StartsWith("--antiafk-seconds=", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(arg.Substring("--antiafk-seconds=".Length), out int seconds))
            {
                Input.AntiAfk.TestIntervalSeconds = seconds;
            }
            // --data=<carpeta> guarda la configuración en otra carpeta (pruebas).
            else if (arg.StartsWith("--data=", StringComparison.OrdinalIgnoreCase))
            {
                Persistence.SettingsStore.DirectoryOverride = arg.Substring("--data=".Length).Trim('"');
            }
        }

        // Una sola copia: si ya hay una abierta, se la trae al frente y esta se cierra
        // (salvo al arrancar en la bandeja: entonces no se molesta a la que ya está abierta).
        if (!SingleInstance.TryAcquire(Persistence.SettingsStore.DirectoryOverride, notifyExisting: !startInTray))
        {
            Shutdown();
            return;
        }

        // Errores inesperados: se registran en error.log en vez de cerrar la app sin dejar rastro.
        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) ErrorLog.Write("hilo", ex);
        };

        ThemeManager.Apply();
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        if (startInTray) new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle(); // atajo y bandeja, sin ventana
        else window.Show();
        SingleInstance.ListenForShow(() => window.Dispatcher.InvokeAsync(window.BringToFront));
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
