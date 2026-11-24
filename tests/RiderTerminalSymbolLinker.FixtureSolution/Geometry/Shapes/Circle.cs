namespace Geometry.Shapes;

/// <summary>A type whose name no other type has: a link to it opens it at once.</summary>
public class Circle(double radius)
{
  public double Radius { get; } = radius;

  public double Area() => Math.PI * Radius * Radius;

  public Circle ScaledBy(double factor) => new(Radius * factor);

  /// <summary>An overload: a link to <see cref="ScaledBy(double)"/> names both, so a click lists them.</summary>
  public Circle ScaledBy(int percent) => new(Radius * percent / 100.0);
}
