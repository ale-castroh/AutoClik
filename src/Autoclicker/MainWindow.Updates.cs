using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Autoclicker.Interop;
using Autoclicker.Localization;

namespace Autoclicker;

/// <summary>
/// Buscar actualizaciones (2.7, Ajustes → Acerca de). Apagado por defecto; ver <see cref="UpdateChecker"/> para
/// lo que hace y lo que no (nunca descarga ni instala nada; el aviso solo abre la página de GitHub).
/// </summary>
public partial class MainWindow
{
    private DispatcherTimer? _updateTimer;
    private DateTime? _updateLastCheck;
    private Version? _updateAvailable;
    private bool _updateChecking;

    private void ApplyUpdatePrefs(Persistence.GlobalPrefs p)
    {
        _updateLastCheck = DateTime.TryParse(p.UpdateLastCheck, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null;
        // Una versión nueva vista antes sigue avisando, salvo que ya la tengas
        _updateAvailable = UpdateChecker.ParseVersion(p.UpdateAvailable) is { } v && v > UpdateChecker.Current ? v : null;
        UpdateSwitch.IsChecked = p.UpdateCheck;
    }

    private void InitUpdates()
    {
        // Con la opción encendida: a los pocos segundos de abrir y luego cada hora se mira si ya toca (una vez al día)
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(UpdateChecker.TestFile != null ? 1 : 8) };
        _updateTimer.Tick += (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromHours(1);
            if (UpdateSwitch.IsChecked == true && (_updateLastCheck is not { } last || DateTime.Now - last > TimeSpan.FromHours(24)))
                _ = CheckForUpdates(manual: false);
        };
        _updateTimer.Start();
        RefreshUpdateUi();
    }

    private void Update_Changed(object sender, RoutedEventArgs e)
    {
        ScheduleSave();
        // Al encenderla, comprobar ya (si no se ha hecho hoy)
        if (UpdateSwitch.IsChecked == true && _updateTimer != null
            && (_updateLastCheck is not { } last || DateTime.Now - last > TimeSpan.FromHours(24)))
            _ = CheckForUpdates(manual: false);
    }

    private void CheckUpdatesNow_Click(object sender, RoutedEventArgs e) => _ = CheckForUpdates(manual: true);

    private async Task CheckForUpdates(bool manual)
    {
        if (_updateChecking) return;
        _updateChecking = true;
        UpdateStatusText.Text = Strings.T("UpdateChecking");
        CheckUpdatesButton.IsEnabled = false;

        Version? latest = await UpdateChecker.LatestAsync();

        _updateChecking = false;
        CheckUpdatesButton.IsEnabled = true;
        if (latest == null)
        {
            RefreshUpdateUi();
            UpdateStatusText.Text = Strings.T("UpdateFailed");
            return;
        }
        _updateLastCheck = DateTime.Now;
        bool wasKnown = _updateAvailable == latest;
        _updateAvailable = latest > UpdateChecker.Current ? latest : null;
        RefreshUpdateUi();
        ScheduleSave();
        // Aviso en la bandeja solo la primera vez que se ve esa versión (y no al pulsar "Buscar ahora": ya lo ves)
        if (_updateAvailable != null && !wasKnown && !manual)
            _tray?.ShowBalloon("AutoClik", Strings.F("UpdateAvailable", _updateAvailable.ToString(3)));
    }

    private void RefreshUpdateUi()
    {
        UpdateStatusText.Text = _updateLastCheck is { } last
            ? Strings.F("UpdateLast", last.ToString("g", CultureInfo.CurrentCulture)) + (_updateAvailable == null ? " · " + Strings.T("UpdateUpToDate") : "")
            : Strings.T("UpdateNever");
        UpdateBanner.Visibility = _updateAvailable != null ? Visibility.Visible : Visibility.Collapsed;
        UpdateBannerText.Text = _updateAvailable != null ? Strings.F("UpdateAvailable", _updateAvailable.ToString(3)) : "";
        // Puntito en el engranaje de Ajustes mientras haya una versión nueva
        SettingsButton.Tag = _updateAvailable != null ? "Update" : null;
        System.Windows.Automation.AutomationProperties.SetHelpText(SettingsButton, _updateAvailable != null ? "update" : "");
    }

    /// <summary>Abre SIEMPRE la misma dirección, escrita en el programa (no viene de internet).</summary>
    private void OpenReleases_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(UpdateChecker.ReleasesPage) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ErrorLog.Write("abrir GitHub", ex);
        }
    }
}
