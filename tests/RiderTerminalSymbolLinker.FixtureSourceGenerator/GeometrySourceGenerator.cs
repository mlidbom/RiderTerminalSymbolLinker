using Microsoft.CodeAnalysis;

namespace RiderTerminalSymbolLinker.FixtureSourceGenerator;

/// <summary>
///   The fixture solution's Geometry project's source generator, for the specifications of links to code no file of the
///   solution declares: a type it writes whole, <c>Geometry.Shapes.Ruler</c>, and a member it writes into a type whose
///   other part the solution declares, <c>Geometry.Shapes.Swatch.Shade</c>, beside <c>Shapes/Swatch.cs</c>.
/// </summary>
/// <remarks>
///   Its output is the same whatever the project holds, so Rider has it as soon as it runs the generator, and the
///   specifications don't depend on how soon it runs it again after a change.
/// </remarks>
[Generator]
public sealed class GeometrySourceGenerator : IIncrementalGenerator
{
   public void Initialize(IncrementalGeneratorInitializationContext context) =>
      context.RegisterPostInitializationOutput(it =>
      {
         it.AddSource("Ruler.g.cs", """
                                    namespace Geometry.Shapes;

                                    /// <summary>A type no file of the solution declares: the source generator writes all of it.</summary>
                                    public static class Ruler
                                    {
                                       public static int Doubled(int length) => length * 2;
                                    }
                                    """);
         it.AddSource("Swatch.g.cs", """
                                     namespace Geometry.Shapes;

                                     public sealed partial class Swatch
                                     {
                                        /// <summary>A member the source generator writes into a type whose other part Swatch.cs declares.</summary>
                                        public int Shade() => Ruler.Doubled(25);
                                     }
                                     """);
      });
}
