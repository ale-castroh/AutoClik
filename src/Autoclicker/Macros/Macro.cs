using System.Globalization;
using System.Text;
using Autoclicker.Persistence;

namespace Autoclicker.Macros;

/// <summary>TypeChar (2.3): escribe el carácter Data tal cual (texto insertado en el editor).</summary>
public enum MacroEventKind { Move, Down, Up, Wheel, HWheel, KeyDown, KeyUp, TypeChar }

public enum MacroButton { Left, Right, Middle, X1, X2 }

/// <summary>Qué se graba: todo, solo el ratón o solo el teclado.</summary>
public enum MacroRecordMode { All, Mouse, Keys }

/// <summary>
/// Un evento de la macro. <see cref="Micros"/> = microsegundos desde el inicio.
/// Ratón: X, Y en píxeles físicos de pantalla; Data = botón (MacroButton) o giro de la rueda.
/// Teclado: Data = tecla virtual, X = código de escaneo, Flags bit 0 = tecla extendida.
/// </summary>
public readonly record struct MacroEvent(long Micros, MacroEventKind Kind, int X, int Y, int Data, int Flags)
{
    public bool IsExtendedKey => (Flags & 1) != 0;
}

/// <summary>Una macro grabada: nombre, fecha y la lista de eventos con sus tiempos.</summary>
public sealed class Macro
{
    public string Name { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.Now;
    public List<MacroEvent> Events { get; set; } = [];

    /// <summary>Atajo propio (tecla virtual + modificadores MOD_*); null = sin atajo (2.2).</summary>
    public int? HotkeyVk { get; set; }
    public uint HotkeyModifiers { get; set; }

    /// <summary>
    /// Posiciones relativas a una ventana (2.3): proceso de la ventana y dónde estaba su esquina
    /// (zona cliente, en pantalla) cuando se tomaron las posiciones. Al reproducir, todo se desplaza
    /// lo que se haya movido la ventana. Null = posiciones de pantalla.
    /// </summary>
    public string? WindowProcess { get; set; }
    public string? WindowTitle { get; set; }
    public int WindowX { get; set; }
    public int WindowY { get; set; }

    /// <summary>Archivo del que se cargó (para renombrar o borrar). Null = aún no guardada.</summary>
    internal string? FilePath { get; set; }

    public long DurationMicros => Events.Count == 0 ? 0 : Events[Events.Count - 1].Micros;
    public int Clicks => Events.Count(e => e.Kind == MacroEventKind.Down);
    public int Keys => Events.Count(e => e.Kind == MacroEventKind.KeyDown);
    public int Moves => Events.Count(e => e.Kind == MacroEventKind.Move);
    public int Scrolls => Events.Count(e => e.Kind is MacroEventKind.Wheel or MacroEventKind.HWheel);

    public Macro Clone(string name) => new()
    {
        Name = name, Created = DateTime.Now, Events = [.. Events],
        WindowProcess = WindowProcess, WindowTitle = WindowTitle, WindowX = WindowX, WindowY = WindowY,
    };

    /// <summary>Copia con las posiciones del ratón desplazadas (para posiciones relativas a una ventana).</summary>
    public Macro Shifted(int dx, int dy)
    {
        var copy = Clone(Name);
        copy.HotkeyVk = HotkeyVk;
        copy.HotkeyModifiers = HotkeyModifiers;
        if (dx != 0 || dy != 0)
            copy.Events = Events.Select(e => e.Kind is MacroEventKind.Move or MacroEventKind.Down or MacroEventKind.Up
                ? e with { X = e.X + dx, Y = e.Y + dy } : e).ToList();
        return copy;
    }

    public Input.Hotkey? Hotkey => HotkeyVk is int vk ? new Input.Hotkey(vk, HotkeyModifiers) : null;

    // ---------------- JSON ----------------
    // Los eventos van en una sola cadena compacta ("t,k,x,y,d,f;...") en vez de una lista JSON:
    // MiniJson escribe cada número en su línea y una macro de miles de eventos ocuparía megas.

    internal string ToJson() => MiniJson.Serialize(new Dictionary<string, object?>
    {
        ["Format"] = "AutoClik macro",
        ["Version"] = 1,
        ["Name"] = Name,
        ["Created"] = Created.ToString("o", CultureInfo.InvariantCulture),
        ["HotkeyVk"] = HotkeyVk,
        ["HotkeyModifiers"] = HotkeyModifiers,
        ["WindowProcess"] = WindowProcess,
        ["WindowTitle"] = WindowTitle,
        ["WindowX"] = WindowX,
        ["WindowY"] = WindowY,
        ["Events"] = EncodeEvents(Events),
    });

    internal static Macro FromJson(string text)
    {
        var j = JsonObj.From(MiniJson.Parse(text));
        if (j.Str("Format") != "AutoClik macro") throw new FormatException("No es una macro de AutoClik");
        return new Macro
        {
            Name = j.Str("Name") ?? "",
            Created = DateTime.TryParse(j.Str("Created"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : DateTime.Now,
            Events = DecodeEvents(j.Str("Events") ?? ""),
            HotkeyVk = j.IntOrNull("HotkeyVk"),
            HotkeyModifiers = (uint)j.Int("HotkeyModifiers", 0),
            WindowProcess = string.IsNullOrWhiteSpace(j.Str("WindowProcess")) ? null : j.Str("WindowProcess"),
            WindowTitle = j.Str("WindowTitle"),
            WindowX = j.Int("WindowX", 0),
            WindowY = j.Int("WindowY", 0),
        };
    }

    private static string EncodeEvents(List<MacroEvent> events)
    {
        var sb = new StringBuilder(events.Count * 18);
        foreach (var e in events)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(e.Micros).Append(',').Append((int)e.Kind).Append(',').Append(e.X).Append(',')
              .Append(e.Y).Append(',').Append(e.Data).Append(',').Append(e.Flags);
        }
        return sb.ToString();
    }

    private static List<MacroEvent> DecodeEvents(string text)
    {
        var list = new List<MacroEvent>();
        foreach (string item in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] p = item.Split(',');
            if (p.Length != 6) throw new FormatException("Evento de macro no válido");
            int kind = int.Parse(p[1], CultureInfo.InvariantCulture);
            if (!Enum.IsDefined(typeof(MacroEventKind), kind)) throw new FormatException("Tipo de evento no válido");
            list.Add(new MacroEvent(
                long.Parse(p[0], CultureInfo.InvariantCulture), (MacroEventKind)kind,
                int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture),
                int.Parse(p[4], CultureInfo.InvariantCulture), int.Parse(p[5], CultureInfo.InvariantCulture)));
        }
        // Siempre en orden de tiempo (un archivo editado a mano podría venir desordenado)
        return list.OrderBy(e => e.Micros).ToList();
    }
}
