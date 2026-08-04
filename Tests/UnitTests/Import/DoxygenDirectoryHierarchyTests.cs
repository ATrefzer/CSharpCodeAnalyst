using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     The same converter in <see cref="DoxygenHierarchyMode.Directories" />: the namespaces come
///     from the directory of each element's source file instead of from the declared C++ namespaces.
///     The sample deliberately declares a namespace "app" that does not match any directory, so
///     every assertion below shows the directory winning.
/// </summary>
[TestFixture]
public class DoxygenDirectoryHierarchyTests
{
    [OneTimeSetUp]
    public void SetUp()
    {
        _xmlDirectory = Path.Combine(Path.GetTempPath(), "DoxygenDirectoryHierarchyTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_xmlDirectory);

        // doxygen reports absolute paths with forward slashes. The directory does not have to
        // exist - the relative path is pure string math.
        WriteXml("index.xml", """
            <doxygenindex version="1.10.0">
              <compound refid="namespaceapp" kind="namespace"><name>app</name></compound>
              <compound refid="classapp_1_1Base" kind="class"><name>app::Base</name></compound>
              <compound refid="classapp_1_1Widget" kind="class"><name>app::Widget</name></compound>
              <compound refid="classapp_1_1Widget_1_1Inner" kind="class"><name>app::Widget::Inner</name></compound>
              <compound refid="classapp_1_1Legacy" kind="class"><name>app::Legacy</name></compound>
              <compound refid="classapp_1_1Vendor" kind="class"><name>app::Vendor</name></compound>
              <compound refid="classapp_1_1Detached" kind="class"><name>app::Detached</name></compound>
              <compound refid="main_8cpp" kind="file"><name>main.cpp</name></compound>
            </doxygenindex>
            """);

        WriteXml("namespaceapp.xml", """
            <doxygen>
              <compounddef id="namespaceapp" kind="namespace" language="C++">
                <compoundname>app</compoundname>
                <sectiondef kind="func">
                  <memberdef kind="function" id="namespaceapp_1ahelper">
                    <type>void</type>
                    <name>helper</name>
                    <location file="C:/src/demo/core/util/helper.cpp" line="7" column="1"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        // Two files in the same directory - the file name is not part of the hierarchy, so both
        // types are siblings.
        WriteXml("classapp_1_1Base.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Base" kind="class" language="C++">
                <compoundname>app::Base</compoundname>
                <location file="C:/src/demo/core/widgets/base.h" line="4" column="1"/>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Widget.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Widget" kind="class" language="C++">
                <compoundname>app::Widget</compoundname>
                <basecompoundref refid="classapp_1_1Base" prot="public">app::Base</basecompoundref>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="classapp_1_1Widget_1adraw">
                    <type>void</type>
                    <name>draw</name>
                    <location file="C:/src/demo/core/widgets/widget.cpp" line="9" column="3"/>
                    <references refid="namespaceapp_1ahelper">app::helper</references>
                  </memberdef>
                </sectiondef>
                <location file="C:/src/demo/core/widgets/widget.h" line="7" column="1"/>
              </compounddef>
            </doxygen>
            """);

        // A nested type belongs to its outer type, not to a folder.
        WriteXml("classapp_1_1Widget_1_1Inner.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Widget_1_1Inner" kind="struct" language="C++">
                <compoundname>app::Widget::Inner</compoundname>
                <location file="C:/src/demo/core/widgets/widget.h" line="12" column="3"/>
              </compounddef>
            </doxygen>
            """);

        // Directly in the imported directory: no path segment left, so "global".
        WriteXml("classapp_1_1Legacy.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Legacy" kind="class" language="C++">
                <compoundname>app::Legacy</compoundname>
                <location file="C:/src/demo/legacy.h" line="3" column="1"/>
              </compounddef>
            </doxygen>
            """);

        // Outside the imported directory (an included header from elsewhere): "global" as well.
        WriteXml("classapp_1_1Vendor.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Vendor" kind="class" language="C++">
                <compoundname>app::Vendor</compoundname>
                <location file="C:/src/vendor/vendor.h" line="3" column="1"/>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Detached.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Detached" kind="class" language="C++">
                <compoundname>app::Detached</compoundname>
              </compounddef>
            </doxygen>
            """);

        // A directory name containing a space: sanitized like every other name, so it survives
        // the whitespace splitting consumers (plain text graph format).
        WriteXml("main_8cpp.xml", """
            <doxygen>
              <compounddef id="main_8cpp" kind="file" language="C++">
                <compoundname>main.cpp</compoundname>
                <sectiondef kind="func">
                  <memberdef kind="function" id="main_8cpp_1amain">
                    <type>int</type>
                    <name>main</name>
                    <location file="C:/src/demo/host app/main.cpp" line="5" column="1"/>
                    <references refid="classapp_1_1Widget_1adraw">app::Widget::draw</references>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        _graph = new DoxygenXmlConverter(DoxygenHierarchyMode.Directories, SourceDirectory).Convert(_xmlDirectory, "DemoCpp");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Directory.Delete(_xmlDirectory, true);
    }

    private const string SourceDirectory = @"C:\src\demo";
    private string _xmlDirectory = null!;
    private CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph _graph = null!;

    private void WriteXml(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_xmlDirectory, fileName), content);
    }

