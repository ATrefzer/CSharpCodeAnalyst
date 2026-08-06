using System.Text.RegularExpressions;

namespace SourceGenerated;

/// <summary>
///     Fixture for the source-generator path of the parser. [GeneratedRegex] ships with the .NET SDK, so
///     this needs no package reference and no restore - the generator runs during the design-time build
///     MSBuildWorkspace performs.
///     <para>
///         The point of the fixture is the partial shape: this class and NumberPattern() are declared
///         here and completed by the generator, so each of them ends up with one hand-written and one
///         generated declaration. Everything the generator adds beside them (its Regex subclass and its
///         helpers) exists only in the generated file.
///     </para>
/// </summary>
public partial class Validator
{
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumberPattern();

    public bool IsNumber(string text)
    {
        return NumberPattern().IsMatch(text);
    }
}
