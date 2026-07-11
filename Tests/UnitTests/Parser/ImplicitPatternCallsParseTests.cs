using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Compiler-invoked pattern methods: a deconstruction ("var (x, y) = point;") calls the user-defined
///     Deconstruct method, a foreach over a duck-typed collection calls GetEnumerator. Neither appears as
///     an invocation in the syntax tree, so no edge is recorded and the pattern methods look unused.
///     MoveNext/Current on the enumerator are deliberately not asserted - whether those secondary edges
///     are wanted is a modelling decision for the implementation.
/// </summary>
[TestFixture]
public class ImplicitPatternCallsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public class Point
                                      {
                                          public void Deconstruct(out int x, out int y)
                                          {
                                              x = 0;
                                              y = 0;
                                          }
                                      }

                                      public class PointEnumerator
                                      {
                                          public Point Current
                                          {
                                              get { return new Point(); }
                                          }

                                          public bool MoveNext()
                                          {
                                              return false;
                                          }
                                      }

                                      public class PointCollection
                                      {
                                          public PointEnumerator GetEnumerator()
                                          {
                                              return new PointEnumerator();
                                          }
                                      }

                                      public class Reader
                                      {
                                          public void Split(Point point)
                                          {
                                              var (x, y) = point;
                                          }

                                          public void Iterate(PointCollection points)
                                          {
                                              foreach (var point in points)
                                              {
                                              }
                                          }
                                      }
                                      """;

    [Test]
    public void PatternMethodElements_AreDetected()
    {
        // Premise guard (green): the pattern methods exist as elements.
        var methods = PathsOf(CodeElementType.Method);
        Assert.Multiple(() =>
        {
            Assert.That(methods, Does.Contain("Point.Deconstruct"));
            Assert.That(methods, Does.Contain("PointCollection.GetEnumerator"));
        });
    }

    [Test]
    public void Deconstruction_CallsDeconstruct()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Reader.Split -> Point.Deconstruct"));
    }

    [Test]
    public void ForEach_CallsGetEnumerator()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Reader.Iterate -> PointCollection.GetEnumerator"));
    }
}
