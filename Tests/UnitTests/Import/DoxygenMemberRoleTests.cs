using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     Doxygen has no flag for a constructor - the XML says "function" for one just as for any other
///     method - so the converter decides it by the naming rule of the language. Covered here: a C++
///     constructor and destructor, the same for a class template (whose compound name carries the
///     template argument list while the constructor's does not), Python's dunder names, a Java enum's
///     constructor, and the cases that must stay <see cref="MemberRole.Normal" />.
/// </summary>
[TestFixture]
public class DoxygenMemberRoleTests
{
    [OneTimeSetUp]
    public void SetUp()
    {
        _xmlDirectory = Path.Combine(Path.GetTempPath(), "DoxygenMemberRoleTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_xmlDirectory);

        WriteXml("index.xml", """
            <doxygenindex version="1.10.0">
              <compound refid="namespaceapp" kind="namespace"><name>app</name></compound>
              <compound refid="classapp_1_1Widget" kind="class"><name>app::Widget</name></compound>
              <compound refid="classapp_1_1Cache" kind="class"><name>app::Cache&lt; T &gt;</name></compound>
              <compound refid="classapp_1_1Loader" kind="class"><name>app::Loader</name></compound>
              <compound refid="classapp_1_1Color" kind="enum"><name>app::Color</name></compound>
            </doxygenindex>
            """);

        WriteXml("namespaceapp.xml", """
            <doxygen>
              <compounddef id="namespaceapp" kind="namespace" language="C++">
                <compoundname>app</compoundname>
                <sectiondef kind="func">
                  <memberdef kind="function" id="free_widget">
                    <type>void</type>
                    <name>Widget</name>
                    <location file="src/free.cpp" line="3" column="1"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Widget.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Widget" kind="class" language="C++">
                <compoundname>app::Widget</compoundname>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="widget_ctor">
                    <type></type>
                    <name>Widget</name>
                    <location file="src/widget.h" line="5" column="3"/>
                  </memberdef>
                  <memberdef kind="function" id="widget_dtor">
                    <type></type>
                    <name>~Widget</name>
                    <location file="src/widget.h" line="6" column="3"/>
                  </memberdef>
                  <memberdef kind="function" id="widget_draw">
                    <type>void</type>
                    <name>draw</name>
                    <location file="src/widget.h" line="7" column="3"/>
                  </memberdef>
                  <memberdef kind="variable" id="widget_count">
                    <type>int</type>
                    <name>count</name>
                    <location file="src/widget.h" line="8" column="3"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Cache.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Cache" kind="class" language="C++">
                <compoundname>app::Cache&lt; T &gt;</compoundname>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="cache_ctor">
                    <type></type>
                    <name>Cache</name>
                    <location file="src/cache.h" line="5" column="3"/>
                  </memberdef>
                  <memberdef kind="function" id="cache_dtor">
                    <type></type>
                    <name>~Cache</name>
                    <location file="src/cache.h" line="6" column="3"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Loader.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Loader" kind="class" language="Python">
                <compoundname>app::Loader</compoundname>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="loader_init">
                    <type>def</type>
                    <name>__init__</name>
                    <location file="app/loader.py" line="4" column="5"/>
                  </memberdef>
                  <memberdef kind="function" id="loader_del">
                    <type>def</type>
                    <name>__del__</name>
                    <location file="app/loader.py" line="7" column="5"/>
                  </memberdef>
                  <memberdef kind="function" id="loader_load">
                    <type>def</type>
                    <name>load</name>
                    <location file="app/loader.py" line="10" column="5"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        WriteXml("classapp_1_1Color.xml", """
            <doxygen>
              <compounddef id="classapp_1_1Color" kind="enum" language="Java">
                <compoundname>app::Color</compoundname>
                <sectiondef kind="public-func">
                  <memberdef kind="function" id="color_ctor">
                    <type></type>
                    <name>Color</name>
                    <location file="app/Color.java" line="4" column="5"/>
                  </memberdef>
                  <memberdef kind="function" id="color_rgb">
                    <type>int</type>
                    <name>rgb</name>
                    <location file="app/Color.java" line="6" column="5"/>
                  </memberdef>
                </sectiondef>
              </compounddef>
            </doxygen>
            """);

        _graph = new DoxygenXmlConverter().Convert(_xmlDirectory, "Demo");
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

    private MemberRole RoleOf(string id)
    {
        return _graph.Nodes[id].MemberRole;
    }

    [Test]
    public void CppConstructorAndDestructor_AreRecognizedByTheirName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RoleOf("widget_ctor"), Is.EqualTo(MemberRole.Constructor));
            Assert.That(RoleOf("widget_dtor"), Is.EqualTo(MemberRole.Finalizer));
        });
    }

    [Test]
    public void ForAClassTemplate_TheTemplateArgumentListIsIgnored()
    {
        // The compound is "Cache< T >" while the constructor memberdef is plain "Cache" - comparing the
        // names as they stand would find neither.
        Assert.Multiple(() =>
        {
            Assert.That(_graph.Nodes["classapp_1_1Cache"].Name, Is.EqualTo("Cache<T>"));
            Assert.That(RoleOf("cache_ctor"), Is.EqualTo(MemberRole.Constructor));
            Assert.That(RoleOf("cache_dtor"), Is.EqualTo(MemberRole.Finalizer));
        });
    }

    [Test]
    public void PythonUsesItsDunderNames()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RoleOf("loader_init"), Is.EqualTo(MemberRole.Constructor));
            Assert.That(RoleOf("loader_del"), Is.EqualTo(MemberRole.Finalizer));
        });
    }

    [Test]
    public void AJavaEnum_MayDeclareAConstructorToo()
    {
        Assert.That(RoleOf("color_ctor"), Is.EqualTo(MemberRole.Constructor));
    }

    [Test]
    public void AnOrdinaryMethod_IsMarkedNormal()
    {
        // Normal rather than Unknown: the converter looked at every method it created, so Unknown stays
        // reserved for producers that do not fill roles at all.
        Assert.Multiple(() =>
        {
            Assert.That(RoleOf("widget_draw"), Is.EqualTo(MemberRole.Normal));
            Assert.That(RoleOf("loader_load"), Is.EqualTo(MemberRole.Normal));
            Assert.That(RoleOf("color_rgb"), Is.EqualTo(MemberRole.Normal));
        });
    }

    [Test]
    public void AFreeFunctionNamedLikeAClass_IsNotItsConstructor()
    {
        // "app::Widget()" is a free function of the namespace, not a member of app::Widget. Only the
        // owning compound decides, never the bare name.
        Assert.That(RoleOf("free_widget"), Is.EqualTo(MemberRole.Normal));
    }

    [Test]
    public void SomethingThatIsNotAMethod_HasNoRole()
    {
        Assert.That(RoleOf("widget_count"), Is.EqualTo(MemberRole.Unknown));
    }
}
