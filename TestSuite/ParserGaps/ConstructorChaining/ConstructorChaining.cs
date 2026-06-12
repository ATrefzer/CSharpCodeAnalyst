namespace ParserGaps.ConstructorChaining;

// KNOWN GAP: ConstructorInitializerSyntax (": base(...)" / ": this(...)") is not an
// InvocationExpressionSyntax and is not handled anywhere. The arguments are still visited,
// but the call to the chained constructor itself is missing.

public class BaseService
{
    public BaseService(int level)
    {
        Level = level;
    }

    public int Level { get; }
}

public class DerivedService : BaseService
{
    // GAP: no Calls relationship DerivedService..ctor -> BaseService..ctor.
    public DerivedService() : base(42)
    {
    }
}

public class SelfChaining
{
    private readonly int _value;

    // GAP: no Calls relationship SelfChaining..ctor -> SelfChaining..ctor (overload).
    public SelfChaining() : this(1)
    {
    }

    public SelfChaining(int value)
    {
        _value = value;
    }
}
