using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Importers.Dart;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     Feeds the converter a JSON sample in the shape the DartExtractor tool emits. The sample
///     mirrors what a small Flutter app produces: a package assembly with a path/library namespace
///     chain, a library at package root (artificial "global" namespace), external elements from the
///     Flutter SDK with their full parent chain, and the whole relationship vocabulary.
///     Elements are deliberately listed child-before-parent - the JSON carries no ordering guarantee.
/// </summary>
[TestFixture]
public class DartGraphConverterTests
{
    [OneTimeSetUp]
    public void SetUp()
    {
        _jsonPath = Path.Combine(Path.GetTempPath(), "DartGraphConverterTests_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(_jsonPath, Json);

        var converter = new DartGraphConverter();
        _graph = converter.ConvertFile(_jsonPath);
        _converter = converter;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        File.Delete(_jsonPath);
    }

    private string _jsonPath = null!;
    private CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph _graph = null!;
    private DartGraphConverter _converter = null!;

    private CodeElement ByFullName(string fullName)
    {
        return _graph.Nodes.Values.Single(e => e.FullName == fullName);
    }

    [Test]
    public void BuildsHierarchyRegardlessOfElementOrder()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ByFullName("app").ElementType, Is.EqualTo(CodeElementType.Assembly));
            Assert.That(ByFullName("app.features").ElementType, Is.EqualTo(CodeElementType.Namespace));
            Assert.That(ByFullName("app.features.login_page.LoginPage").ElementType, Is.EqualTo(CodeElementType.Class));
            Assert.That(ByFullName("app.features.login_page.LoginPage.build").Parent?.FullName,
                Is.EqualTo("app.features.login_page.LoginPage"));

