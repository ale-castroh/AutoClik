using System.Windows.Media;
using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Interop;

/// <summary>
/// Icono en la bandeja del sistema con Shell_NotifyIcon (sin cargar Windows Forms).
/// Los clics llegan como mensajes a la ventana principal (ver <see cref="HandleMessage"/>).
/// </summary>
internal sealed unsafe class TrayIcon : IDisposable
{
    public const int CallbackMessage = WM_APP + 1;
    private static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");

    private readonly nint _hwnd;
    private nint _icon;
    private string _tooltip = "AutoClik";
    private bool _added;

    public event Action? LeftClick;
    public event Action? RightClick;

    public TrayIcon(nint hwnd) => _hwnd = hwnd;

    /// <summary>Cambia el icono (acento o "activo") y el texto al pasar el ratón.</summary>
    public void Update(Color accent, bool active, string tooltip)
    {
        int size = Math.Max(GetSystemMetrics(SM_CXSMICON), 16);
        nint old = _icon;
        _icon = IconFactory.ToHIcon(IconFactory.Render(size, accent, active));
        _tooltip = tooltip;
        Notify(_added ? NIM_MODIFY : NIM_ADD);
        _added = true;
        if (old != 0) DestroyIcon(old);
    }

    public bool HandleMessage(int msg, nint lParam)
    {
        if (msg == CallbackMessage)
        {
            switch ((int)lParam)
            {
                case WM_LBUTTONUP: LeftClick?.Invoke(); break;
                case WM_RBUTTONUP: RightClick?.Invoke(); break;
            }
            return true;
        }

        // Si el Explorador se reinicia, la bandeja se vacía: hay que volver a añadir el icono.
        if (msg == TaskbarCreated && _added)
        {
            Notify(NIM_ADD);
            return true;
        }
        return false;
    }

    private void Notify(uint message)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
        };
        string tip = _tooltip.Length > 127 ? _tooltip.Substring(0, 127) : _tooltip;
        for (int i = 0; i < tip.Length; i++) data.szTip[i] = tip[i];
        Shell_NotifyIconW(message, &data);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = new NOTIFYICONDATAW { cbSize = (uint)sizeof(NOTIFYICONDATAW), hWnd = _hwnd, uID = 1 };
            Shell_NotifyIconW(NIM_DELETE, &data);
            _added = false;
        }
        if (_icon != 0) DestroyIcon(_icon);
        _icon = 0;
    }
}