    private CodeElement ByFullName(string fullName)
    {
        return _graph.Nodes.Values.Single(e => e.FullName == fullName);
    }

    [Test]
    public void BuildsNamespacesFromDirectories()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ByFullName("DemoCpp.core").ElementType, Is.EqualTo(CodeElementType.Namespace));
            Assert.That(ByFullName("DemoCpp.core.widgets").ElementType, Is.EqualTo(CodeElementType.Namespace));

            // Both types live in core/widgets, in different files - the file name is not a segment.
            Assert.That(ByFullName("DemoCpp.core.widgets.Widget").Parent?.FullName, Is.EqualTo("DemoCpp.core.widgets"));
            Assert.That(ByFullName("DemoCpp.core.widgets.Base").Parent?.FullName, Is.EqualTo("DemoCpp.core.widgets"));

            // The declared namespace "app" is gone.
            Assert.That(_graph.Nodes.Values.Any(e => e.Name == "app"), Is.False);
        });
    }

    [Test]
    public void KeepsNestedTypesAndMembersBelowTheirType()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ByFullName("DemoCpp.core.widgets.Widget.Inner").ElementType, Is.EqualTo(CodeElementType.Struct));
            Assert.That(ByFullName("DemoCpp.core.widgets.Widget.draw").ElementType, Is.EqualTo(CodeElementType.Method));
        });
    }

    [Test]
    public void PlacesFreeMembersInTheDirectoryOfTheirOwnFile()
    {
        Assert.Multiple(() =>
        {
            // Declared in the namespace compound, defined in core/util.
            Assert.That(ByFullName("DemoCpp.core.util.helper").ElementType, Is.EqualTo(CodeElementType.Method));

            // Only listed in a file compound.
            Assert.That(ByFullName("DemoCpp.hostapp.main").ElementType, Is.EqualTo(CodeElementType.Method));
        });
    }

    [Test]
    public void UsesGlobalNamespaceWhenNoDirectoryApplies()
    {
        Assert.Multiple(() =>
        {
            // Directly in the imported directory.
            Assert.That(ByFullName("DemoCpp.global.Legacy").Parent?.Name, Is.EqualTo(CodeElement.GlobalNamespaceName));

            // Outside the imported directory.
            Assert.That(ByFullName("DemoCpp.global.Vendor").Parent?.Name, Is.EqualTo(CodeElement.GlobalNamespaceName));

            // No location at all.
            Assert.That(ByFullName("DemoCpp.global.Detached").Parent?.Name, Is.EqualTo(CodeElement.GlobalNamespaceName));
        });
    }

    [Test]
    public void KeepsAbsoluteSourceLocations()
    {
        // The hierarchy is relative to the imported directory, the location stays absolute so it
        // can still be opened in an editor.
        var widget = ByFullName("DemoCpp.core.widgets.Widget");
        Assert.That(widget.SourceLocations.Single().File, Is.EqualTo(@"C:\src\demo\core\widgets\widget.h"));
    }

    [Test]
    public void CreatesTheSameRelationshipsAsInNamespaceMode()
    {
        var relationships = _graph.GetAllRelationships()
            .Select(r => (_graph.Nodes[r.SourceId].FullName, r.Type, _graph.Nodes[r.TargetId].FullName))
            .ToHashSet();

        Assert.That(relationships, Is.EquivalentTo(new[]
        {
            ("DemoCpp.core.widgets.Widget", RelationshipType.Inherits, "DemoCpp.core.widgets.Base"),
            ("DemoCpp.core.widgets.Widget.draw", RelationshipType.Calls, "DemoCpp.core.util.helper"),
            ("DemoCpp.hostapp.main", RelationshipType.Calls, "DemoCpp.core.widgets.Widget.draw")
        }));
    }

    [Test]
    public void RequiresASourceDirectoryForTheDirectoryMode()
    {
        Assert.Throws<ArgumentException>(() => _ = new DoxygenXmlConverter(DoxygenHierarchyMode.Directories));
    }
}
