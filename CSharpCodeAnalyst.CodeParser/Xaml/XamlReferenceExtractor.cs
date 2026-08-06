using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CSharpCodeAnalyst.CodeParser.Xaml;

/// <summary>
///     A single reference to CLR code found in a XAML file. <see cref="MemberName" /> is null when the
///     whole type is referenced (an element tag, <c>{x:Type}</c>), and set for <c>{x:Static}</c>.
///     <see cref="AssemblyName" /> comes from the <c>;assembly=</c> part of the xmlns and is null when the
///     xmlns omits it - which means the type lives in the same assembly as the XAML file.
/// </summary>
public sealed record XamlReference(
    string NamespaceName,
    string TypeName,
    string? MemberName,
    string? AssemblyName,
    int Line,
    int Column)
{
    /// <summary>
    ///     True for an object element (<c>&lt;local:MyControl/&gt;</c>) - XAML creates an instance there,
    ///     so the constructor runs. False for everything that only names a type: property element syntax
    ///     (<c>&lt;local:MyControl.Items&gt;</c>), an attached property and <c>{x:Type}</c>.
    /// </summary>
    public bool IsInstantiation { get; init; }

    public string TypeFullName => $"{NamespaceName}.{TypeName}";
}

/// <summary>
///     Everything one XAML file contributes: the code-behind class it belongs to (from <c>x:Class</c>, null
///     for a resource dictionary) and the CLR references it makes.
/// </summary>
public sealed class XamlFileReferences
{
    public string? CodeBehindClass { get; init; }
    public IReadOnlyList<XamlReference> References { get; init; } = [];
}

/// <summary>
///     Reads the CLR references out of a XAML file - the ones the markup compiler does *not* turn into C#.
///     <para>
///         The WPF markup compiler generates a partial class per XAML file that contains the event handler
///         wiring and a field per <c>x:Name</c>, so those references are already visible to Roslyn. What
///         never reaches C# is everything declarative: it is compiled into BAML and resolved by reflection
///         at runtime. Three of those constructs carry a fully qualified CLR name and can therefore be
///         resolved exactly, which is what this extractor collects:
///     </para>
///     <list type="bullet">
///         <item>element tags - <c>&lt;local:MyControl/&gt;</c>, including property element syntax</item>
///         <item><c>{x:Static local:Texts.Caption}</c></item>
///         <item><c>{x:Type local:Foo}</c></item>
///     </list>
///     <para>
///         <c>{Binding Path}</c> is deliberately NOT collected. Without evaluating the DataContext it is a
///         bare member name, and matching that by name across the whole codebase would suppress far more
///         than it explains.
///     </para>
///     <para>
///         Prefixes are resolved through the XML namespace declarations, so a <c>clr-namespace</c> xmlns is
///         mapped exactly - there is no name guessing anywhere in here.
///     </para>
/// </summary>
public static class XamlReferenceExtractor
{
    private const string ClrNamespacePrefix = "clr-namespace:";
    
    /// <summary>
    /// Typically named x:
    /// </summary>
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    ///     Matches "{prefix:Static target:Type.Member}" and "{prefix:Type target:Type}", also when nested
    ///     inside another markup extension. Both prefixes are resolved against the element, never assumed.
    /// </summary>
    private static readonly Regex MarkupExtension = new(
        @"\{\s*(?<xamlPrefix>\w+)\s*:\s*(?<kind>Static|Type)\s+(?<prefix>\w+)\s*:\s*(?<path>[\w.]+)",
        RegexOptions.Compiled);

