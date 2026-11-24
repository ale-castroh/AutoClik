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
                && Enum.TryParse(arg["--theme=".Length..], ignoreCase: true, out AppTheme forced))
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
                Persistence.SettingsStore.DirectoryOverride = arg["--data=".Length..].Trim('"');
            }
        }

        ThemeManager.Apply();
        base.OnStartup(e);
    }
}
