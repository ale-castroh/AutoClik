using System.Globalization;
using System.Text;

namespace Autoclicker.Persistence;

/// <summary>Contadores de un periodo (hoy o desde siempre).</summary>
public sealed class UsageTotals
{
    public long Clicks { get; set; }
    public long Keys { get; set; }
    public long MacroPlays { get; set; }
    public long Seconds { get; set; }
    public long LongestSeconds { get; set; }

    internal Dictionary<string, object?> ToJson() => new()
    {
        ["Clicks"] = Clicks,
        ["Keys"] = Keys,
        ["MacroPlays"] = MacroPlays,
        ["Seconds"] = Seconds,
        ["LongestSeconds"] = LongestSeconds,
    };

    internal static UsageTotals FromJson(JsonObj j) => new()
    {
        Clicks = Math.Max(0, j.Long("Clicks", 0)),
        Keys = Math.Max(0, j.Long("Keys", 0)),
        MacroPlays = Math.Max(0, j.Long("MacroPlays", 0)),
        Seconds = Math.Max(0, j.Long("Seconds", 0)),
        LongestSeconds = Math.Max(0, j.Long("LongestSeconds", 0)),
    };

    internal void Add(long clicks, long keys, long plays, long seconds)
    {
        Clicks += clicks;
        Keys += keys;
        MacroPlays += plays;
        Seconds += seconds;
        LongestSeconds = Math.Max(LongestSeconds, seconds);
    }
}

/// <summary>Uso de una macro: cuántas veces se reprodujo y cuándo fue la última.</summary>
public sealed class MacroUsage
{
    public long Plays { get; set; }
    public DateTime? Last { get; set; }
}

/// <summary>
/// Estadísticas (2.6): clics, teclas, macros reproducidas y tiempo, de hoy y en total, y el uso de cada macro.
/// Solo en este PC, en stats.json junto a settings.json; nunca se envían a ningún sitio.
/// </summary>
public sealed class UsageStats
{
    public const string FileName = "stats.json";

    public UsageTotals Total { get; private set; } = new();
    public UsageTotals Today { get; private set; } = new();
    public DateTime TodayDate { get; private set; } = DateTime.Today;
    public DateTime Since { get; private set; } = DateTime.Today;
    public Dictionary<string, MacroUsage> Macros { get; } = new(StringComparer.CurrentCultureIgnoreCase);

    public static string FilePath => Path.Combine(SettingsStore.DataDirectory, FileName);

    /// <summary>Hoy: si cambió el día desde la última vez, empieza de cero.</summary>
    public UsageTotals TodayTotals
    {
        get
        {
            RollDay();
            return Today;
        }
    }

    private void RollDay()
    {
        if (TodayDate == DateTime.Today) return;
        TodayDate = DateTime.Today;
        Today = new UsageTotals();
    }

    /// <summary>Una tanda del autoclicker terminó.</summary>
    public void AddClicks(long clicks, long keys, TimeSpan duration)
    {
        if (clicks <= 0 && keys <= 0 && duration < TimeSpan.FromSeconds(1)) return;
        RollDay();
        long seconds = (long)Math.Max(0, duration.TotalSeconds);
        Total.Add(clicks, keys, 0, seconds);
        Today.Add(clicks, keys, 0, seconds);
    }

    /// <summary>Una reproducción de macro terminó (o se paró).</summary>
    public void AddMacro(string name, long clicks, long keys, TimeSpan duration)
    {
        RollDay();
        long seconds = (long)Math.Max(0, duration.TotalSeconds);
        Total.Add(clicks, keys, 1, seconds);
        Today.Add(clicks, keys, 1, seconds);
        if (!Macros.TryGetValue(name, out var usage)) Macros[name] = usage = new MacroUsage();
        usage.Plays++;
        usage.Last = DateTime.Now;
    }

    public MacroUsage? UsageOf(string name) => Macros.TryGetValue(name, out var usage) ? usage : null;

    public void RenameMacro(string oldName, string newName)
    {
        if (!Macros.TryGetValue(oldName, out var usage)) return;
        Macros.Remove(oldName);
        Macros[newName] = usage;
    }

    public void ForgetMacro(string name) => Macros.Remove(name);

    public void Reset()
    {
        Total = new UsageTotals();
        Today = new UsageTotals();
        TodayDate = DateTime.Today;
        Since = DateTime.Today;
        Macros.Clear();
    }

    // ---------------- Disco ----------------

    public static UsageStats Load()
    {
        var stats = new UsageStats();
        try
        {
            if (!File.Exists(FilePath)) return stats;
            var j = JsonObj.From(MiniJson.Parse(File.ReadAllText(FilePath, Encoding.UTF8)));
            stats.Total = UsageTotals.FromJson(j.Obj("Total"));
            stats.Today = UsageTotals.FromJson(j.Obj("Today"));
            stats.TodayDate = ParseDate(j.Str("TodayDate")) ?? DateTime.Today;
            stats.Since = ParseDate(j.Str("Since")) ?? DateTime.Today;
            foreach (var kv in j.Obj("Macros").Entries)
            {
                var m = JsonObj.From(kv.Value);
                stats.Macros[kv.Key] = new MacroUsage
                {
                    Plays = Math.Max(0, m.Long("Plays", 0)),
                    Last = DateTime.TryParse(m.Str("Last"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null,
                };
            }
            stats.RollDay();
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException
                                       or InvalidCastException or OverflowException or ArgumentException)
        {
            ErrorLog.Write("estadísticas", ex);
        }
        return stats;
    }

    public void Save()
    {
        try
        {
            var json = new Dictionary<string, object?>
            {
                ["Since"] = Since.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["TodayDate"] = TodayDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["Today"] = Today.ToJson(),
                ["Total"] = Total.ToJson(),
                ["Macros"] = Macros.ToDictionary(kv => kv.Key, kv => (object?)new Dictionary<string, object?>
                {
                    ["Plays"] = kv.Value.Plays,
                    ["Last"] = kv.Value.Last?.ToString("o", CultureInfo.InvariantCulture),
                }),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, MiniJson.Serialize(json), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write("guardar estadísticas", ex);
        }
    }

    private static DateTime? ParseDate(string? text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