            // A library at package root has no path segments of its own.
            Assert.That(ByFullName("app.global.main").ElementType, Is.EqualTo(CodeElementType.Method));
            Assert.That(ByFullName("app.global").Name, Is.EqualTo(CodeElement.GlobalNamespaceName));
        });
    }

    [Test]
    public void MarksSdkElementsAsExternal()
    {
        Assert.Multiple(() =>
        {
            // The whole parent chain of an external element is external, too.
            Assert.That(ByFullName("flutter").IsExternal, Is.True);
            Assert.That(ByFullName("flutter.src.widgets.framework").IsExternal, Is.True);
            Assert.That(ByFullName("flutter.src.widgets.framework.StatelessWidget").IsExternal, Is.True);

            Assert.That(ByFullName("app").IsExternal, Is.False);
            Assert.That(ByFullName("app.features.login_page.LoginPage").IsExternal, Is.False);
        });
    }

    [Test]
    public void KeepsSourceLocations()
    {
        var element = ByFullName("app.features.login_page.LoginPage");
        var location = element.SourceLocations.Single();

        Assert.Multiple(() =>
        {
            Assert.That(location.Line, Is.EqualTo(12));
            Assert.That(location.Column, Is.EqualTo(7));
            // Forward slashes from the Dart side become native separators.
            Assert.That(location.File, Does.Contain(Path.DirectorySeparatorChar));
        });
    }

    [Test]
    public void CreatesRelationships()
    {
        var relationships = _graph.GetAllRelationships()
            .Select(r => (_graph.Nodes[r.SourceId].FullName, r.Type, _graph.Nodes[r.TargetId].FullName))
            .ToHashSet();

        Assert.That(relationships, Is.EquivalentTo(new[]
        {
            ("app.features.login_page.LoginPage", RelationshipType.Inherits, "flutter.src.widgets.framework.StatelessWidget"),
            ("app.features.login_page.LoginPage.build", RelationshipType.Overrides, "flutter.src.widgets.framework.StatelessWidget.build"),
            ("app.features.login_page.LoginPage.build", RelationshipType.Uses, "app.features.login_page.LoginPage.title"),
            // Creates points at the type, the written constructor gets its own Calls edge.
            ("app.global.main", RelationshipType.Creates, "app.features.login_page.LoginPage"),
            ("app.global.main", RelationshipType.Calls, "app.features.login_page.LoginPage.new"),
            ("app.global.main", RelationshipType.Calls, "flutter.src.widgets.framework.StatelessWidget.build")
        }));
    }

    [Test]
    public void SkipsUnknownTypesAndDanglingReferencesInsteadOfThrowing()
    {
        Assert.Multiple(() =>
        {
            // "Gadget" has an element type the C# side does not know.
            Assert.That(_graph.Nodes.Values.Any(e => e.Name == "Gadget"), Is.False);
            Assert.That(_converter.SkippedElements, Is.EqualTo(1));

            // One relationship points at the skipped element, one uses an unknown type.
            Assert.That(_converter.SkippedRelationships, Is.EqualTo(2));
        });
    }

    [Test]
    public void FillsTheMetricStore()
    {
        var build = ByFullName("app.features.login_page.LoginPage.build");
        var metrics = _converter.Metrics.TryGet(build.Id);

        Assert.Multiple(() =>
        {
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics!.CodeLines, Is.EqualTo(26));
            Assert.That(metrics.CommentLines, Is.EqualTo(4));
            Assert.That(metrics.LogicalLinesOfCode, Is.EqualTo(1));
            Assert.That(metrics.CyclomaticComplexity, Is.EqualTo(3));

            // Only members with a body are measured, so a store entry is the exception, not the rule.
            Assert.That(_converter.Metrics.TryGet(ByFullName("app.features.login_page.LoginPage").Id), Is.Null);

            // "Gadget" was skipped as an element, so its metrics must not linger in the store.
            Assert.That(_converter.Metrics.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void RejectsAnIncompatibleFormatVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), "DartGraphConverterTests_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{"format":99,"projectName":"app","elements":[],"relationships":[],"metrics":{}}""");
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new DartGraphConverter().ConvertFile(path));
            Assert.That(exception!.Message, Does.Contain("99"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private const string Json =
        """
        {
          "format": 2,
          "projectName": "app",
          "elements": [
            { "id": "m:build", "type": "Method", "name": "build", "parent": "t:LoginPage",
              "location": { "file": "lib/features/login_page.dart", "line": 20, "column": 3 } },
            { "id": "t:LoginPage", "type": "Class", "name": "LoginPage", "parent": "ns:app/features/login_page",
              "location": { "file": "lib/features/login_page.dart", "line": 12, "column": 7 } },
            { "id": "f:title", "type": "Field", "name": "title", "parent": "t:LoginPage" },
            { "id": "c:LoginPage.new", "type": "Method", "name": "new", "parent": "t:LoginPage" },
            { "id": "ns:app/features/login_page", "type": "Namespace", "name": "login_page", "parent": "ns:app/features" },
            { "id": "ns:app/features", "type": "Namespace", "name": "features", "parent": "pkg:app" },
            { "id": "pkg:app", "type": "Assembly", "name": "app" },

            { "id": "ns:app/global", "type": "Namespace", "name": "global", "parent": "pkg:app" },
            { "id": "m:main", "type": "Method", "name": "main", "parent": "ns:app/global" },

            { "id": "pkg:flutter", "type": "Assembly", "name": "flutter", "external": true },
            { "id": "ns:flutter/src", "type": "Namespace", "name": "src", "parent": "pkg:flutter", "external": true },
            { "id": "ns:flutter/src/widgets", "type": "Namespace", "name": "widgets", "parent": "ns:flutter/src", "external": true },
            { "id": "ns:flutter/src/widgets/framework", "type": "Namespace", "name": "framework", "parent": "ns:flutter/src/widgets", "external": true },
            { "id": "t:StatelessWidget", "type": "Class", "name": "StatelessWidget", "parent": "ns:flutter/src/widgets/framework", "external": true },
            { "id": "m:StatelessWidget.build", "type": "Method", "name": "build", "parent": "t:StatelessWidget", "external": true },

            { "id": "t:Gadget", "type": "ExtensionType", "name": "Gadget", "parent": "ns:app/features" }
          ],
          "relationships": [
            { "source": "t:LoginPage", "target": "t:StatelessWidget", "type": "Inherits" },
            { "source": "m:build", "target": "m:StatelessWidget.build", "type": "Overrides" },
            { "source": "m:build", "target": "f:title", "type": "Uses" },
            { "source": "m:main", "target": "t:LoginPage", "type": "Creates" },
            { "source": "m:main", "target": "c:LoginPage.new", "type": "Calls" },
            { "source": "m:main", "target": "m:StatelessWidget.build", "type": "Calls" },
            { "source": "m:main", "target": "t:Gadget", "type": "Uses" },
            { "source": "m:main", "target": "f:title", "type": "Consumes" }
          ],
          "metrics": {
            "m:build": { "code": 26, "comment": 4, "logical": 1, "complexity": 3 },
            "m:main":  { "code": 3,  "comment": 0, "logical": 1, "complexity": 1 },
            "t:Gadget": { "code": 9, "comment": 0, "logical": 2, "complexity": 1 }
          }
        }
        """;
}
