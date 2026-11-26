using System.Diagnostics;
using System.Reflection;
using Autoclicker.Persistence;

namespace Autoclicker.Interop;

/// <summary>
/// Accesos directos en el escritorio (2.6): un .lnk que abre AutoClik con una orden (--play "Macro"...).
/// Con --data (pruebas) se crean en &lt;carpeta&gt;\desktop, nunca en el escritorio de verdad.
/// </summary>
internal static class Shortcut
{
    public static string Folder =>
        SettingsStore.DirectoryOverride is { } dir ? Path.Combine(dir, "desktop")
        : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    /// <summary>Crea (o sustituye) el acceso directo. Devuelve su ruta, o null si no se pudo.</summary>
    public static string? Create(string title, string arguments, string description)
    {
        try
        {
            string exe = Process.GetCurrentProcess().MainModule!.FileName;
            // Las pruebas también llevan su carpeta de datos, para no tocar nunca la configuración real
            if (SettingsStore.DirectoryOverride is { } data) arguments = $"\"--data={data}\" " + arguments;

            Directory.CreateDirectory(Folder);
            string clean = new(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            string path = Path.Combine(Folder, clean.Trim() + ".lnk");

            // WScript.Shell viene con Windows: sin referencias COM en el proyecto
            Type shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
            object shell = Activator.CreateInstance(shellType)!;
            try
            {
                object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [path])!;
                Type linkType = link.GetType();
                void Set(string name, object value) => linkType.InvokeMember(name, BindingFlags.SetProperty, null, link, [value]);
                Set("TargetPath", exe);
                Set("Arguments", arguments);
                Set("WorkingDirectory", Path.GetDirectoryName(exe)!);
                Set("IconLocation", exe + ",0");
                Set("Description", description);
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException
                                       or TargetInvocationException or TypeLoadException or ArgumentException)
        {
            ErrorLog.Write("acceso directo", ex);
            return null;
        }
    }

    /// <summary>Entre comillas para la línea de comandos (las comillas del nombre se quitan).</summary>
    public static string Quote(string value) => "\"" + value.Replace("\"", "") + "\"";
}
