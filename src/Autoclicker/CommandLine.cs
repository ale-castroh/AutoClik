namespace Autoclicker;

public enum CommandKind { Start, Stop, Toggle, Play, Profile, Watch, Show, Hide, Exit }

/// <summary>Una orden de la línea de comandos (2.6), con su valor si lo lleva (--play "Macro").</summary>
public sealed record AppCommand(CommandKind Kind, string? Value = null);

/// <summary>
/// Línea de comandos (2.6): --start, --stop, --toggle, --play "Macro", --profile "Nombre", --watch on|off,
/// --show, --hide, --exit y --help. Si AutoClik ya está abierto, las órdenes se le pasan a esa copia.
/// </summary>
public static class CommandLine
{
    public static List<AppCommand> Parse(IReadOnlyList<string> args, out bool help, out List<string> errors)
    {
        var commands = new List<AppCommand>();
        errors = [];
        help = false;
        for (int i = 0; i < args.Count; i++)
        {
            string raw = args[i];
            string arg = raw.ToLowerInvariant();
            // --play=Macro equivale a --play Macro
            string? inline = null;
            int eq = arg.IndexOf('=');
            if (eq > 0 && (arg.StartsWith("--play=") || arg.StartsWith("--profile=") || arg.StartsWith("--watch=")))
            {
                inline = raw.Substring(eq + 1).Trim('"');
                arg = arg.Substring(0, eq);
            }
            string? Value()
            {
                if (inline != null) return inline;
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) return args[++i];
                return null;
            }

            switch (arg)
            {
                case "--start": commands.Add(new(CommandKind.Start)); break;
                case "--stop": commands.Add(new(CommandKind.Stop)); break;
                case "--toggle": commands.Add(new(CommandKind.Toggle)); break;
                case "--show": commands.Add(new(CommandKind.Show)); break;
                case "--hide": commands.Add(new(CommandKind.Hide)); break;
                case "--exit": commands.Add(new(CommandKind.Exit)); break;
                case "--help" or "-h" or "/?" or "-?": help = true; break;
                case "--play" or "--profile":
                {
                    string? value = Value();
                    if (string.IsNullOrWhiteSpace(value)) errors.Add(raw);
                    else commands.Add(new(arg == "--play" ? CommandKind.Play : CommandKind.Profile, value!.Trim()));
                    break;
                }
                case "--watch":
                {
                    string? value = Value()?.Trim().ToLowerInvariant();
                    if (value is "on" or "off") commands.Add(new(CommandKind.Watch, value));
                    else errors.Add(raw);
                    break;
                }
            }
        }
        return commands;
    }

    /// <summary>¿Hace falta enseñar la ventana? Las que clican o reproducen arrancan ocultas (mini ventana).</summary>
    public static bool StartsHidden(IEnumerable<AppCommand> commands) =>
        commands.Any(c => c.Kind is CommandKind.Start or CommandKind.Toggle or CommandKind.Play or CommandKind.Hide);

    /// <summary>Órdenes que no tiene sentido ejecutar si AutoClik no estaba abierto (parar o salir).</summary>
    public static bool OnlyStopOrExit(IReadOnlyCollection<AppCommand> commands) =>
        commands.Count > 0 && commands.All(c => c.Kind is CommandKind.Stop or CommandKind.Exit);

    public static string Serialize(IEnumerable<AppCommand> commands) =>
        string.Join("\n", commands.Select(c => c.Value == null ? c.Kind.ToString() : c.Kind + "\t" + c.Value.Replace("\n", " ")));

    public static List<AppCommand> Deserialize(string text)
    {
        var list = new List<AppCommand>();
        foreach (string line in text.Split('\n'))
        {
            if (line.Length == 0) continue;
            string[] parts = line.Split(new[] { '\t' }, 2);
            if (Enum.TryParse(parts[0], out CommandKind kind)) list.Add(new(kind, parts.Length > 1 ? parts[1] : null));
        }
        return list;
    }
}
