using Contracts.Graph;

// ReSharper disable StringLiteralTypo

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class ObjectOrientedApprovalTests : ApprovalTestBase
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
            "Core.ObjectOriented.global.Core.ObjectOriented.AbstractShape",
            "Core.ObjectOriented.global.Core.ObjectOriented.Rectangle",
            "Core.ObjectOriented.global.Core.ObjectOriented.Circle",
            "Core.ObjectOriented.global.Core.ObjectOriented.ColoredRectangle",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicCircle",
            "Core.ObjectOriented.global.Core.ObjectOriented.AdvancedShape",
            "Core.ObjectOriented.global.Core.ObjectOriented.ShapeManager",
            "Core.ObjectOriented.global.Core.ObjectOriented.Animal",
            "Core.ObjectOriented.global.Core.ObjectOriented.Dog",
            "Core.ObjectOriented.global.Core.ObjectOriented.AnimalShelter",
            "Core.ObjectOriented.global.Core.ObjectOriented.Bird",
            "Core.ObjectOriented.global.Core.ObjectOriented.Car",
            "Core.ObjectOriented.global.Core.ObjectOriented.Cat",
            "Core.ObjectOriented.global.Core.ObjectOriented.Motorcycle",
            "Core.ObjectOriented.global.Core.ObjectOriented.Vehicle"
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
            "Core.ObjectOriented.global.Core.ObjectOriented.Rectangle -> Core.ObjectOriented.global.Core.ObjectOriented.AbstractShape",
            "Core.ObjectOriented.global.Core.ObjectOriented.Circle -> Core.ObjectOriented.global.Core.ObjectOriented.AbstractShape",
            "Core.ObjectOriented.global.Core.ObjectOriented.ColoredRectangle -> Core.ObjectOriented.global.Core.ObjectOriented.Rectangle",
            "Core.ObjectOriented.global.Core.ObjectOriented.Dog -> Core.ObjectOriented.global.Core.ObjectOriented.Animal",
            "Core.ObjectOriented.global.Core.ObjectOriented.Cat -> Core.ObjectOriented.global.Core.ObjectOriented.Animal",
            "Core.ObjectOriented.global.Core.ObjectOriented.Bird -> Core.ObjectOriented.global.Core.ObjectOriented.Animal",
            "Core.ObjectOriented.global.Core.ObjectOriented.Car -> Core.ObjectOriented.global.Core.ObjectOriented.Vehicle",
            "Core.ObjectOriented.global.Core.ObjectOriented.Motorcycle -> Core.ObjectOriented.global.Core.ObjectOriented.Vehicle"
        };

        CollectionAssert.AreEquivalent(expected, inheritanceRelationships.ToArray());
    }

    [Test]
    public void ImplementsRelationships_ShouldBeDetected()
    {
        var inheritanceRelationships = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Implements);

        var expected = new[]
        {
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle -> Core.ObjectOriented.global.Core.ObjectOriented.IResizable",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle -> Core.ObjectOriented.global.Core.ObjectOriented.IColorable",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle.Color -> Core.ObjectOriented.global.Core.ObjectOriented.IColorable.Color",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle.Draw -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable.Draw",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle.Move -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable.Move",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle.Resize -> Core.ObjectOriented.global.Core.ObjectOriented.IResizable.Resize",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle.GetPerimeter -> Core.ObjectOriented.global.Core.ObjectOriented.IResizable.GetPerimeter",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicRectangle.ChangeColor -> Core.ObjectOriented.global.Core.ObjectOriented.IColorable.ChangeColor",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicCircle -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicCircle -> Core.ObjectOriented.global.Core.ObjectOriented.IColorable",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicCircle.Color -> Core.ObjectOriented.global.Core.ObjectOriented.IColorable.Color",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicCircle.Draw -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable.Draw",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicCircle.Move -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable.Move",
            "Core.ObjectOriented.global.Core.ObjectOriented.GraphicCircle.ChangeColor -> Core.ObjectOriented.global.Core.ObjectOriented.IColorable.ChangeColor",
            "Core.ObjectOriented.global.Core.ObjectOriented.IAdvancedDrawable -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable",
            "Core.ObjectOriented.global.Core.ObjectOriented.AdvancedShape -> Core.ObjectOriented.global.Core.ObjectOriented.IAdvancedDrawable",
            "Core.ObjectOriented.global.Core.ObjectOriented.AdvancedShape.IsVisible -> Core.ObjectOriented.global.Core.ObjectOriented.IAdvancedDrawable.IsVisible",
            "Core.ObjectOriented.global.Core.ObjectOriented.AdvancedShape.Draw -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable.Draw",
            "Core.ObjectOriented.global.Core.ObjectOriented.AdvancedShape.Move -> Core.ObjectOriented.global.Core.ObjectOriented.IDrawable.Move",
            "Core.ObjectOriented.global.Core.ObjectOriented.AdvancedShape.DrawWithEffect -> Core.ObjectOriented.global.Core.ObjectOriented.IAdvancedDrawable.DrawWithEffect"
        };

        CollectionAssert.AreEquivalent(expected, inheritanceRelationships.ToArray());
    }
}