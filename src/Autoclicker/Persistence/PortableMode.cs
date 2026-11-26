namespace Autoclicker.Persistence;

/// <summary>
/// Modo portátil (2.6): los datos (ajustes, macros, imágenes de Vigilar, estadísticas) viven en la carpeta
/// "AutoClik-datos" junto al .exe, para llevar AutoClik en un USB. Activarlo copia los datos allí;
/// desactivarlo los devuelve a la carpeta normal y borra la portátil. En los dos casos AutoClik se reinicia
/// (las rutas se leen una vez al arrancar).
/// </summary>
public static class PortableMode
{
    /// <summary>Lo que se copia: el resto (error.log, temporales) se queda donde está.</summary>
    private static readonly string[] Files = ["settings.json", UsageStats.FileName];
    private static readonly string[] Folders = ["macros", "images"];

    public static bool IsActive => SettingsStore.PortableDirectory != null;

    /// <summary>Crea la carpeta portátil con una copia de los datos. False si no se puede escribir junto al .exe.</summary>
    public static bool Enable()
    {
        string target = SettingsStore.PortablePath;
        try
        {
            Directory.CreateDirectory(target);
            // Comprobar de verdad que se puede escribir (Archivos de programa deja crear la carpeta a veces, pero no archivos)
            string probe = Path.Combine(target, ".probe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            Copy(SettingsStore.NormalDirectory, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            ErrorLog.Write("modo portátil", ex);
            try { if (Directory.Exists(target) && !Directory.EnumerateFileSystemEntries(target).Any()) Directory.Delete(target); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            return false;
        }
    }

    /// <summary>Devuelve los datos a la carpeta normal (sustituye lo que hubiera) y borra la portátil.</summary>
    public static bool Disable()
    {
        string source = SettingsStore.PortablePath;
        try
        {
            string target = SettingsStore.NormalDirectory;
            foreach (string folder in Folders)
            {
                string existing = Path.Combine(target, folder);
                if (Directory.Exists(existing)) Directory.Delete(existing, recursive: true);
            }
            Copy(source, target);
            Directory.Delete(source, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            ErrorLog.Write("salir del modo portátil", ex);
            return false;
        }
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Files)
        {
            string source = Path.Combine(from, file);
            if (File.Exists(source)) File.Copy(source, Path.Combine(to, file), overwrite: true);
        }
        foreach (string folder in Folders)
        {
            string source = Path.Combine(from, folder);
            if (!Directory.Exists(source)) continue;
            string dest = Path.Combine(to, folder);
            Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
    }
}
