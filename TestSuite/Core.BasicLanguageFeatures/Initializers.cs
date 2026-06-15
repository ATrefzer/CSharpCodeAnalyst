namespace Core.BasicLanguageFeatures.Initializers;

// Property and field initializers: the containing type "creates" the object, the member "uses" it.

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
