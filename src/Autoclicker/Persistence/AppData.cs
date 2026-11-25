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
}

public sealed class PointData
{
    public int X { get; set; }
    public int Y { get; set; }
    public MouseButtonKind? Button { get; set; }
    public ClickKind? Kind { get; set; }
    public int? DelayMs { get; set; }
    public int? HoldMs { get; set; }
}
