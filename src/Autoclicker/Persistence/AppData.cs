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
    public bool Translucent { get; set; } = true;
    public bool HudEnabled { get; set; } = true;
    public int? HudLeft { get; set; }
    public int? HudTop { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    // Anti-AFK (experimental): ventana por nombre de proceso y punto en coordenadas de cliente.
    // Macros (2.1): atajos, última macro elegida y opciones de reproducción. Globales, no por perfil.
    public int MacroRecordVk { get; set; } = 0x76; // F7
    public uint MacroRecordModifiers { get; set; }
    public int MacroPlayVk { get; set; } = 0x77;   // F8
    public uint MacroPlayModifiers { get; set; }
    public string? MacroName { get; set; }
    public Macros.MacroRepeat MacroRepeat { get; set; } = Macros.MacroRepeat.Once;
    public int MacroCount { get; set; } = 10;
    public int MacroMinutes { get; set; } = 10;
    public double MacroSpeed { get; set; } = 1.0;
    public Macros.MacroRecordMode MacroMode { get; set; } = Macros.MacroRecordMode.All;

    /// <summary>Mostrar funciones experimentales (Anti-AFK...). Apagado por defecto.</summary>
    public bool Experimental { get; set; }
    public bool AntiAfkEnabled { get; set; }
    public int AntiAfkMinutes { get; set; } = 15;
    public string? AntiAfkProcess { get; set; }
    public string? AntiAfkTitle { get; set; }
    public int AntiAfkX { get; set; }
    public int AntiAfkY { get; set; }
    public bool AntiAfkUseKey { get; set; }
    public int AntiAfkKeyVk { get; set; } = 0x20; // Espacio

    internal Dictionary<string, object?> ToJson() => new()
    {
        ["HotkeyVk"] = HotkeyVk,
        ["HotkeyModifiers"] = HotkeyModifiers,
        ["HotkeyHoldMode"] = HotkeyHoldMode,
        ["MinimizeToTray"] = MinimizeToTray,
        ["CloseToTray"] = CloseToTray,
        ["EmergencyCorner"] = EmergencyCorner,
        ["Pinned"] = Pinned,
        ["Translucent"] = Translucent,
        ["HudEnabled"] = HudEnabled,
        ["HudLeft"] = HudLeft,
        ["HudTop"] = HudTop,
        ["WindowLeft"] = WindowLeft,
        ["WindowTop"] = WindowTop,
        ["Experimental"] = Experimental,
        ["MacroRecordVk"] = MacroRecordVk,
        ["MacroRecordModifiers"] = MacroRecordModifiers,
        ["MacroPlayVk"] = MacroPlayVk,
        ["MacroPlayModifiers"] = MacroPlayModifiers,
        ["MacroName"] = MacroName,
        ["MacroRepeat"] = MacroRepeat,
        ["MacroCount"] = MacroCount,
        ["MacroMinutes"] = MacroMinutes,
        ["MacroSpeed"] = MacroSpeed,
        ["MacroMode"] = MacroMode,
        ["AntiAfkEnabled"] = AntiAfkEnabled,
        ["AntiAfkMinutes"] = AntiAfkMinutes,
        ["AntiAfkProcess"] = AntiAfkProcess,
        ["AntiAfkTitle"] = AntiAfkTitle,
        ["AntiAfkX"] = AntiAfkX,
        ["AntiAfkY"] = AntiAfkY,
        ["AntiAfkUseKey"] = AntiAfkUseKey,
        ["AntiAfkKeyVk"] = AntiAfkKeyVk,
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
            Translucent = j.Bool("Translucent", d.Translucent),
            HudEnabled = j.Bool("HudEnabled", d.HudEnabled),
            HudLeft = j.IntOrNull("HudLeft"),
            HudTop = j.IntOrNull("HudTop"),
            WindowLeft = j.NumOrNull("WindowLeft"),
            WindowTop = j.NumOrNull("WindowTop"),
            MacroRecordVk = j.Int("MacroRecordVk", d.MacroRecordVk),
            MacroRecordModifiers = (uint)j.Int("MacroRecordModifiers", 0),
            MacroPlayVk = j.Int("MacroPlayVk", d.MacroPlayVk),
            MacroPlayModifiers = (uint)j.Int("MacroPlayModifiers", 0),
            MacroName = j.Str("MacroName"),
            MacroRepeat = j.Enum("MacroRepeat", d.MacroRepeat),
            MacroCount = j.Int("MacroCount", d.MacroCount),
            MacroMinutes = j.Int("MacroMinutes", d.MacroMinutes),
            MacroSpeed = j.Num("MacroSpeed", d.MacroSpeed),
            MacroMode = j.Enum("MacroMode", d.MacroMode),
            // Sin el campo (ajustes de la 1.4): encendido si ya se usaba el anti-AFK.
            Experimental = j.Bool("Experimental", j.Bool("AntiAfkEnabled", false) || j.Str("AntiAfkProcess") != null),
            AntiAfkEnabled = j.Bool("AntiAfkEnabled", false),
            AntiAfkMinutes = j.Int("AntiAfkMinutes", d.AntiAfkMinutes),
            AntiAfkProcess = j.Str("AntiAfkProcess"),
            AntiAfkTitle = j.Str("AntiAfkTitle"),
            AntiAfkX = j.Int("AntiAfkX", 0),
            AntiAfkY = j.Int("AntiAfkY", 0),
            AntiAfkUseKey = j.Bool("AntiAfkUseKey", false),
            AntiAfkKeyVk = j.Int("AntiAfkKeyVk", d.AntiAfkKeyVk),
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
    public ClickAction Action { get; set; } = ClickAction.Mouse;
    public int KeyVk { get; set; } = 0x45;      // E
    public bool WheelUp { get; set; }
    public int WheelSteps { get; set; } = 3;
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
        ["Action"] = Action,
        ["KeyVk"] = KeyVk,
        ["WheelUp"] = WheelUp,
        ["WheelSteps"] = WheelSteps,
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
            Action = j.Enum("Action", d.Action),
            KeyVk = j.Int("KeyVk", d.KeyVk),
            WheelUp = j.Bool("WheelUp", d.WheelUp),
            WheelSteps = j.Int("WheelSteps", d.WheelSteps),
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
