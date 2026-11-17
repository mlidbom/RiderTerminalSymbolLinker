namespace Geometry.Shapes;

/// <summary>
///   A C# 14 extension block: ReSharper reports its members twice, and a link to one must still open it at once.
/// </summary>
public static class CircleExtensions
{
  extension(Circle circle)
  {
    public double Diameter => circle.Radius * 2;
  }
}
