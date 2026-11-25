using System.Text;

namespace Autoclicker.Persistence;

/// <summary>
/// Guarda y carga <see cref="AppData"/> en %AppData%\AutoClik\settings.json.
/// - Escritura atómica (archivo temporal + reemplazo): un corte de luz nunca deja el archivo a medias.
/// - Si el archivo está dañado, se aparta como .bak y se empieza con valores por defecto.
/// - Si aún no existe, se importan los ajustes de versiones anteriores (%AppData%\Autoclicker).
/// </summary>
public sealed class SettingsStore
{
    private const string AppFolder = "AutoClik";
    private const string LegacyFolder = "Autoclicker";

    /// <summary>Carpeta alternativa (argumento --data=...), útil para pruebas.</summary>
    public static string? DirectoryOverride { get; set; }

    public string FilePath { get; }

    private string? LegacyFilePath { get; }

    public SettingsStore(string? directory = null)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        directory ??= DirectoryOverride ?? Path.Combine(appData, AppFolder);
        FilePath = Path.Combine(directory, "settings.json");
        if (DirectoryOverride == null) LegacyFilePath = Path.Combine(appData, LegacyFolder, "settings.json");
    }

    public AppData Load()
    {
        string? source = File.Exists(FilePath) ? FilePath
                       : LegacyFilePath != null && File.Exists(LegacyFilePath) ? LegacyFilePath
                       : null;
        if (source == null) return Fresh();

        try
        {
            return AppData.FromJson(JsonObj.From(MiniJson.Parse(File.ReadAllText(source, Encoding.UTF8))));
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException
                                       or InvalidCastException or OverflowException or IndexOutOfRangeException
                                       or ArgumentException)
        {
            if (source == FilePath)
            {
                try { File.Copy(FilePath, FilePath + ".bak", overwrite: true); } catch { /* sin copia, seguimos */ }
            }
            return Fresh();
        }
    }

    private static AppData Fresh()
    {
        var data = new AppData();
        data.Normalize();
        return data;
    }

    public void Save(AppData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, MiniJson.Serialize(data.ToJson()), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No se pudo guardar (disco lleno, permisos...): la app sigue funcionando.
        }
    }
}
