using Autoclicker.Engine;
using Autoclicker.Localization;

namespace Autoclicker.Persistence;

/// <summary>Todo lo que se guarda en disco.</summary>
public sealed class AppData
{
    public static string DefaultProfileName => Strings.T("DefaultProfile");

    public int Version { get; set; } = 1;
    public string CurrentProfile { get; set; } = DefaultProfileName;
    public Dictionary<string, ProfileData> Profiles { get; set; } = new() { [DefaultProfileName] = new() };
    public GlobalPrefs Prefs { get; set; } = new();

    /// <summary>Garantiza que hay al menos un perfil y que el actual existe.</summary>
    public void Normalize()
    {
        Profiles ??= new();
        Prefs ??= new();
        if (Profiles.Count == 0) Profiles[DefaultProfileName] = new();
        if (CurrentProfile is null || !Profiles.ContainsKey(CurrentProfile)) CurrentProfile = Profiles.Keys.First();
    }

    // ---------------- JSON ----------------

    internal Dictionary<string, object?> ToJson() => new()
    {
        ["Version"] = Version,
        ["CurrentProfile"] = CurrentProfile,
        ["Profiles"] = Profiles.ToDictionary(kv => kv.Key, kv => (object?)kv.Value.ToJson()),
        ["Prefs"] = Prefs.ToJson(),
    };

    internal static AppData FromJson(JsonObj j)
    {
        var data = new AppData
        {
            Version = j.Int("Version", 1),
            Prefs = GlobalPrefs.FromJson(j.Obj("Prefs")),
            Profiles = new Dictionary<string, ProfileData>(),
        };
        foreach (var kv in j.Obj("Profiles").Entries)
            data.Profiles[kv.Key] = ProfileData.FromJson(JsonObj.From(kv.Value));
        data.CurrentProfile = j.Str("CurrentProfile") ?? "";
        data.Normalize();
        return data;
    }
}

/// <summary>Preferencias comunes a todos los perfiles.</summary>
public sealed class GlobalPrefs
{
    public int HotkeyVk { get; set; } = 0x75; // F6
    public uint HotkeyModifiers { get; set; }
    public bool HotkeyHoldMode { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; }
    public bool EmergencyCorner { get; set; } = true;
    public bool Pinned { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    // Anti-AFK (experimental): ventana por nombre de proceso y punto en coordenadas de cliente.
    public bool AntiAfkEnabled { get; set; }
    public int AntiAfkMinutes { get; set; } = 15;
    public string? AntiAfkProcess { get; set; }
    public string? AntiAfkTitle { get; set; }
    public int AntiAfkX { get; set; }
    public int AntiAfkY { get; set; }

    internal Dictionary<string, object?> ToJson() => new()
    {
        ["HotkeyVk"] = HotkeyVk,
        ["HotkeyModifiers"] = HotkeyModifiers,
        ["HotkeyHoldMode"] = HotkeyHoldMode,
        ["MinimizeToTray"] = MinimizeToTray,
        ["CloseToTray"] = CloseToTray,
        ["EmergencyCorner"] = EmergencyCorner,
        ["Pinned"] = Pinned,
        ["WindowLeft"] = WindowLeft,
        ["WindowTop"] = WindowTop,
        ["AntiAfkEnabled"] = AntiAfkEnabled,
        ["AntiAfkMinutes"] = AntiAfkMinutes,
        ["AntiAfkProcess"] = AntiAfkProcess,
        ["AntiAfkTitle"] = AntiAfkTitle,
        ["AntiAfkX"] = AntiAfkX,
        ["AntiAfkY"] = AntiAfkY,
    };

    internal static GlobalPrefs FromJson(JsonObj j)
    {
        var d = new GlobalPrefs();
        return new GlobalPrefs
        {
            HotkeyVk = j.Int("HotkeyVk", d.HotkeyVk),
            HotkeyModifiers = (uint)j.Int("HotkeyModifiers", 0),
            HotkeyHoldMode = j.Bool("HotkeyHoldMode", d.HotkeyHoldMode),
            MinimizeToTray = j.Bool("MinimizeToTray", d.MinimizeToTray),
            CloseToTray = j.Bool("CloseToTray", d.CloseToTray),
            EmergencyCorner = j.Bool("EmergencyCorner", d.EmergencyCorner),
            Pinned = j.Bool("Pinned", d.Pinned),
            WindowLeft = j.NumOrNull("WindowLeft"),
            WindowTop = j.NumOrNull("WindowTop"),
            AntiAfkEnabled = j.Bool("AntiAfkEnabled", false),
            AntiAfkMinutes = j.Int("AntiAfkMinutes", d.AntiAfkMinutes),
            AntiAfkProcess = j.Str("AntiAfkProcess"),
            AntiAfkTitle = j.Str("AntiAfkTitle"),
            AntiAfkX = j.Int("AntiAfkX", 0),
            AntiAfkY = j.Int("AntiAfkY", 0),
        };
    }
}

public enum IntervalUnit { Milliseconds, Seconds, Minutes, Hours }
public enum RepeatMode { Forever, Count, Time }
public enum PositionMode { Cursor, Fixed, Points }

/// <summary>Una configuración de clics con nombre (incluye los marcadores).</summary>
public sealed class ProfileData
{
    public long Interval { get; set; } = 100;
    public IntervalUnit Unit { get; set; } = IntervalUnit.Milliseconds;
    public MouseButtonKind Button { get; set; } = MouseButtonKind.Left;
    public ClickKind Kind { get; set; } = ClickKind.Single;
    public int HoldMs { get; set; } = 100;

