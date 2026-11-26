using System.Globalization;
using Autoclicker.Localization;

namespace Autoclicker.Macros;

public enum StepKind { Moves, Click, Drag, KeyPress, KeyDown, KeyUp, ButtonDown, ButtonUp, Wheel, Text, WaitColor }

/// <summary>
/// Un paso del editor: un grupo de eventos que se entiende como una sola acción ("Clic izquierdo en
/// 512, 300", "Pulsar E durante 0,12 s"...). Los tiempos de <see cref="Events"/> son relativos al
/// inicio del paso; <see cref="WaitBefore"/> es la espera desde el final del paso anterior.
/// </summary>
public sealed class MacroStep
{
    public StepKind Kind { get; set; }
    public long WaitBefore { get; set; }
    public List<MacroEvent> Events { get; set; } = [];

    public long Duration => Events.Count == 0 ? 0 : Events[Events.Count - 1].Micros;

    public MacroStep Clone() => new() { Kind = Kind, WaitBefore = WaitBefore, Events = [.. Events] };

    /// <summary>Primer evento de ratón con posición (clic, arrastre, pulsar/soltar botón).</summary>
    public MacroEvent? FirstPointer => Events.Where(e => e.Kind is MacroEventKind.Down or MacroEventKind.Up).Cast<MacroEvent?>().FirstOrDefault();
}

/// <summary>
/// Convierte los eventos de una macro en pasos legibles y al revés, y las operaciones del editor.
/// Ida y vuelta sin pérdidas: ToEvents(FromEvents(x)) reproduce los mismos eventos y tiempos.
/// </summary>
public static class MacroSteps
{
    /// <summary>Intervalo entre letras al escribir un texto insertado.</summary>
    public const long TypeIntervalMicros = 30_000;

    public static List<MacroStep> FromEvents(IReadOnlyList<MacroEvent> events)
    {
        var steps = new List<MacroStep>();
        long prevEnd = 0;
        int i = 0;
        while (i < events.Count)
        {
            int end = GroupEnd(events, i, out StepKind kind); // exclusivo
            var group = new List<MacroEvent>(end - i);
            long start = events[i].Micros;
            for (int k = i; k < end; k++) group.Add(events[k] with { Micros = events[k].Micros - start });
            steps.Add(new MacroStep { Kind = kind, WaitBefore = Math.Max(0, start - prevEnd), Events = group });
            prevEnd = events[end - 1].Micros;
            i = end;
        }
        return steps;
    }

    /// <summary>Dónde termina el paso que empieza en <paramref name="i"/>, y de qué tipo es.</summary>
    private static int GroupEnd(IReadOnlyList<MacroEvent> ev, int i, out StepKind kind)
    {
        MacroEvent e = ev[i];
        switch (e.Kind)
        {
            case MacroEventKind.Move:
                kind = StepKind.Moves;
                return While(ev, i, x => x.Kind == MacroEventKind.Move);

            case MacroEventKind.Down:
            {
                // Hasta su "soltar" del mismo botón, con solo movimientos en medio: clic o arrastre
                int j = i + 1, moves = 0;
                while (j < ev.Count && ev[j].Kind == MacroEventKind.Move) { j++; moves++; }
                if (j < ev.Count && ev[j].Kind == MacroEventKind.Up && ev[j].Data == e.Data)
                {
                    kind = moves > 0 && Distance(e, ev[j]) > 4 ? StepKind.Drag : StepKind.Click;
                    return j + 1;
                }
                kind = StepKind.ButtonDown;
                return i + 1;
            }

            case MacroEventKind.Up:
                kind = StepKind.ButtonUp;
                return i + 1;

            case MacroEventKind.Wheel:
            case MacroEventKind.HWheel:
                kind = StepKind.Wheel;
                return While(ev, i, x => x.Kind == e.Kind && Math.Sign(x.Data) == Math.Sign(e.Data));

            case MacroEventKind.KeyDown:
                if (i + 1 < ev.Count && ev[i + 1].Kind == MacroEventKind.KeyUp && ev[i + 1].Data == e.Data)
                {
                    kind = StepKind.KeyPress;
                    return i + 2;
                }
                kind = StepKind.KeyDown;
                return i + 1;

            case MacroEventKind.KeyUp:
                kind = StepKind.KeyUp;
                return i + 1;

            case MacroEventKind.WaitColor:
                kind = StepKind.WaitColor;
                return i + 1;

            default: // TypeChar
                kind = StepKind.Text;
                return While(ev, i, x => x.Kind == MacroEventKind.TypeChar);
        }
    }

