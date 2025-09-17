using System;

namespace Core.ObjectOriented;

// Test inheritance hierarchies
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