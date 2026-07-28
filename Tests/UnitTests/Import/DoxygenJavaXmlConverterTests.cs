using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     The Java counterpart of <see cref="DoxygenXmlConverterTests" />. The XML below is a trimmed
///     copy of what doxygen 1.17 really emits for a small Java project, so it pins the two things
///     Java does differently from C++:
///     - an enum is a compound of its own (kind="enum") with members, not a memberdef,
///     - doxygen invents namespace compounds for packages it only saw in a reference
///     ("java::util"), which must not end up in the graph.
///     Packages themselves need no special handling: doxygen reports them as "::" separated
///     namespace compounds, exactly like C++ namespaces.
/// </summary>
[TestFixture]
public class DoxygenJavaXmlConverterTests
{
    [OneTimeSetUp]
    public void SetUp()
    {
        _xmlDirectory = Path.Combine(Path.GetTempPath(), "DoxygenJavaXmlConverterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_xmlDirectory);

        WriteXml("index.xml", """
            <doxygenindex version="1.17.0">
              <compound refid="namespacecom" kind="namespace"><name>com</name></compound>
              <compound refid="namespacecom_1_1example" kind="namespace"><name>com::example</name></compound>
              <compound refid="namespacecom_1_1example_1_1core" kind="namespace"><name>com::example::core</name></compound>
              <compound refid="namespacejava" kind="namespace"><name>java</name></compound>
              <compound refid="namespacejava_1_1util" kind="namespace"><name>java::util</name></compound>
              <compound refid="classcom_1_1example_1_1core_1_1_circle" kind="class"><name>com::example::core::Circle</name></compound>
              <compound refid="interfacecom_1_1example_1_1core_1_1_shape" kind="interface"><name>com::example::core::Shape</name></compound>
              <compound refid="enumcom_1_1example_1_1core_1_1_kind" kind="enum"><name>com::example::core::Kind</name></compound>
              <compound refid="_circle_8java" kind="file"><name>Circle.java</name></compound>
              <compound refid="dir_123" kind="dir"><name>src</name></compound>
            </doxygenindex>
            """);

        // Doxygen writes a compound for every package level, all of them without members.
        WriteNamespace("namespacecom", "com");
        WriteNamespace("namespacecom_1_1example", "com::example");
        WriteNamespace("namespacecom_1_1example_1_1core", "com::example::core");
        WriteNamespace("namespacejava", "java");
        WriteNamespace("namespacejava_1_1util", "java::util");

        WriteXml("interfacecom_1_1example_1_1core_1_1_shape.xml", """
            <doxygen>
              <compounddef id="interfacecom_1_1example_1_1core_1_1_shape" kind="interface" language="Java" prot="public">
                <compoundname>com::example::core::Shape</compoundname>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="interfacecom_1_1example_1_1core_1_1_shape_1aarea">
                    <type>double</type>
                    <name>area</name>
                    <location file="src/com/example/core/Shape.java" line="4" column="12"/>
                  </memberdef>
                </sectiondef>
                <location file="src/com/example/core/Shape.java" line="3" column="18"/>
              </compounddef>
            </doxygen>
            """);

        // A Java enum: a type with constants (variables) and behaviour (functions).
        WriteXml("enumcom_1_1example_1_1core_1_1_kind.xml", """
            <doxygen>
              <compounddef id="enumcom_1_1example_1_1core_1_1_kind" kind="enum" language="Java" prot="public">
                <compoundname>com::example::core::Kind</compoundname>
                <sectiondef kind="public-attrib">
                  <memberdef kind="variable" id="enumcom_1_1example_1_1core_1_1_kind_1aROUND">
                    <type></type>
                    <name>ROUND</name>
                    <location file="src/com/example/core/Kind.java" line="4" column="1"/>
                  </memberdef>
                </sectiondef>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="enumcom_1_1example_1_1core_1_1_kind_1aisRound">
                    <type>boolean</type>
                    <name>isRound</name>
                    <location file="src/com/example/core/Kind.java" line="7" column="20"/>
                    <references refid="enumcom_1_1example_1_1core_1_1_kind_1aROUND">com.example.core.Kind.ROUND</references>
                  </memberdef>
                </sectiondef>
                <location file="src/com/example/core/Kind.java" line="3" column="7"/>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classcom_1_1example_1_1core_1_1_circle.xml", """
            <doxygen>
              <compounddef id="classcom_1_1example_1_1core_1_1_circle" kind="class" language="Java" prot="public">
                <compoundname>com::example::core::Circle</compoundname>
                <basecompoundref refid="interfacecom_1_1example_1_1core_1_1_shape" prot="public">com.example.core.Shape</basecompoundref>
                <basecompoundref prot="public">java.util.Observable</basecompoundref>
                <sectiondef kind="private-attrib">
                  <memberdef kind="variable" id="classcom_1_1example_1_1core_1_1_circle_1akind">
                    <type><ref refid="enumcom_1_1example_1_1core_1_1_kind" kindref="compound">Kind</ref></type>
                    <name>kind</name>
                    <location file="src/com/example/core/Circle.java" line="10" column="18"/>
                  </memberdef>
                </sectiondef>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="classcom_1_1example_1_1core_1_1_circle_1aarea">
                    <type>double</type>
                    <name>area</name>
                    <reimplements refid="interfacecom_1_1example_1_1core_1_1_shape_1aarea">area</reimplements>
                    <location file="src/com/example/core/Circle.java" line="18" column="19"/>
                    <references refid="enumcom_1_1example_1_1core_1_1_kind_1aisRound">com.example.core.Kind.isRound</references>
                  </memberdef>
                </sectiondef>
                <location file="src/com/example/core/Circle.java" line="7" column="7"/>
              </compounddef>
            </doxygen>
            """);

        // Every file compound exists, but none of them has global-scope members - Java has none.
        WriteXml("_circle_8java.xml", """
            <doxygen>
              <compounddef id="_circle_8java" kind="file" language="Java">
                <compoundname>Circle.java</compoundname>
                <innerclass refid="classcom_1_1example_1_1core_1_1_circle" prot="public">com::example::core::Circle</innerclass>
                <innernamespace refid="namespacecom_1_1example_1_1core">com::example::core</innernamespace>
                <location file="src/com/example/core/Circle.java"/>
              </compounddef>
            </doxygen>
            """);

        _graph = new DoxygenXmlConverter().Convert(_xmlDirectory, "DemoJava");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Directory.Delete(_xmlDirectory, true);
    }

    private string _xmlDirectory = null!;
    private CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph _graph = null!;

    private void WriteXml(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_xmlDirectory, fileName), content);
    }

    private void WriteNamespace(string refId, string qualifiedName)
    {
        WriteXml(refId + ".xml", $"""
            <doxygen>
              <compounddef id="{refId}" kind="namespace" language="Java">
                <compoundname>{qualifiedName}</compoundname>
              </compounddef>
            </doxygen>
            """);
    }

    private CodeElement ByFullName(string fullName)
    {
        return _graph.Nodes.Values.Single(e => e.FullName == fullName);
    }

    [Test]
    public void MapsPackagesToNestedNamespaces()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ByFullName("DemoJava.com").ElementType, Is.EqualTo(CodeElementType.Namespace));
            Assert.That(ByFullName("DemoJava.com.example.core").Parent?.FullName, Is.EqualTo("DemoJava.com.example"));
            Assert.That(ByFullName("DemoJava.com.example.core.Circle").ElementType, Is.EqualTo(CodeElementType.Class));
            Assert.That(ByFullName("DemoJava.com.example.core.Shape").ElementType, Is.EqualTo(CodeElementType.Interface));
        });
    }

    [Test]
    public void MapsEnumCompoundToATypeWithItsMembers()
    {
        var kind = ByFullName("DemoJava.com.example.core.Kind");

        Assert.Multiple(() =>
        {
            Assert.That(kind.ElementType, Is.EqualTo(CodeElementType.Enum));
            Assert.That(kind.Parent?.FullName, Is.EqualTo("DemoJava.com.example.core"));
            Assert.That(ByFullName("DemoJava.com.example.core.Kind.ROUND").ElementType, Is.EqualTo(CodeElementType.Field));
            Assert.That(ByFullName("DemoJava.com.example.core.Kind.isRound").ElementType, Is.EqualTo(CodeElementType.Method));
        });
    }

    [Test]
    public void DropsPackagesWithoutContent()
    {
        // "java" / "java::util" exist only because a base class referred to them, and no file
        // contributed a global-scope member, so the artificial "global" namespace is gone too.
        var namespaces = _graph.Nodes.Values
            .Where(e => e.ElementType == CodeElementType.Namespace)
            .Select(e => e.FullName);

        Assert.That(namespaces, Is.EquivalentTo(new[]
        {
            "DemoJava.com",
            "DemoJava.com.example",
            "DemoJava.com.example.core"
        }));
    }

    [Test]
    public void CreatesRelationships()
    {
        var relationships = _graph.GetAllRelationships()
            .Select(r => (_graph.Nodes[r.SourceId].FullName, r.Type, _graph.Nodes[r.TargetId].FullName))
            .ToHashSet();

        Assert.That(relationships, Is.EquivalentTo(new[]
        {
            // "implements" and "extends" are the same XML element; the target decides the type.
            ("DemoJava.com.example.core.Circle", RelationshipType.Implements, "DemoJava.com.example.core.Shape"),

            // Field of an enum type.
            ("DemoJava.com.example.core.Circle.kind", RelationshipType.Uses, "DemoJava.com.example.core.Kind"),

            // Calls inside method bodies, including within the enum itself.
            ("DemoJava.com.example.core.Circle.area", RelationshipType.Calls, "DemoJava.com.example.core.Kind.isRound"),
            ("DemoJava.com.example.core.Kind.isRound", RelationshipType.Uses, "DemoJava.com.example.core.Kind.ROUND")
        }));
    }
}
