using Autoclicker.Engine;
using static Autoclicker.Engine.EngineNative;

namespace Autoclicker.Input;

/// <summary>Qué tipo de clic hace el clic por permanencia.</summary>
public enum DwellClick { Left, Right, Double }

/// <summary>
/// Clic por permanencia (2.7, accesibilidad): si el ratón se queda quieto <see cref="DelayMs"/>, clica una vez ahí.
/// Después no vuelve a clicar hasta que el ratón se mueva (más de <see cref="TolerancePx"/>): nunca repite en el
/// mismo sitio. Solo decide; quien lo usa mira el cursor cada pocos milisegundos y dibuja el círculo.
/// </summary>
public sealed class DwellClicker
{
    /// <summary>El círculo aparece pasado este tiempo quieto (así no parpadea con cada pausa mínima).</summary>
    public const int RingAfterMs = 150;

    public int DelayMs { get; set; } = 800;
    public int TolerancePx { get; set; } = 6;
    public DwellClick Click { get; set; } = DwellClick.Left;

    private POINT _anchor;
    private long _stillSinceMs;
    private bool _hasAnchor;
    private bool _armed;

    /// <summary>Olvida dónde estaba: hará falta mover el ratón antes del próximo clic.</summary>
    public void Reset() => _hasAnchor = false;

    /// <summary>
    /// Mira la posición actual. Devuelve true si toca clicar ahora; <paramref name="progress"/> es lo que lleva
    /// el círculo (0-1), o null si no hay que mostrarlo.
    /// </summary>
    internal bool Update(POINT p, long nowMs, bool buttonDown, out double? progress)
    {
        progress = null;
        if (!_hasAnchor || buttonDown)
        {
            // Al empezar, o si el usuario está pulsando un botón (arrastrando...): esperar a que se mueva
            _anchor = p;
            _stillSinceMs = nowMs;
            _hasAnchor = true;
            _armed = false;
            return false;
        }
        if (Math.Max(Math.Abs(p.X - _anchor.X), Math.Abs(p.Y - _anchor.Y)) > TolerancePx)
        {
            _anchor = p;
            _stillSinceMs = nowMs;
            _armed = true;
            return false;
        }
        if (!_armed) return false;

        long still = nowMs - _stillSinceMs;
        if (still >= DelayMs)
        {
            _armed = false; // hasta que se mueva otra vez
            return true;
        }
        if (still >= RingAfterMs) progress = (double)still / DelayMs;
        return false;
    }

    /// <summary>Hace el clic donde está el cursor (eventos marcados como de AutoClik).</summary>
    public void SendClick()
    {
        var settings = new ClickSettings
        {
            Button = Click == DwellClick.Right ? MouseButtonKind.Right : MouseButtonKind.Left,
            Kind = Click == DwellClick.Double ? ClickKind.Double : ClickKind.Single,
            UseFixedPosition = false,
        };
        GetCursorPos(out POINT p);
        MouseInput.Send(settings, p, down: true, up: true);
    }
}