    public static XamlFileReferences Extract(string xaml)
    {
        ArgumentNullException.ThrowIfNull(xaml);

        XDocument document;
        try
        {
            document = XDocument.Parse(xaml, LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            // A malformed or unsupported file contributes nothing. It must never break the parse run.
            return new XamlFileReferences();
        }

        var references = new List<XamlReference>();

        foreach (var element in document.Descendants())
        {
            CollectElementTag(element, references);

            foreach (var attribute in element.Attributes())
            {
                CollectAttachedProperty(attribute, references);
                CollectMarkupExtensions(element, attribute, references);
            }
        }

        return new XamlFileReferences
        {
            CodeBehindClass = document.Root?.Attribute(XName.Get("Class", XamlNamespace))?.Value,
            References = references
        };
    }

    /// <summary>
    ///     The element tag itself: &lt;local:MyControl/&gt;. Property element syntax puts the property
    ///     behind a dot (&lt;local:MyControl.Items&gt;), so only the part in front of it is the type.
    /// </summary>
    private static void CollectElementTag(XElement element, List<XamlReference> references)
    {
        // Only a tag without a dot creates an object; with one it is property element syntax.
        // This is not necessary redundant: <Grid><local:GridHelper.Columns>...</local:GridHelper.Columns></Grid>
        var isInstantiation = !element.Name.LocalName.Contains('.');
        Add(element.Name.NamespaceName, element.Name.LocalName, element, references,
            isInstantiation: isInstantiation);
    }

    /// <summary>An attached property written as local:MyPanel.Dock="..." references MyPanel.</summary>
    private static void CollectAttachedProperty(XAttribute attribute, List<XamlReference> references)
    {
        Add(attribute.Name.NamespaceName, attribute.Name.LocalName, attribute, references);
    }

    private static void CollectMarkupExtensions(XElement element, XAttribute attribute,
        List<XamlReference> references)
    {
        foreach (Match match in MarkupExtension.Matches(attribute.Value))
        {
            // "x" is only a convention - verify the prefix really maps to the XAML language namespace.
            var xamlNamespace = element.GetNamespaceOfPrefix(match.Groups["xamlPrefix"].Value);
            if (xamlNamespace?.NamespaceName != XamlNamespace)
            {
                continue;
            }

            var targetNamespace = element.GetNamespaceOfPrefix(match.Groups["prefix"].Value);
            if (targetNamespace is null)
            {
                continue;
            }

            var path = match.Groups["path"].Value;
            if (match.Groups["kind"].Value == "Type")
            {
                Add(targetNamespace.NamespaceName, path, attribute, references);
                continue;
            }

            // {x:Static Type.Member} - the last segment is the member.
            var separator = path.LastIndexOf('.');
            if (separator <= 0 || separator == path.Length - 1)
            {
                continue;
            }

            Add(targetNamespace.NamespaceName, path[..separator], attribute, references,
                path[(separator + 1)..]);
        }
    }

    private static void Add(string namespaceName, string localName, IXmlLineInfo position,
        List<XamlReference> references, string? memberName = null, bool isInstantiation = false)
    {
        if (!namespaceName.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal))
        {
            // A framework namespace (presentation, xaml, ...) - nothing of ours is referenced.
            return;
        }

        // "clr-namespace:Some.Namespace;assembly=Some.Assembly" - the assembly part is optional and
        // absent exactly when the type lives in the same assembly as the XAML file.
        var declaration = namespaceName[ClrNamespacePrefix.Length..];
        var semicolon = declaration.IndexOf(';');
        var clrNamespace = semicolon < 0 ? declaration : declaration[..semicolon];

        string? assemblyName = null;
        if (semicolon >= 0)
        {
            const string assemblyKey = "assembly=";
            var assemblyPart = declaration[(semicolon + 1)..].Trim();
            if (assemblyPart.StartsWith(assemblyKey, StringComparison.Ordinal))
            {
                assemblyName = assemblyPart[assemblyKey.Length..].Trim();
            }
        }

        // Property element syntax: <local:MyControl.Items> names the type in front of the dot.
        var dot = localName.IndexOf('.');
        var typeName = dot < 0 ? localName : localName[..dot];

        if (clrNamespace.Length == 0 || typeName.Length == 0)
        {
            return;
        }

        references.Add(new XamlReference(clrNamespace, typeName, memberName, assemblyName,
            position.LineNumber, position.LinePosition) { IsInstantiation = isInstantiation });
    }
}
