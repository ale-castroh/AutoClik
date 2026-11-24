using Autoclicker.Engine;

namespace Autoclicker.Markers;

/// <summary>Punto editable de la secuencia (coordenadas físicas). null = usar el valor general.</summary>
public sealed class MarkerPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public MouseButtonKind? Button { get; set; }
    public ClickKind? Kind { get; set; }
    public int? DelayMs { get; set; }
    public int? HoldMs { get; set; }

    public ClickPoint ToClickPoint() => new(X, Y, Button, Kind, DelayMs, HoldMs);
}
