using CodeGraph.Export;
using CodeGraph.Graph;
using CodeParserTests.Helper;

namespace CodeParserTests.UnitTests.Export;

[TestFixture]
public class PlantUmlExportTests
{
    private static string Export(CodeGraph.Graph.CodeGraph g)
    {
        var exporter = new PlantUmlExport();
        return exporter.Export(g);
    }

    private static TestCodeGraph BuildBasicGraph()
    {
        var g = new TestCodeGraph();

        // Assembly & namespace conflict scenario
        var asm = g.CreateAssembly("MyAsm");
        var nsExport = g.CreateNamespace("Export", asm);

        // Types
        var iface = g.CreateInterface("Export-IService", nsExport);
        var baseClass = g.CreateClass("Export-Base", nsExport);

        // Dots should be replaced in alias (aka FullName)
        var implClass = g.CreateClass("Export-ServiceImpl", nsExport, "Export.ServiceImpl");


        var structType = g.CreateStruct("Export-Data", nsExport);
        var enumType = g.CreateEnum("Export-Color", nsExport);
        var recordType = g.CreateRecord("Export-RecordA", nsExport);
        var delegateType = g.CreateDelegate("Export-StringHandler", nsExport);
        var conflictClass = g.CreateClass("Export-Export", nsExport); // name same as namespace

        // Members in impl class (order check)
        var methodA = g.CreateMethod("Export-ServiceImpl.DoWork", implClass);
        var propertyA = g.CreateProperty("Export-ServiceImpl.State", implClass);
        var eventA = g.CreateEvent("Export-ServiceImpl.Changed", implClass);
        var fieldRef = g.CreateField("Export-ServiceImpl._data", implClass);

        // Relationships
        implClass.Relationships.Add(new Relationship(implClass.Id, iface.Id, RelationshipType.Implements));
        implClass.Relationships.Add(new Relationship(implClass.Id, baseClass.Id, RelationshipType.Inherits));
        // Field association (_data -> Export.Data)
        fieldRef.Relationships.Add(new Relationship(fieldRef.Id, structType.Id, RelationshipType.Uses));
        // Additional weak dependency from method to enum
        methodA.Relationships.Add(new Relationship(methodA.Id, enumType.Id, RelationshipType.Uses));
        // Record creation
        methodA.Relationships.Add(new Relationship(methodA.Id, recordType.Id, RelationshipType.Creates));
        // Delegate usage
        methodA.Relationships.Add(new Relationship(methodA.Id, delegateType.Id, RelationshipType.Uses));
        // Self method call (should not create self weak dependency arrow)
        methodA.Relationships.Add(new Relationship(methodA.Id, methodA.Id, RelationshipType.Calls));

        return g;
    }

    [Test]
    public void Export_ShouldContainHeaderAndFooter()
    {
        var text = Export(BuildBasicGraph());
        Assert.That(text.Contains("@startuml"));
        Assert.That(text.Contains("@enduml"));
    }

    [Test]
    public void TypeAlias_ShouldReplaceDotsWithUnderscores()
    {
        var text = Export(BuildBasicGraph());
        // Alias example for Export.ServiceImpl -> Export_ServiceImpl
        Assert.That(text.Contains(@"class ""Export_ServiceImpl"" as Export_ServiceImpl"));
    }

    [Test]
    public void NamespaceAndClassSameName_ShouldUseAliasForClass()
    {
        var text = Export(BuildBasicGraph());
        // Namespace block header
        Assert.That(text.Contains("namespace Export {"));
        // Class with same name must have alias Export_Export
        // The underscore in the class name is sanitized.
        Assert.That(text.Contains("class \"Export_Export\" as Export_Export"));
    }

    [Test]
    public void Members_ShouldBeOrderedAndFormatted()
    {
        var text = Export(BuildBasicGraph());
        // Extract block for ServiceImpl
        var lines = text.Split('\n');
        var startIndex = Array.FindIndex(lines, l => l.Contains("class \"Export_ServiceImpl\" as Export_ServiceImpl {"));
        Assert.That(startIndex, Is.GreaterThan(-1));
        var endIndex = Array.FindIndex(lines, startIndex + 1, l => l.Trim() == "}");
        Assert.That(endIndex, Is.GreaterThan(startIndex));
        var memberLines = lines.Skip(startIndex + 1).Take(endIndex - startIndex - 1).Select(l => l.Trim()).ToList();

        // Expect order: Method (), Property, Event, Field
        // Note class members allow '-' so it is not sanitized here, but the class name is.
        var expectedOrder = new[] { "Export-ServiceImpl.DoWork()", "Export-ServiceImpl.State", "Export-ServiceImpl.Changed", "Export-ServiceImpl._data" };
        Assert.That(memberLines, Is.EqualTo(expectedOrder));
    }

    [Test]
    public void Stereotypes_ShouldBeRendered()
    {
        var text = Export(BuildBasicGraph());
        Assert.That(text.Contains("Export_IService <<interface>>"));
        Assert.That(text.Contains("Export_Data <<struct>>"));
        Assert.That(text.Contains("Export_Color <<enumeration>>"));
        Assert.That(text.Contains("Export_RecordA <<record>>"));
        Assert.That(text.Contains("Export_StringHandler <<delegate>>"));
    }

    [Test]
    public void InheritsAndImplements_ShouldUseCorrectArrows()
    {
        var text = Export(BuildBasicGraph());
        Assert.That(text.Contains("Export_ServiceImpl --|> Export_Base")); // inherits
        Assert.That(text.Contains("Export_ServiceImpl ..|> Export_IService")); // implements
    }

    [Test]
    public void FieldAssociation_ShouldUseDirectedAssociation_NotWeakDependency()
    {
        var text = Export(BuildBasicGraph());
        // Association arrow
        var association = "Export_ServiceImpl --> Export_Data";
        Assert.That(text.Contains(association));
        // No weak dependency arrow for same pair
        Assert.That(!text.Contains("Export_ServiceImpl ..> Export_Data"));
    }

    [Test]
    public void WeakDependencies_ShouldUseDoubleDotArrow()
    {
        var text = Export(BuildBasicGraph());
        // Example: method uses enum -> ServiceImpl depends on Color
        Assert.That(text.Contains("Export_ServiceImpl ..> Export_Color"));
    }

    [Test]
    public void SelfWeakDependency_ShouldNotBeCreated()
    {
        var text = Export(BuildBasicGraph());
        // Ensure no self dependency arrow for weak dependency
        Assert.That(!text.Contains("Export_ServiceImpl ..> Export_ServiceImpl"));
    }
}