using Contracts.Graph;

// ReSharper disable StringLiteralTypo

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class ObjectOrientedApprovalTests : ProjectTestBase
{
    private CodeGraph GetTestAssemblyGraph()
    {
        return GetAssemblyGraph("Core.ObjectOriented");
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var classes = GetAllClasses(GetTestAssemblyGraph());

        var expected = new[]
        {
            "Core.ObjectOriented.Core.ObjectOriented.AbstractShape",
            "Core.ObjectOriented.Core.ObjectOriented.Rectangle",
            "Core.ObjectOriented.Core.ObjectOriented.Circle",
            "Core.ObjectOriented.Core.ObjectOriented.ColoredRectangle",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicCircle",
            "Core.ObjectOriented.Core.ObjectOriented.AdvancedShape",
            "Core.ObjectOriented.Core.ObjectOriented.ShapeManager",
            "Core.ObjectOriented.Core.ObjectOriented.Animal",
            "Core.ObjectOriented.Core.ObjectOriented.Dog",
            "Core.ObjectOriented.Core.ObjectOriented.AnimalShelter",
            "Core.ObjectOriented.Core.ObjectOriented.Bird",
            "Core.ObjectOriented.Core.ObjectOriented.Car",
            "Core.ObjectOriented.Core.ObjectOriented.Cat",
            "Core.ObjectOriented.Core.ObjectOriented.Motorcycle",
            "Core.ObjectOriented.Core.ObjectOriented.Vehicle"
        };

        CollectionAssert.AreEquivalent(expected, classes);
    }

    [Test]
    public void InheritanceRelationships_ShouldBeDetected()
    {
        var inheritanceRelationships = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Inherits)
            ;

        var expected = new[]
        {
            "Core.ObjectOriented.Core.ObjectOriented.Rectangle -> Core.ObjectOriented.Core.ObjectOriented.AbstractShape",
            "Core.ObjectOriented.Core.ObjectOriented.Circle -> Core.ObjectOriented.Core.ObjectOriented.AbstractShape",
            "Core.ObjectOriented.Core.ObjectOriented.ColoredRectangle -> Core.ObjectOriented.Core.ObjectOriented.Rectangle",
            "Core.ObjectOriented.Core.ObjectOriented.Dog -> Core.ObjectOriented.Core.ObjectOriented.Animal",
            "Core.ObjectOriented.Core.ObjectOriented.Cat -> Core.ObjectOriented.Core.ObjectOriented.Animal",
            "Core.ObjectOriented.Core.ObjectOriented.Bird -> Core.ObjectOriented.Core.ObjectOriented.Animal",
            "Core.ObjectOriented.Core.ObjectOriented.Car -> Core.ObjectOriented.Core.ObjectOriented.Vehicle",
            "Core.ObjectOriented.Core.ObjectOriented.Motorcycle -> Core.ObjectOriented.Core.ObjectOriented.Vehicle"
        };

        CollectionAssert.AreEquivalent(expected, inheritanceRelationships.ToArray());
    }

    [Test]
    public void ImplementsRelationships_ShouldBeDetected()
    {
        var inheritanceRelationships = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Implements);

        var expected = new[]
        {
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle -> Core.ObjectOriented.Core.ObjectOriented.IDrawable",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle -> Core.ObjectOriented.Core.ObjectOriented.IResizable",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle -> Core.ObjectOriented.Core.ObjectOriented.IColorable",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle.Color -> Core.ObjectOriented.Core.ObjectOriented.IColorable.Color",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle.Draw -> Core.ObjectOriented.Core.ObjectOriented.IDrawable.Draw",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle.Move -> Core.ObjectOriented.Core.ObjectOriented.IDrawable.Move",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle.Resize -> Core.ObjectOriented.Core.ObjectOriented.IResizable.Resize",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle.GetPerimeter -> Core.ObjectOriented.Core.ObjectOriented.IResizable.GetPerimeter",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicRectangle.ChangeColor -> Core.ObjectOriented.Core.ObjectOriented.IColorable.ChangeColor",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicCircle -> Core.ObjectOriented.Core.ObjectOriented.IDrawable",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicCircle -> Core.ObjectOriented.Core.ObjectOriented.IColorable",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicCircle.Color -> Core.ObjectOriented.Core.ObjectOriented.IColorable.Color",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicCircle.Draw -> Core.ObjectOriented.Core.ObjectOriented.IDrawable.Draw",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicCircle.Move -> Core.ObjectOriented.Core.ObjectOriented.IDrawable.Move",
            "Core.ObjectOriented.Core.ObjectOriented.GraphicCircle.ChangeColor -> Core.ObjectOriented.Core.ObjectOriented.IColorable.ChangeColor",
            "Core.ObjectOriented.Core.ObjectOriented.IAdvancedDrawable -> Core.ObjectOriented.Core.ObjectOriented.IDrawable",
            "Core.ObjectOriented.Core.ObjectOriented.AdvancedShape -> Core.ObjectOriented.Core.ObjectOriented.IAdvancedDrawable",
            "Core.ObjectOriented.Core.ObjectOriented.AdvancedShape.IsVisible -> Core.ObjectOriented.Core.ObjectOriented.IAdvancedDrawable.IsVisible",
            "Core.ObjectOriented.Core.ObjectOriented.AdvancedShape.Draw -> Core.ObjectOriented.Core.ObjectOriented.IDrawable.Draw",
            "Core.ObjectOriented.Core.ObjectOriented.AdvancedShape.Move -> Core.ObjectOriented.Core.ObjectOriented.IDrawable.Move",
            "Core.ObjectOriented.Core.ObjectOriented.AdvancedShape.DrawWithEffect -> Core.ObjectOriented.Core.ObjectOriented.IAdvancedDrawable.DrawWithEffect"
        };

        CollectionAssert.AreEquivalent(expected, inheritanceRelationships.ToArray());
    }
}