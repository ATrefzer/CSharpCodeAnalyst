using System.Text;
using Contracts.Graph;
using CSharpCodeAnalyst.Import;

namespace CodeParserTests.UnitTests;

[TestFixture]
public static class JdepsImporterTest
{
    [Test]
    public static void Import_jdeps()
    {
        var sampleData = new[]
        {
            "services.article.representation.bo.Representation -> java.lang.Integer java.base",
            "services.article.representation.bo.Representation -> java.lang.Long java.base",
            "services.article.representation.bo.RepresentationArticleImpl -> services.article.bo.ArticleHeadImpl classes",
            "services.article.representation.bo.RepresentationArticleImpl -> java.lang.Boolean java.base",
            "services.article.representation.bo.RepresentationType -> java.lang.Class java.base"
        };

        var importer = new JdepsImporter();
        var codeGraph = importer.ImportFromLines(sampleData);


        var sb = new StringBuilder();
        sb.AppendLine($"Imported {codeGraph.Nodes.Count} code elements");
        sb.AppendLine($"Created {codeGraph.GetAllRelationships().Count()} relationships");

        // Print hierarchy
        var rootElements = codeGraph.Nodes.Values.Where(n => n.Parent == null).OrderBy(n => n.Name);
        foreach (var root in rootElements)
        {
            PrintElement(sb, root, 0);
        }

        // Print relationships
        sb.AppendLine("Relationships:");
        foreach (var rel in codeGraph.GetAllRelationships())
        {
            var from = codeGraph.Nodes[rel.SourceId];
            var to = codeGraph.Nodes[rel.TargetId];
            sb.AppendLine($"  {from.FullName} -> {to.FullName} ({rel.Type})");
        }

        var reference = """
                        Imported 15 code elements
                        Created 5 relationships
                        Namespace: java
                          Namespace: lang
                            Class: Boolean
                            Class: Class
                            Class: Integer
                            Class: Long
                        Namespace: services
                          Namespace: article
                            Namespace: bo
                              Class: ArticleHeadImpl
                            Namespace: representation
                              Namespace: bo
                                Class: Representation
                                Class: RepresentationArticleImpl
                                Class: RepresentationType
                        Relationships:
                          services.article.representation.bo.Representation -> java.lang.Integer (Uses)
                          services.article.representation.bo.Representation -> java.lang.Long (Uses)
                          services.article.representation.bo.RepresentationArticleImpl -> services.article.bo.ArticleHeadImpl (Uses)
                          services.article.representation.bo.RepresentationArticleImpl -> java.lang.Boolean (Uses)
                          services.article.representation.bo.RepresentationType -> java.lang.Class (Uses)
                        """.Trim();

        var actual = sb.ToString().Trim();
        Assert.AreEqual(reference, actual);
    }

    private static void PrintElement(StringBuilder sb, CodeElement element, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        sb.AppendLine($"{indentStr}{element.ElementType}: {element.Name}");

        foreach (var child in element.Children.OrderBy(c => c.Name))
        {
            PrintElement(sb, child, indent + 1);
        }
    }
}