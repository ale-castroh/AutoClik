using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autoclicker.Persistence;

/// <summary>
/// Guarda y carga <see cref="AppData"/> en %AppData%\Autoclicker\settings.json.
/// Escritura atómica (archivo temporal + reemplazo): un corte de luz nunca deja el archivo a medias.
/// Si el archivo está dañado, se aparta como .bak y se empieza con valores por defecto.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Carpeta alternativa (argumento --data=...), útil para pruebas.</summary>
    public static string? DirectoryOverride { get; set; }

    public string FilePath { get; }

    public SettingsStore(string? directory = null)
    {
        directory ??= DirectoryOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autoclicker");
        FilePath = Path.Combine(directory, "settings.json");
    }

    public AppData Load()
    {
        AppData data;
        try
        {
            data = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppData>(File.ReadAllText(FilePath), Options) ?? new AppData()
                : new AppData();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            try { File.Copy(FilePath, FilePath + ".bak", overwrite: true); } catch { /* sin copia, seguimos */ }
            data = new AppData();
        }
        data.Normalize();
        return data;
    }

    public void Save(AppData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(data, Options));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No se pudo guardar (disco lleno, permisos...): la app sigue funcionando.
        }
    }
}
