namespace Geometry.Shapes;

/// <summary>A type whose name no other type has: a link to it opens it at once.</summary>
public class Circle(double radius)
{
  public double Radius { get; } = radius;

  public double Area() => Math.PI * Radius * Radius;

  public Circle ScaledBy(double factor) => new(Radius * factor);
}
