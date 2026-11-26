using System.Diagnostics;
using System.Runtime.InteropServices;
using Autoclicker.Localization;
using Autoclicker.Persistence;

namespace Autoclicker.Scheduling;

public enum StartMode { Now, At, In }

public enum EndAction { None, Notify, Sleep, Shutdown }

/// <summary>
/// Una programación (2.5): cuándo empezar (los clics o la macro) y qué hacer al terminar solos.
/// No se guarda en disco: al cerrar AutoClik se pierde (a la bandeja, no).
/// </summary>
public sealed class Schedule
{
    public StartMode Mode { get; set; } = StartMode.At;
    public int Hour { get; set; } = 3;
    public int Minute { get; set; }
    public int InMinutes { get; set; } = 30;
    public EndAction End { get; set; } = EndAction.None;

    /// <summary>Solo pruebas automáticas: "Dentro de" cuenta segundos en vez de minutos.</summary>
    public static bool TestSeconds { get; set; }

    /// <summary>Cuándo empezar, calculado desde <paramref name="now"/>. "A las" ya pasada = mañana.</summary>
    public DateTime StartTime(DateTime now) => Mode switch
    {
        StartMode.Now => now,
        StartMode.In => now + (TestSeconds ? TimeSpan.FromSeconds(InMinutes) : TimeSpan.FromMinutes(InMinutes)),
        _ => NextAt(now, Hour, Minute),
    };

    public static DateTime NextAt(DateTime now, int hour, int minute)
    {
        var today = now.Date + new TimeSpan(hour, minute, 0);
        return today > now ? today : today.AddDays(1);
    }

    public static string EndKey(EndAction action) => action switch
    {
        EndAction.Notify => "ScheduleEndNotify",
        EndAction.Sleep => "ScheduleEndSleep",
        EndAction.Shutdown => "ScheduleEndShutdown",
        _ => "ScheduleEndNone",
    };

    /// <summary>"Empieza a las 03:00 (en 2 h 14 min)" o "(mañana)".</summary>
    public static string Describe(DateTime start, DateTime now)
    {
        TimeSpan left = start - now;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        string inText = left.TotalMinutes < 1 ? Strings.F("ScheduleInSeconds", (int)Math.Ceiling(left.TotalSeconds))
            : left.TotalHours < 1 ? Strings.F("ScheduleInMinutes", (int)Math.Ceiling(left.TotalMinutes))
            : Strings.F("ScheduleInHours", (int)left.TotalHours, left.Minutes);
        string at = start.ToString("HH:mm");
        return start.Date > now.Date
            ? Strings.F("ScheduleStartsTomorrow", at, inText)
            : Strings.F("ScheduleStartsAt", at, inText);
    }
}

/// <summary>
/// Suspender o apagar el PC. Con <see cref="TestMode"/> (pruebas automáticas) no hace nada de verdad:
/// solo anota la acción en power-test.txt, junto a los ajustes.
/// </summary>
public static class Power
{
    public static bool TestMode { get; set; }

    /// <summary>Segundos de la cuenta atrás antes de suspender o apagar (pruebas: menos).</summary>
    public static int CountdownSeconds { get; set; } = 60;

    public static void Run(EndAction action)
    {
        if (action is not (EndAction.Sleep or EndAction.Shutdown)) return;
        if (TestMode)
        {
            string file = Path.Combine(Path.GetDirectoryName(new SettingsStore().FilePath)!, "power-test.txt");
            File.AppendAllText(file, action + Environment.NewLine);
            return;
        }
        try
        {
            if (action == EndAction.Shutdown)
                Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false });
            else
                SetSuspendState(false, false, false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ErrorLog.Write("apagar/suspender", ex);
        }
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
