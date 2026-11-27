namespace Geometry.Shapes;

/// <summary>
///   A colour sample, declared in two parts: this one, and the one the Geometry project's source generator writes, with
///   Shade.
/// </summary>
public sealed partial class Swatch
{
  public string Colour { get; } = "red";

  /// <summary>Calls a generated member of this type and one of a type the generator writes whole.</summary>
  public string Describe() => $"{Colour} at {Shade()} of {Ruler.Doubled(50)}";
}
