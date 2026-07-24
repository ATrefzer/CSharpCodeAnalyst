using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Features.Import;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     Feeds a small, schema-faithful doxygen XML sample through the converter. The sample
///     covers: namespaces, inheritance (incl. an unresolvable external base), nested types,
///     call and use references, signature type references, global-scope elements (artificial
///     "global" namespace), template specialization names with spaces, and memberdefs that are
///     duplicated between namespace and file compounds.
/// </summary>
[TestFixture]
public class DoxygenXmlConverterTests
{

    [OneTimeSetUp]
    public void SetUp()
    {
        _xmlDirectory = Path.Combine(Path.GetTempPath(), "DoxygenXmlConverterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_xmlDirectory);

        WriteXml("index.xml", """
            <doxygenindex version="1.10.0">
              <compound refid="namespaceapp" kind="namespace"><name>app</name></compound>
              <compound refid="classapp_1_1Base" kind="class"><name>app::Base</name></compound>
              <compound refid="classapp_1_1Widget" kind="class"><name>app::Widget</name></compound>
              <compound refid="classapp_1_1Widget_1_1Inner" kind="class"><name>app::Widget::Inner</name></compound>
              <compound refid="classLegacy" kind="class"><name>Legacy</name></compound>
              <compound refid="classTmpl_3_01int_01_4" kind="class"><name>Tmpl&lt; int &gt;</name></compound>
              <compound refid="main_8cpp" kind="file"><name>main.cpp</name></compound>
              <compound refid="dir_123" kind="dir"><name>src</name></compound>
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
                    <location file="src/helper.cpp" line="7" column="1"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Base.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Base" kind="class" language="C++">
                <compoundname>app::Base</compoundname>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="classapp_1_1Base_1arender">
                    <type>void</type>
                    <name>render</name>
                    <location file="src/base.h" line="6" column="3"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Widget.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Widget" kind="class" language="C++">
                <compoundname>app::Widget</compoundname>
                <basecompoundref refid="classapp_1_1Base" prot="public">app::Base</basecompoundref>
                <basecompoundref prot="public">std::enable_shared_from_this&lt; Widget &gt;</basecompoundref>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="classapp_1_1Widget_1adraw">
                    <type>void</type>
                    <name>draw</name>
                    <location file="src/widget.h" line="9" column="3"/>
                    <references refid="namespaceapp_1ahelper">app::helper</references>
                    <references refid="classapp_1_1Widget_1acount">app::Widget::count_</references>
                  </memberdef>
                  <memberdef kind="function" id="classapp_1_1Widget_1aeq">
                    <type>bool</type>
                    <name>operator ==</name>
                    <param>
                      <type>const <ref refid="classapp_1_1Widget" kindref="compound">Widget</ref> &amp;</type>
                      <declname>other</declname>
                    </param>
                    <location file="src/widget.h" line="10" column="3"/>
                  </memberdef>
                </sectiondef>
                <sectiondef kind="private-attrib">
                  <memberdef kind="variable" id="classapp_1_1Widget_1acount">
                    <type>int</type>
                    <name>count_</name>
                    <location file="src/widget.h" line="12" column="7"/>
                  </memberdef>
                  <memberdef kind="variable" id="classapp_1_1Widget_1ainner">
                    <type><ref refid="classapp_1_1Widget_1_1Inner" kindref="compound">Inner</ref></type>
                    <name>inner_</name>
                    <location file="src/widget.h" line="13" column="7"/>
                  </memberdef>
                </sectiondef>
                <location file="src/widget.h" line="7" column="1"/>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Widget_1_1Inner.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Widget_1_1Inner" kind="struct" language="C++">
                <compoundname>app::Widget::Inner</compoundname>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classLegacy.xml", """
            <doxygen>
              <compounddef id="classLegacy" kind="class" language="C++">
                <compoundname>Legacy</compoundname>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classTmpl_3_01int_01_4.xml", """
            <doxygen>
              <compounddef id="classTmpl_3_01int_01_4" kind="class" language="C++">
                <compoundname>Tmpl&lt; int &gt;</compoundname>
              </compounddef>
            </doxygen>
            """);

        WriteXml("main_8cpp.xml", """
            <doxygen>
              <compounddef id="main_8cpp" kind="file" language="C++">
                <compoundname>main.cpp</compoundname>
                <sectiondef kind="func">
                  <memberdef kind="function" id="main_8cpp_1amain">
                    <type>int</type>
                    <name>main</name>
                    <location file="src/main.cpp" line="5" column="1"/>
                    <references refid="classapp_1_1Widget_1adraw">app::Widget::draw</references>
                    <references refid="classstd_1_1vector_1apush">std::vector::push_back</references>
                  </memberdef>
                  <memberdef kind="function" id="namespaceapp_1ahelper">
                    <type>void</type>
                    <name>helper</name>
                    <location file="src/helper.cpp" line="7" column="1"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        _graph = new DoxygenXmlConverter().Convert(_xmlDirectory, "DemoCpp");
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

    private CodeElement ByFullName(string fullName)
    {
        return _graph.Nodes.Values.Single(e => e.FullName == fullName);
    }

    [Test]
    public void BuildsHierarchyWithGlobalNamespace()
    {
        Assert.Multiple(() =>
        {
            // Namespaced elements below the assembly.
            Assert.That(ByFullName("DemoCpp.app").ElementType, Is.EqualTo(CodeElementType.Namespace));
            Assert.That(ByFullName("DemoCpp.app.Widget").Parent?.FullName, Is.EqualTo("DemoCpp.app"));
            Assert.That(ByFullName("DemoCpp.app.Widget.Inner").ElementType, Is.EqualTo(CodeElementType.Struct));
            Assert.That(ByFullName("DemoCpp.app.Widget.Inner").Parent?.FullName, Is.EqualTo("DemoCpp.app.Widget"));

            // Namespace-less elements go into the artificial "global" namespace.
            Assert.That(ByFullName("DemoCpp.global").Name, Is.EqualTo(CodeElement.GlobalNamespaceName));
            Assert.That(ByFullName("DemoCpp.global.Legacy").Parent?.FullName, Is.EqualTo("DemoCpp.global"));
            Assert.That(ByFullName("DemoCpp.global.main").ElementType, Is.EqualTo(CodeElementType.Method));
        });
    }

    [Test]
    public void SanitizesNamesContainingWhitespace()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ByFullName("DemoCpp.global.Tmpl<int>").Name, Is.EqualTo("Tmpl<int>"));
            Assert.That(ByFullName("DemoCpp.app.Widget.operator==").ElementType, Is.EqualTo(CodeElementType.Method));
        });
    }

    [Test]
    public void DeduplicatesMembersRepeatedInFileCompounds()
    {
        // "helper" is listed in the namespace compound and again in main.cpp - it must exist
        // exactly once, below the namespace.
        var helper = _graph.Nodes.Values.Single(e => e.Name == "helper");
        Assert.That(helper.Parent?.FullName, Is.EqualTo("DemoCpp.app"));
    }

    [Test]
    public void CreatesRelationships()
    {
        var byIds = _graph.GetAllRelationships()
            .Select(r => (_graph.Nodes[r.SourceId].FullName, r.Type, _graph.Nodes[r.TargetId].FullName))
            .ToHashSet();

        Assert.That(byIds, Is.EquivalentTo(new[]
        {
            ("DemoCpp.app.Widget", RelationshipType.Inherits, "DemoCpp.app.Base"),
            ("DemoCpp.app.Widget.draw", RelationshipType.Calls, "DemoCpp.app.helper"),
            ("DemoCpp.app.Widget.draw", RelationshipType.Uses, "DemoCpp.app.Widget.count_"),
            ("DemoCpp.app.Widget.operator==", RelationshipType.Uses, "DemoCpp.app.Widget"),
            ("DemoCpp.app.Widget.inner_", RelationshipType.Uses, "DemoCpp.app.Widget.Inner"),
            ("DemoCpp.global.main", RelationshipType.Calls, "DemoCpp.app.Widget.draw")
        }));
    }

    [Test]
    public void CountsSkippedExternalReferences()
    {
        // std::enable_shared_from_this (base without refid) and std::vector::push_back.
        var converter = new DoxygenXmlConverter();
        converter.Convert(_xmlDirectory, "DemoCpp");
        Assert.That(converter.SkippedUnresolvedReferences, Is.EqualTo(2));
    }
}