    public RepeatMode Repeat { get; set; } = RepeatMode.Forever;
    public int RepeatCount { get; set; } = 100;
    public int DurationSeconds { get; set; } = 60;

    public PositionMode Position { get; set; } = PositionMode.Cursor;
    public int X { get; set; }
    public int Y { get; set; }
    public int OffsetPx { get; set; }
    public List<PointData> Points { get; set; } = [];
    public double Speed { get; set; } = 1.0;

    public int JitterPercent { get; set; }
    public int StartDelaySeconds { get; set; }
    public bool LockToWindow { get; set; }
    public bool RestoreCursor { get; set; }

    internal Dictionary<string, object?> ToJson() => new()
    {
        ["Interval"] = Interval,
        ["Unit"] = Unit,
        ["Button"] = Button,
        ["Kind"] = Kind,
        ["HoldMs"] = HoldMs,
        ["Repeat"] = Repeat,
        ["RepeatCount"] = RepeatCount,
        ["DurationSeconds"] = DurationSeconds,
        ["Position"] = Position,
        ["X"] = X,
        ["Y"] = Y,
        ["OffsetPx"] = OffsetPx,
        ["Points"] = Points.Select(p => (object?)p.ToJson()).ToList(),
        ["Speed"] = Speed,
        ["JitterPercent"] = JitterPercent,
        ["StartDelaySeconds"] = StartDelaySeconds,
        ["LockToWindow"] = LockToWindow,
        ["RestoreCursor"] = RestoreCursor,
    };

    internal static ProfileData FromJson(JsonObj j)
    {
        var d = new ProfileData();
        return new ProfileData
        {
            Interval = j.Long("Interval", d.Interval),
            Unit = j.Enum("Unit", d.Unit),
            Button = j.Enum("Button", d.Button),
            Kind = j.Enum("Kind", d.Kind),
            HoldMs = j.Int("HoldMs", d.HoldMs),
            Repeat = j.Enum("Repeat", d.Repeat),
            RepeatCount = j.Int("RepeatCount", d.RepeatCount),
            DurationSeconds = j.Int("DurationSeconds", d.DurationSeconds),
            Position = j.Enum("Position", d.Position),
            X = j.Int("X", 0),
            Y = j.Int("Y", 0),
            OffsetPx = j.Int("OffsetPx", 0),
            Points = j.Array("Points").Select(PointData.FromJson).ToList(),
            Speed = j.Num("Speed", d.Speed),
            JitterPercent = j.Int("JitterPercent", 0),
            StartDelaySeconds = j.Int("StartDelaySeconds", 0),
            LockToWindow = j.Bool("LockToWindow", false),
            RestoreCursor = j.Bool("RestoreCursor", false),
        };
    }
}

public sealed class PointData
{
    public int X { get; set; }
    public int Y { get; set; }
    public MouseButtonKind? Button { get; set; }
    public ClickKind? Kind { get; set; }
    public int? DelayMs { get; set; }
    public int? HoldMs { get; set; }
    public int? EndX { get; set; }
    public int? EndY { get; set; }

    internal Dictionary<string, object?> ToJson() => new()
    {
        ["X"] = X,
        ["Y"] = Y,
        ["Button"] = Button,
        ["Kind"] = Kind,
        ["DelayMs"] = DelayMs,
        ["HoldMs"] = HoldMs,
        ["EndX"] = EndX,
        ["EndY"] = EndY,
    };

    internal static PointData FromJson(JsonObj j) => new()
    {
        X = j.Int("X", 0),
        Y = j.Int("Y", 0),
        Button = j.EnumOrNull<MouseButtonKind>("Button"),
        Kind = j.EnumOrNull<ClickKind>("Kind"),
        DelayMs = j.IntOrNull("DelayMs"),
        HoldMs = j.IntOrNull("HoldMs"),
        EndX = j.IntOrNull("EndX"),
        EndY = j.IntOrNull("EndY"),
    };
}
