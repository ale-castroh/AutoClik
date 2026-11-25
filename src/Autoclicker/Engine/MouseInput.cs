using static Autoclicker.Engine.EngineNative;

namespace Autoclicker.Engine;

/// <summary>Envía clics reales con SendInput.</summary>
internal static unsafe class MouseInput
{
    /// <summary>
    /// Marca que viaja en dwExtraInfo de cada evento inyectado, para poder distinguir
    /// nuestros clics de los del usuario.
    /// </summary>
    public const nint Signature = 0x41434C4B; // "ACLK"

    private static readonly uint OwnProcessId = (uint)Compat.ProcessId;

    /// <summary>
    /// Calcula dónde clicar (cursor, o punto fijo con desvío aleatorio).
    /// Devuelve false si el punto cae sobre la propia app y hay que omitir el clic.
    /// </summary>
    public static bool TryGetTarget(ClickSettings s, Random random, out POINT p)
    {
        if (s.UseFixedPosition)
        {
            p = new POINT { X = s.X, Y = s.Y };
            if (s.RandomOffsetPx > 0)
            {
                p.X += random.Next(-s.RandomOffsetPx, s.RandomOffsetPx + 1);
                p.Y += random.Next(-s.RandomOffsetPx, s.RandomOffsetPx + 1);
            }
        }
        else
        {
            GetCursorPos(out p);
        }

        return !(s.SkipOwnWindows && IsOwnWindowAt(p));
    }

    /// <summary>
    /// Envía pulsar y/o soltar en <paramref name="p"/>. Con down y up a la vez hace el clic
    /// completo (dos veces si es doble). Con <paramref name="restore"/>, tras soltar el cursor
    /// vuelve a ese punto. Todo va en una sola llamada a SendInput.
    /// </summary>
    public static bool Send(ClickSettings s, POINT p, bool down, bool up, POINT? restore = null)
    {
        (uint downFlag, uint upFlag) = s.Button switch
        {
            MouseButtonKind.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            MouseButtonKind.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
        };

        INPUT* inputs = stackalloc INPUT[6];
        int n = 0;

        if (s.UseFixedPosition && down) inputs[n++] = MoveTo(p);

        int times = s.Kind == ClickKind.Double && down && up ? 2 : 1;
        for (int i = 0; i < times; i++)
        {
            if (down) inputs[n++] = Button(downFlag);
            if (up) inputs[n++] = Button(upFlag);
        }

        if (up && restore is { } back) inputs[n++] = MoveTo(back);

        return SendInput((uint)n, inputs, sizeof(INPUT)) == n;
    }

    /// <summary>Mueve el cursor a <paramref name="p"/> (durante un arrastre).</summary>
    public static void MoveCursor(POINT p)
    {
        INPUT move = MoveTo(p);
        SendInput(1, &move, sizeof(INPUT));
    }

    private static INPUT Button(uint flags) => new()
    {
        type = INPUT_MOUSE,
        mi = new MOUSEINPUT { dwFlags = flags, dwExtraInfo = Signature },
    };

    /// <summary>Movimiento absoluto sobre todo el escritorio virtual (multi-monitor).</summary>
    private static INPUT MoveTo(POINT p)
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = Math.Max(GetSystemMetrics(SM_CXVIRTUALSCREEN) - 1, 1);
        int height = Math.Max(GetSystemMetrics(SM_CYVIRTUALSCREEN) - 1, 1);

        return new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dx = (int)Math.Round((p.X - left) * 65535.0 / width),
                dy = (int)Math.Round((p.Y - top) * 65535.0 / height),
                dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                dwExtraInfo = Signature,
            },
        };
    }

    public static bool IsOwnProcessWindow(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == OwnProcessId;
    }

    /// <summary>
    /// ¿Hay una ventana de esta app en el punto? Las ventanas traspasables (WS_EX_TRANSPARENT,
    /// como los marcadores mientras se clica) no cuentan: el clic las atraviesa.
    /// </summary>
    private static bool IsOwnWindowAt(POINT p)
    {
        nint hwnd = WindowFromPoint(p);
        if (hwnd == 0) return false;
        nint root = GetAncestor(hwnd, GA_ROOT);
        GetWindowThreadProcessId(root, out uint pid);
        if (pid != OwnProcessId) return false;
        return ((long)GetWindowLongPtr(root, GWL_EXSTYLE) & WS_EX_TRANSPARENT) == 0;
    }
}
