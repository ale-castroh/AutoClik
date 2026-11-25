using System.Diagnostics;
using Microsoft.Win32;
using Autoclicker.Persistence;

namespace Autoclicker.Interop;

/// <summary>
/// "Iniciar con Windows": una entrada en HKCU\...\Run que abre AutoClik oculto en la bandeja
/// (argumento --tray). No necesita permisos de administrador.
/// Con --data=&lt;carpeta&gt; (pruebas) nunca se toca el registro: se usa un archivo en esa carpeta.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AutoClik";
    public const string TrayArgument = "--tray";

    private static string? TestFile =>
        SettingsStore.DirectoryOverride is { } dir ? Path.Combine(dir, "autostart.txt") : null;

    private static string Command =>
        $"\"{Process.GetCurrentProcess().MainModule!.FileName}\" {TrayArgument}";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                if (TestFile is { } file) return File.Exists(file);
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return false;
            }
        }
    }

    /// <summary>Activa o desactiva el inicio automático. Devuelve false si no se pudo.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            if (TestFile is { } file)
            {
                if (enabled) File.WriteAllText(file, Command);
                else if (File.Exists(file)) File.Delete(file);
                return true;
            }

            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, Command);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            ErrorLog.Write("inicio con Windows", ex);
            return false;
        }
    }

    /// <summary>Si está activado pero el .exe se movió de carpeta, apunta la entrada a la ubicación actual.</summary>
    public static void RefreshPath()
    {
        if (TestFile != null || !IsEnabled) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) as string != Command) Set(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Sin acceso al registro: se deja como está.
        }
    }
}
