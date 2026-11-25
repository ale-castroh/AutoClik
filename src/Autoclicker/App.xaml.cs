using System.Windows;
using Autoclicker.Theme;

namespace Autoclicker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // --theme=light | --theme=dark fuerza un tema (útil para pruebas).
        foreach (string arg in e.Args)
        {
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
            // --data=<carpeta> guarda la configuración en otra carpeta (pruebas).
            else if (arg.StartsWith("--data=", StringComparison.OrdinalIgnoreCase))
            {
                Persistence.SettingsStore.DirectoryOverride = arg.Substring("--data=".Length).Trim('"');
            }
        }

        // Una sola copia: si ya hay una abierta, se la trae al frente y esta se cierra.
        if (!SingleInstance.TryAcquire(Persistence.SettingsStore.DirectoryOverride))
        {
            Shutdown();
            return;
        }

        ThemeManager.Apply();
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        SingleInstance.ListenForShow(() => window.Dispatcher.InvokeAsync(window.BringToFront));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstance.Release();
        base.OnExit(e);
    }
}
