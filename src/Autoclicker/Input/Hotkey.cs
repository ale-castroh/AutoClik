using static Autoclicker.Interop.NativeMethods;

namespace Autoclicker.Input;

/// <summary>Un atajo: tecla virtual (o botón lateral del ratón) + modificadores MOD_*.</summary>
public readonly record struct Hotkey(int VirtualKey, uint Modifiers)
{
    public static readonly Hotkey Default = new(VK_F6, 0);

    public bool IsMouse => VirtualKey is VK_XBUTTON1 or VK_XBUTTON2;

    public override string ToString()
    {
        var parts = new List<string>(5);
        if ((Modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((Modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((Modifiers & MOD_SHIFT) != 0) parts.Add("Mayús");
        if ((Modifiers & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(KeyName(VirtualKey));
        return string.Join(" + ", parts);
    }

    /// <summary>¿Están pulsados ahora mismo la tecla y sus modificadores?</summary>
    public bool IsDown()
    {
        if (!Pressed(VirtualKey)) return false;
        if ((Modifiers & MOD_CONTROL) != 0 && !Pressed(VK_CONTROL)) return false;
        if ((Modifiers & MOD_ALT) != 0 && !Pressed(VK_MENU)) return false;
        if ((Modifiers & MOD_SHIFT) != 0 && !Pressed(VK_SHIFT)) return false;
        if ((Modifiers & MOD_WIN) != 0 && !Pressed(VK_LWIN) && !Pressed(VK_RWIN)) return false;
        return true;
    }

    public static bool Pressed(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static readonly HashSet<int> ExtendedKeys =
    [
        0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, // RePág, AvPág, Fin, Inicio, flechas
        0x2D, 0x2E, 0x6F, 0x90, 0xA3, 0xA5, 0x5D,       // Insert, Supr, Num /, BloqNum, Ctrl/Alt dcha, Menú
    ];

    /// <summary>Nombre de la tecla en el idioma del teclado de Windows.</summary>
    public static unsafe string KeyName(int vk)
    {
        if (vk == VK_XBUTTON1) return "Ratón X1";
        if (vk == VK_XBUTTON2) return "Ratón X2";
        if (vk is >= 0x70 and <= 0x87) return $"F{vk - 0x6F}";

        uint scan = MapVirtualKey((uint)vk, 0);
        int lParam = (int)(scan << 16);
        if (ExtendedKeys.Contains(vk)) lParam |= 1 << 24;

        char* buffer = stackalloc char[64];
        int len = GetKeyNameText(lParam, buffer, 64);
        if (len > 0)
        {
            string name = new(buffer, 0, len);
            return name.Length == 1 ? name.ToUpperInvariant() : char.ToUpper(name[0]) + name[1..].ToLowerInvariant();
        }
        return $"Tecla {vk:X2}";
    }
}
