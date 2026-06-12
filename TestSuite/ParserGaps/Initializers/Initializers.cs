namespace ParserGaps.Initializers;

// KNOWN GAP: AnalyzePropertyBody only looks at the expression body and the accessor list.
// The property initializer (PropertyDeclarationSyntax.Initializer) is never analyzed,
// so the object creation is invisible. Field initializers in contrast are handled.

public class Engine
{
}

public class CarWithPropertyInitializer
{
    // GAP: no Creates relationship is created for "= new Engine()".
    public Engine Engine { get; } = new Engine();
}

public class CarWithFieldInitializer
{
    // Contrast case: field initializers ARE analyzed (CarWithFieldInitializer -creates-> Engine).
    private readonly Engine _engine = new Engine();
}
