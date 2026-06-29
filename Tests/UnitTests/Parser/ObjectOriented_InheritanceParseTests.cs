using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Inheritance hierarchies: abstract base with virtual/abstract members, multi-level inheritance and
///     constructor chaining (": base(...)" and ": this(...)"). Migrated from the former
///     Core.ObjectOriented approval fixture (Inheritance.cs).
/// </summary>
[TestFixture]
public class ObjectOriented_InheritanceParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                     using System;

                                     namespace Demo;

                                     public abstract class AbstractShape
                                     {
                                         protected string Name { get; set; } = string.Empty;

                                         public abstract double GetArea();

                                         public virtual void Display()
                                         {
                                             Console.WriteLine($"Shape: {Name}");
                                         }

                                         protected abstract void ValidateProperties();
                                     }

                                     public class Rectangle : AbstractShape
                                     {
                                         public Rectangle(double width, double height)
                                         {
                                             Width = width;
                                             Height = height;
                                             Name = "Rectangle";
                                             ValidateProperties();
                                         }

                                         public double Width { get; set; }
                                         public double Height { get; set; }

                                         public override double GetArea()
                                         {
                                             return Width * Height;
                                         }

                                         protected override void ValidateProperties()
                                         {
                                             if (Width <= 0 || Height <= 0)
                                                 throw new ArgumentException("Dimensions must be positive");
                                         }

                                         public override void Display()
                                         {
                                             base.Display();
                                             Console.WriteLine($"Dimensions: {Width}x{Height}");
                                         }
                                     }

                                     public class Circle : AbstractShape
                                     {
                                         public Circle(double radius)
                                         {
                                             Radius = radius;
                                             Name = "Circle";
                                             ValidateProperties();
                                         }

                                         public double Radius { get; set; }

                                         public override double GetArea()
                                         {
                                             return Math.PI * Radius * Radius;
                                         }

                                         protected override void ValidateProperties()
                                         {
                                             if (Radius <= 0)
                                                 throw new ArgumentException("Radius must be positive");
                                         }
                                     }

                                     // Multiple inheritance levels
                                     public class ColoredRectangle : Rectangle
                                     {
                                         public ColoredRectangle(double width, double height, string color)
                                             : base(width, height)
                                         {
                                             Color = color;
                                         }

                                         public string Color { get; set; }

                                         public override void Display()
                                         {
                                             base.Display();
                                             Console.WriteLine($"Color: {Color}");
                                         }
                                     }

                                     // Constructor chaining to another constructor of the same class (": this(...)").
                                     public class Square
                                     {
                                         private readonly double _side;

                                         public Square() : this(1.0)
                                         {
                                         }

                                         public Square(double side)
                                         {
                                             _side = side;
                                         }
                                     }
                                     """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[] { "AbstractShape", "Rectangle", "Circle", "ColoredRectangle", "Square" };
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void InheritanceRelationships_AreDetected()
    {
        var expected = new[]
        {
            "Rectangle -> AbstractShape",
            "Circle -> AbstractShape",
            "ColoredRectangle -> Rectangle"
        };

        Assert.That(RelsOf(RelationshipType.Inherits), Is.EquivalentTo(expected));
    }

    [Test]
    public void ConstructorChaining_IsDetected()
    {
        var calls = RelsOf(RelationshipType.Calls);

        // ": base(...)" links to the base constructor; ": this(...)" links to the peer constructor.
        Assert.That(calls, Does.Contain("ColoredRectangle..ctor -> Rectangle..ctor"));
        Assert.That(calls, Does.Contain("Square..ctor -> Square..ctor"));
    }
}