    private static int While(IReadOnlyList<MacroEvent> ev, int i, Func<MacroEvent, bool> same)
    {
        int j = i + 1;
        while (j < ev.Count && same(ev[j])) j++;
        return j;
    }

    private static int Distance(MacroEvent a, MacroEvent b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    public static List<MacroEvent> ToEvents(IEnumerable<MacroStep> steps)
    {
        var list = new List<MacroEvent>();
        long t = 0;
        foreach (var step in steps)
        {
            t += Math.Max(0, step.WaitBefore);
            foreach (var e in step.Events) list.Add(e with { Micros = t + e.Micros });
            t += step.Duration;
        }
        return list;
    }

    // ================= Operaciones del editor =================

    /// <summary>Las esperas de más de <paramref name="maxMicros"/> pasan a durar eso.</summary>
    public static int ShortenWaits(List<MacroStep> steps, long maxMicros)
    {
        int changed = 0;
        foreach (var s in steps)
            if (s.WaitBefore > maxMicros) { s.WaitBefore = maxMicros; changed++; }
        return changed;
    }

    /// <summary>
    /// Quita los pasos de "mover el ratón" (cada clic ya lleva su posición). Su tiempo se suma a la
    /// espera del paso siguiente: el resto de la macro no se adelanta.
    /// </summary>
    public static int RemoveMoves(List<MacroStep> steps)
    {
        int removed = 0;
        for (int i = steps.Count - 1; i >= 0; i--)
        {
            if (steps[i].Kind != StepKind.Moves) continue;
            long time = steps[i].WaitBefore + steps[i].Duration;
            if (i + 1 < steps.Count) steps[i + 1].WaitBefore += time;
            steps.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    public static MacroStep NewKeyPress(int vk, long holdMicros, long waitBefore)
    {
        (int scan, int flags) = ScanCode(vk);
        return new MacroStep
        {
            Kind = StepKind.KeyPress,
            WaitBefore = waitBefore,
            Events =
            [
                new MacroEvent(0, MacroEventKind.KeyDown, scan, 0, vk, flags),
                new MacroEvent(Math.Max(1_000, holdMicros), MacroEventKind.KeyUp, scan, 0, vk, flags),
            ],
        };
    }

    public static MacroStep NewClick(MacroButton button, int x, int y, long waitBefore) => new()
    {
        Kind = StepKind.Click,
        WaitBefore = waitBefore,
        Events =
        [
            new MacroEvent(0, MacroEventKind.Down, x, y, (int)button, 0),
            new MacroEvent(40_000, MacroEventKind.Up, x, y, (int)button, 0),
        ],
    };

    /// <summary>Esperar a que el punto tenga ese color (tolerancia 10), como mucho <paramref name="timeoutSeconds"/>.</summary>
    public static MacroStep NewWaitColor(int x, int y, int color, int timeoutSeconds, long waitBefore) => new()
    {
        Kind = StepKind.WaitColor,
        WaitBefore = waitBefore,
        Events = [new MacroEvent(0, MacroEventKind.WaitColor, x, y, color & 0xFFFFFF, 10 | (Math.Max(1, timeoutSeconds) << 8))],
    };

    /// <summary>Cambia el punto (y el color a esperar) de un paso "Esperar a un color".</summary>
    public static void SetWaitPoint(MacroStep step, int x, int y, int? color = null)
    {
        if (step.Kind != StepKind.WaitColor) return;
        MacroEvent e = step.Events[0];
        step.Events[0] = e with { X = x, Y = y, Data = color ?? e.Data };
    }

    public static void SetWaitTimeout(MacroStep step, int seconds)
    {
        if (step.Kind != StepKind.WaitColor) return;
        MacroEvent e = step.Events[0];
        step.Events[0] = e with { Flags = (e.Flags & 0xFF) | (Math.Max(1, Math.Min(3600, seconds)) << 8) };
    }

    public static MacroStep NewText(string text, long waitBefore) => new()
    {
        Kind = StepKind.Text,
        WaitBefore = waitBefore,
        Events = text.Select((c, i) => new MacroEvent(i * TypeIntervalMicros, MacroEventKind.TypeChar, 0, 0, c, 0)).ToList(),
    };

    /// <summary>Cambia la tecla de un paso de teclado (pulsar, mantener o soltar).</summary>
    public static void SetKey(MacroStep step, int vk)
    {
        (int scan, int flags) = ScanCode(vk);
        step.Events = step.Events.Select(e => e.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp
            ? e with { Data = vk, X = scan, Flags = flags } : e).ToList();
    }

    /// <summary>Cuánto se mantiene la tecla de un paso "Pulsar".</summary>
    public static void SetHold(MacroStep step, long holdMicros)
    {
        if (step.Kind != StepKind.KeyPress || step.Events.Count != 2) return;
        step.Events[1] = step.Events[1] with { Micros = Math.Max(1_000, holdMicros) };
    }

    /// <summary>Mueve un clic (o pulsar/soltar botón) a otra posición.</summary>
    public static void SetPosition(MacroStep step, int x, int y)
    {
        if (step.Kind == StepKind.Drag) return; // un arrastre tiene origen y destino
        step.Events = step.Events.Select(e => e.Kind is MacroEventKind.Down or MacroEventKind.Up ? e with { X = x, Y = y } : e).ToList();
    }

    public static void SetText(MacroStep step, string text)
    {
        if (step.Kind != StepKind.Text) return;
        step.Events = NewText(text, 0).Events;
    }

    public static string TextOf(MacroStep step) =>
        new string(step.Events.Where(e => e.Kind == MacroEventKind.TypeChar).Select(e => (char)e.Data).ToArray());

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    private static (int Scan, int Flags) ScanCode(int vk)
    {
        bool extended = vk is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
            or 0x2D or 0x2E or 0x6F or 0x90 or 0xA3 or 0xA5 or 0x5B or 0x5C or 0x5D;
        return ((int)MapVirtualKey((uint)vk, 0), extended ? 1 : 0);
    }

    // ================= Textos =================

    public static string Seconds(long micros) =>
        (micros / 1_000_000.0).ToString(micros < 10_000_000 ? "0.00" : "0.0", CultureInfo.CurrentCulture);

    private static string ButtonName(int b) => Strings.T((MacroButton)b switch
    {
        MacroButton.Right => "StepButtonRight",
        MacroButton.Middle => "StepButtonMiddle",
        MacroButton.X1 => "StepButtonX1",
        MacroButton.X2 => "StepButtonX2",
        _ => "StepButtonLeft",
    });

    public static string Describe(MacroStep s)
    {
        MacroEvent first = s.Events[0];
        MacroEvent last = s.Events[s.Events.Count - 1];
        string key = s.Events.FirstOrDefault(e => e.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp) is var k && k.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp
            ? Input.Hotkey.KeyName(k.Data) : "";
        return s.Kind switch
        {
            StepKind.Moves => Strings.F("StepMoves", s.Events.Count),
            StepKind.Click => Strings.F("StepClick", ButtonName(first.Data), first.X, first.Y),
            StepKind.Drag => Strings.F("StepDrag", first.X, first.Y, last.X, last.Y),
            StepKind.KeyPress => Strings.F("StepKeyPress", key, Seconds(s.Duration)),
            StepKind.KeyDown => Strings.F("StepKeyDown", key),
            StepKind.KeyUp => Strings.F("StepKeyUp", key),
            StepKind.ButtonDown => Strings.F("StepButtonDown", ButtonName(first.Data), first.X, first.Y),
            StepKind.ButtonUp => Strings.F("StepButtonUp", ButtonName(first.Data)),
            StepKind.WaitColor => Strings.F("StepWaitColor", first.Data, first.X, first.Y, first.WaitTimeoutSeconds),
            StepKind.Wheel => Strings.F(first.Data < 0 ? "StepWheelDown" : first.Kind == MacroEventKind.HWheel ? "StepWheelRight" : "StepWheelUp",
                                        s.Events.Sum(e => Math.Abs(e.Data)) / 120),
            _ => Strings.F("StepText", TextOf(s)),
        };
    }

    /// <summary>Icono (Segoe Fluent Icons) de cada tipo de paso.</summary>
    public static string Glyph(StepKind kind) => kind switch
    {
        StepKind.Moves => "",
        StepKind.KeyPress or StepKind.KeyDown or StepKind.KeyUp => "",
        StepKind.Wheel => "",
        StepKind.Text => "",
        StepKind.WaitColor => "",
        _ => "",
    };
}
