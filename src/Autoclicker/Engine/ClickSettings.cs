namespace Autoclicker.Engine;

public enum MouseButtonKind { Left, Right, Middle }

public enum ClickKind { Single, Double, Hold }

/// <summary>
/// Un punto de la secuencia multi-punto (coordenadas físicas). Los valores null
/// heredan la configuración general (botón, tipo, intervalo).
/// </summary>
public sealed record ClickPoint(int X, int Y, MouseButtonKind? Button = null, ClickKind? Kind = null,
                                int? DelayMs = null, int? HoldMs = null);

/// <summary>Configuración inmutable de una sesión de clics.</summary>
public sealed record ClickSettings
{
    /// <summary>Tiempo entre clics (mínimo 1 ms).</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Variación aleatoria del intervalo, en % (0 = exacto).</summary>
    public double JitterPercent { get; init; }

    public MouseButtonKind Button { get; init; } = MouseButtonKind.Left;

    public ClickKind Kind { get; init; } = ClickKind.Single;

    /// <summary>Tiempo que se mantiene pulsado el botón cuando Kind = Hold.</summary>
    public TimeSpan HoldDuration { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Secuencia multi-punto. Si tiene puntos, se clican en orden y en bucle;
    /// RepeatCount pasa a contar vueltas completas.
    /// </summary>
    public IReadOnlyList<ClickPoint>? Points { get; init; }

    /// <summary>Velocidad de la secuencia de puntos (2 = el doble de rápido). Divide esperas y pulsaciones.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Número de clics (o vueltas, con puntos) antes de parar. 0 = sin límite.</summary>
    public int RepeatCount { get; init; }

    /// <summary>Tiempo máximo clicando. Zero = sin límite.</summary>
    public TimeSpan TimeLimit { get; init; }

    /// <summary>Espera antes del primer clic.</summary>
    public TimeSpan StartDelay { get; init; }

    /// <summary>true = clicar en (X, Y); false = donde esté el cursor.</summary>
    public bool UseFixedPosition { get; init; }

    /// <summary>Coordenadas físicas de pantalla (píxeles reales).</summary>
    public int X { get; init; }
    public int Y { get; init; }

    /// <summary>Desvío aleatorio ± píxeles alrededor de (X, Y). Solo con posición fija.</summary>
    public int RandomOffsetPx { get; init; }

    /// <summary>
    /// Solo clicar mientras siga activa la ventana que lo estaba al empezar. Si cambia,
    /// los clics se pausan y se reanudan al volver a ella.
    /// </summary>
    public bool LockToForegroundWindow { get; init; }

    /// <summary>No clicar sobre las ventanas de esta misma app (evita pulsar "Detener").</summary>
    public bool SkipOwnWindows { get; init; } = true;
}
