using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker;

/// <summary>
/// Garantiza una sola copia de AutoClik por usuario. Si se abre otra vez, la copia nueva avisa
/// a la que ya está abierta (que se muestra y se pone delante de todo) y se cierra sola.
/// </summary>
internal static class SingleInstance
{
    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;
    private static RegisteredWaitHandle? _wait;

    /// <param name="scope">
    /// Nombre del "ámbito": la carpeta de datos (--data) separa las copias de prueba de la normal.
    /// </param>
    /// <returns>true si esta es la primera copia; false si ya había una (y ya se le ha avisado).</returns>
    public static bool TryAcquire(string? scope)
    {
        string name = @"Local\AutoClik." + (scope == null ? "main" : StableHash(scope));

        _mutex = new Mutex(initiallyOwned: true, name + ".lock", out bool createdNew);
        if (createdNew)
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".show");
            return true;
        }

        // Ya hay una copia: darle permiso para ponerse en primer plano (esta copia lo tiene,
        // porque la acaba de abrir el usuario) y avisarla.
        AllowSetForegroundWindow(ASFW_ANY);
        try
        {
            using var show = EventWaitHandle.OpenExisting(name + ".show");
            show.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // La otra copia aún está arrancando o cerrándose: no hay nada que mostrar.
        }
        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    /// <summary>Llama a <paramref name="onShow"/> (en un hilo del sistema) cada vez que otra copia pide mostrarse.</summary>
    public static void ListenForShow(Action onShow)
    {
        if (_showEvent == null) return;
        _wait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => onShow(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public static void Release()
    {
        _wait?.Unregister(null);
        _showEvent?.Dispose();
        try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { /* no era nuestro */ }
        _mutex?.Dispose();
    }

    /// <summary>Hash estable entre procesos (string.GetHashCode no lo es en todas las versiones de .NET).</summary>
    private static string StableHash(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text.ToLowerInvariant())
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash.ToString("x8");
    }
}
