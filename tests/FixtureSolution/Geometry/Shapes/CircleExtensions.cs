namespace Geometry.Shapes;

/// <summary>
///   A C# 14 extension block, whose members the MCP's releases list in two ways: a link to one must open it at once.
/// </summary>
public static class CircleExtensions
{
  extension(Circle circle)
  {
    public double Diameter => circle.Radius * 2;
  }
}
