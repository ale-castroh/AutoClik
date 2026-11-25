using System.Reflection;
using System.Text;
using Autoclicker.Persistence;

namespace Autoclicker;

/// <summary>
/// Registro de errores inesperados en error.log, junto a settings.json. Nunca lanza
/// excepciones: si no se puede escribir, se ignora. Se limita a ~256 KB (la mitad
/// antigua pasa a error.old.log).
/// </summary>
internal static class ErrorLog
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Gate = new();

    public static string FilePath => Path.Combine(Path.GetDirectoryName(new SettingsStore().FilePath)!, "error.log");

    public static void Write(string context, Exception ex)
    {
        try
        {
            lock (Gate)
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    string old = Path.Combine(Path.GetDirectoryName(path)!, "error.old.log");
                    File.Copy(path, old, overwrite: true);
                    File.Delete(path);
                }

                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
                var text = new StringBuilder()
                    .Append("==== ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    .Append("  AutoClik ").Append(version)
                    .Append("  Windows ").Append(Environment.OSVersion.Version)
                    .Append("  (").Append(context).AppendLine(")")
                    .AppendLine(ex.ToString())
                    .AppendLine();
                File.AppendAllText(path, text.ToString(), new UTF8Encoding(false));
            }
        }
        catch
        {
            // Si ni siquiera se puede registrar, no hay nada más que hacer.
        }
    }
}
