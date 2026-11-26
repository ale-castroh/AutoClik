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
    public static bool TryAcquire(string? scope, bool notifyExisting = true)
    {
        _scope = scope == null ? "main" : StableHash(scope);
        string name = @"Local\AutoClik." + _scope;

        _mutex = new Mutex(initiallyOwned: true, name + ".lock", out bool createdNew);
        if (createdNew)
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".show");
            return true;
        }

        if (!notifyExisting)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
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

    // ================= Órdenes de la línea de comandos (2.6) =================

    private static string _scope = "main";
    private static Thread? _pipeThread;
    private static volatile bool _closing;

    /// <summary>Tubería de esta sesión de Windows y este ámbito; solo el usuario actual puede abrirla.</summary>
    private static string PipeName => $"AutoClik.{System.Diagnostics.Process.GetCurrentProcess().SessionId}.{_scope}.cmd";

    /// <summary>La copia abierta escucha órdenes de otras copias (--play, --start...).</summary>
    public static void ListenForCommands(Action<string> onCommands)
    {
        _pipeThread = new Thread(() =>
        {
            var security = new System.IO.Pipes.PipeSecurity();
            security.AddAccessRule(new System.IO.Pipes.PipeAccessRule(
                System.Security.Principal.WindowsIdentity.GetCurrent().User!,
                System.IO.Pipes.PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow));
            while (!_closing)
            {
                try
                {
                    using var server = new System.IO.Pipes.NamedPipeServerStream(PipeName, System.IO.Pipes.PipeDirection.In, 1,
                        System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.None, 4096, 4096, security);
                    server.WaitForConnection();
                    if (_closing) return;
                    using var reader = new StreamReader(server, System.Text.Encoding.UTF8);
                    string text = reader.ReadToEnd();
                    if (text.Length > 0) onCommands(text);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (_closing) return;
                    ErrorLog.Write("órdenes", ex);
                    Thread.Sleep(500);
                }
            }
        }) { IsBackground = true, Name = "Commands" };
        _pipeThread.Start();
    }

    /// <summary>Pasa las órdenes a la copia abierta. False si no respondió a tiempo.</summary>
    public static bool SendCommands(string text)
    {
        AllowSetForegroundWindow(ASFW_ANY); // por si la orden es --show
        try
        {
            using var client = new System.IO.Pipes.NamedPipeClientStream(".", PipeName, System.IO.Pipes.PipeDirection.Out);
            client.Connect(3000);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void Release()
    {
        _closing = true;
        if (_pipeThread != null)
        {
            // Despertar al hilo que espera una conexión para que termine
            try
            {
                using var wake = new System.IO.Pipes.NamedPipeClientStream(".", PipeName, System.IO.Pipes.PipeDirection.Out);
                wake.Connect(200);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException) { }
        }
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
