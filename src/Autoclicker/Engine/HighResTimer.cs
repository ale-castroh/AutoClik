using Microsoft.Win32.SafeHandles;
using static Autoclicker.Engine.EngineNative;

namespace Autoclicker.Engine;

/// <summary>
/// Temporizador de espera de alta resolución (Windows 10 1803+). Permite dormir el hilo
/// con precisión sub-milisegundo sin cambiar la resolución global del reloj del sistema.
/// En sistemas antiguos cae a un temporizador normal + timeBeginPeriod(1).
/// </summary>
internal sealed class HighResTimer : WaitHandle
{
    private readonly bool _legacy;

    private HighResTimer(nint handle, bool legacy)
    {
        SafeWaitHandle = new SafeWaitHandle(handle, ownsHandle: true);
        _legacy = legacy;
    }

    public static HighResTimer Create()
    {
        nint h = CreateWaitableTimerExW(0, 0, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
        if (h != 0) return new HighResTimer(h, legacy: false);

        h = CreateWaitableTimerExW(0, 0, 0, TIMER_ALL_ACCESS);
        if (h == 0) throw new System.ComponentModel.Win32Exception();
        timeBeginPeriod(1);
        return new HighResTimer(h, legacy: true);
    }

    /// <summary>Arma el temporizador para dentro de <paramref name="hundredNs"/> unidades de 100 ns.</summary>
    public void Arm(long hundredNs)
    {
        long due = -Math.Max(hundredNs, 1); // negativo = relativo
        SetWaitableTimer(SafeWaitHandle.DangerousGetHandle(), ref due, 0, 0, 0, 0);
    }

    protected override void Dispose(bool explicitDisposing)
    {
        if (_legacy) timeEndPeriod(1);
        base.Dispose(explicitDisposing);
    }
}
