namespace Geometry.Shapes;

/// <summary>A type with a type nested in it, whose members must link as the outer type's do.</summary>
public class Drawing
{
  public record Stroke(double Width)
  {
    public Stroke Thicker() => this with { Width = Width * 2 };
  }
}
