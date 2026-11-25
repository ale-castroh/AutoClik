using System.Runtime.InteropServices;

namespace Autoclicker.Engine;

/// <summary>
/// Pulsaciones de teclado con SendInput por código de escaneo (lo que leen los juegos), con el bit
/// de tecla extendida cuando toca. Teclas sin código de escaneo (multimedia): por tecla virtual.
/// </summary>
internal static unsafe class KeyInput
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_SCANCODE = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    /// <summary>La unión de INPUT (teclado o ratón, que es la rama mayor y fija el tamaño).</summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public EngineNative.MOUSEINPUT mi;
    }

    /// <summary>INPUT completo; .NET coloca la unión con la alineación correcta en 32 y 64 bits.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct KINPUT
    {
        public uint type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, KINPUT* inputs, int size);

    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    /// <summary>Teclas que Windows marca como "extendidas" (flechas, Insert, Supr, Inicio, Ctrl/Alt derechos...).</summary>
    private static bool IsExtended(int vk) => vk is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
        or 0x2D or 0x2E or 0x6F or 0x90 or 0xA3 or 0xA5 or 0x5B or 0x5C or 0x5D;

    /// <summary>Pulsar y/o soltar la tecla <paramref name="vk"/>. Con down y up a la vez, una pulsación completa.</summary>
    public static bool Send(int vk, bool down, bool up)
    {
        uint scan = MapVirtualKey((uint)vk, 0); // MAPVK_VK_TO_VSC
        uint flags = IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0;

        KINPUT* inputs = stackalloc KINPUT[2];
        int n = 0;
        if (down) inputs[n++] = Make(vk, scan, flags);
        if (up) inputs[n++] = Make(vk, scan, flags | KEYEVENTF_KEYUP);
        return SendInput((uint)n, inputs, sizeof(KINPUT)) == n;
    }

    private static KINPUT Make(int vk, uint scan, uint flags)
    {
        var input = new KINPUT { type = INPUT_KEYBOARD };
        input.u.ki = scan != 0
            ? new KEYBDINPUT { wScan = (ushort)scan, dwFlags = flags | KEYEVENTF_SCANCODE, dwExtraInfo = MouseInput.Signature }
            : new KEYBDINPUT { wVk = (ushort)vk, dwFlags = flags, dwExtraInfo = MouseInput.Signature };
        return input;
    }
}
