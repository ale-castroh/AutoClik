using System.Text;
using Autoclicker.Persistence;

namespace Autoclicker.Macros;

/// <summary>
/// Biblioteca de macros: un archivo .json por macro en la carpeta "macros", junto a settings.json
/// (%AppData%\AutoClik\macros, o la carpeta de --data). Escritura atómica, como los ajustes.
/// Exportar/importar usa el mismo formato con la extensión .autoclik.
/// </summary>
public sealed class MacroStore
{
    public const string ExportExtension = ".autoclik";

    public string Folder { get; } = Path.Combine(Path.GetDirectoryName(new SettingsStore().FilePath)!, "macros");

    /// <summary>Todas las macros, por nombre. Las que estén dañadas se saltan (y se registran).</summary>
    public List<Macro> LoadAll()
    {
        var list = new List<Macro>();
        if (!Directory.Exists(Folder)) return list;
        foreach (string file in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                var macro = Macro.FromJson(File.ReadAllText(file, Encoding.UTF8));
                if (macro.Name.Length == 0) macro.Name = Path.GetFileNameWithoutExtension(file);
                macro.FilePath = file;
                list.Add(macro);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException
                                           or InvalidCastException or OverflowException)
            {
                ErrorLog.Write("macro dañada: " + Path.GetFileName(file), ex);
            }
        }
        return list.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Guarda (o vuelve a guardar) la macro en su archivo.</summary>
    public bool Save(Macro macro)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string path = macro.FilePath ?? FreePath(macro.Name);
            WriteAtomic(path, macro.ToJson());
            macro.FilePath = path;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write("guardar macro", ex);
            return false;
        }
    }

    public void Delete(Macro macro)
    {
        if (macro.FilePath == null) return;
        try { File.Delete(macro.FilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ErrorLog.Write("borrar macro", ex); }
        macro.FilePath = null;
    }

    /// <summary>Cambia el nombre (y el archivo, para que se reconozca en la carpeta).</summary>
    public bool Rename(Macro macro, string newName)
    {
        string? old = macro.FilePath;
        string oldName = macro.Name;
        macro.Name = newName;
        macro.FilePath = null;
        if (!Save(macro))
        {
            macro.Name = oldName;
            macro.FilePath = old;
            return false;
        }
        if (old != null && !string.Equals(old, macro.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(old); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return true;
    }

    public void Export(Macro macro, string path) => WriteAtomic(path, macro.ToJson());

    /// <summary>Lee una macro exportada. Lanza FormatException si el archivo no es válido.</summary>
    public static Macro Import(string path) => Macro.FromJson(File.ReadAllText(path, Encoding.UTF8));

    /// <summary>"Macro 1", "Macro 2"... o "Nombre (2)" si ya existe.</summary>
    public static string UniqueName(IEnumerable<Macro> existing, string wanted)
    {
        var names = new HashSet<string>(existing.Select(m => m.Name), StringComparer.CurrentCultureIgnoreCase);
        if (!names.Contains(wanted)) return wanted;
        for (int i = 2; ; i++)
        {
            string candidate = $"{wanted} ({i})";
            if (!names.Contains(candidate)) return candidate;
        }
    }

    /// <summary>Ruta libre para el nombre (sin caracteres no válidos en Windows).</summary>
    private string FreePath(string name)
    {
        var clean = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        if (clean.Length == 0) clean = "macro";
        if (clean.Length > 60) clean = clean.Substring(0, 60);
        string path = Path.Combine(Folder, clean + ".json");
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(Folder, $"{clean} ({i}).json");
        return path;
    }

    private static void WriteAtomic(string path, string text)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
    }
}
