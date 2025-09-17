using System;
using System.Collections.Generic;

namespace Core.ObjectOriented;

// Test interface implementation and polymorphism
public interface IDrawable
{
    void Draw();
    void Move(int x, int y);
}

public interface IResizable
{
    void Resize(double factor);
    double GetPerimeter();
}

public interface IColorable
{
    string Color { get; set; }
    void ChangeColor(string newColor);
}

// Multiple interface implementation
public class GraphicRectangle : IDrawable, IResizable, IColorable
{

    public GraphicRectangle(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; set; }
    public double Height { get; set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public string Color { get; set; } = "Black";

    public void ChangeColor(string newColor)
    {
        Color = newColor;
    }

    public void Draw()
    {
        Console.WriteLine($"Drawing {Color} rectangle at ({X},{Y}): {Width}x{Height}");
    }

    public void Move(int x, int y)
    {
        X = x;
        Y = y;
    }

    public void Resize(double factor)
    {
        Width *= factor;
        Height *= factor;
    }

    public double GetPerimeter()
    {
        return 2 * (Width + Height);
    }
}

public class GraphicCircle : IDrawable, IColorable
{

    public GraphicCircle(double radius)
    {
        Radius = radius;
    }

    public double Radius { get; set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public string Color { get; set; } = "Red";

    public void ChangeColor(string newColor)
    {
        Color = newColor;
    }

    public void Draw()
    {
        Console.WriteLine($"Drawing {Color} circle at ({X},{Y}): radius {Radius}");
    }

    public void Move(int x, int y)
    {
        X = x;
        Y = y;
    }
}

// Interface inheritance
public interface IAdvancedDrawable : IDrawable
{
    bool IsVisible { get; set; }
    void DrawWithEffect(string effect);
}

public class AdvancedShape : IAdvancedDrawable
{
    public bool IsVisible { get; set; } = true;

    public void Draw()
    {
        if (IsVisible)
            Console.WriteLine("Drawing advanced shape");
    }

    public void Move(int x, int y)
    {
        Console.WriteLine($"Moving to ({x},{y})");
    }

    public void DrawWithEffect(string effect)
    {
        Console.WriteLine($"Drawing with effect: {effect}");
    }
}

// Polymorphism test
public class ShapeManager
{
    private readonly List<IDrawable> _shapes = new();

    public void AddShape(IDrawable shape)
    {
        _shapes.Add(shape);
    }

    public void DrawAllShapes()
    {
        foreach (var shape in _shapes)
        {
            shape.Draw();

            if (shape is IColorable colorable)
            {
                Console.WriteLine($"Shape color: {colorable.Color}");
            }

            if (shape is IResizable resizable)
            {
                Console.WriteLine($"Shape perimeter: {resizable.GetPerimeter()}");
            }
        }
    }

    public void MoveAllShapes(int deltaX, int deltaY)
    {
        foreach (var shape in _shapes)
        {
            shape.Move(deltaX, deltaY);
        }
    }
}