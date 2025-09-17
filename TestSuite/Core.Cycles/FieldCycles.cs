namespace Core.Cycles;

// Test class-level field dependency cycles
public class ClassA
{
    private ClassB? _fieldB;

    public void UseB()
    {
        _fieldB?.DoSomething();
    }

    public void MethodA()
    {
        // Some logic
    }
}

public class ClassB
{
    private ClassA? _fieldA;

    public void UseA()
    {
        _fieldA?.MethodA();
    }

    public void DoSomething()
    {
        // Some logic
    }
}

// Three-way cycle
public class NodeX
{
    private NodeY? _nodeY;

    public void ProcessX()
    {
        _nodeY?.ProcessY();
    }
}

public class NodeY
{
    private NodeZ? _nodeZ;

    public void ProcessY()
    {
        _nodeZ?.ProcessZ();
    }
}

public class NodeZ
{
    private NodeX? _nodeX;

    public void ProcessZ()
    {
        _nodeX?.ProcessX();
    }
}

// Complex cycle with multiple fields
public class ComplexA
{
    private ComplexB? _fieldB1;
    private ComplexB? _fieldB2;
    private ComplexC? _fieldC;

    public void UseB1()
    {
        _fieldB1?.MethodB();
    }

    public void UseB2()
    {
        _fieldB2?.MethodB();
    }

    public void UseC()
    {
        _fieldC?.MethodC();
    }
}

public class ComplexB
{
    private ComplexA? _fieldA;
    private ComplexC? _fieldC;

    public void UseA()
    {
        _fieldA?.UseC();
    }

    public void UseC()
    {
        _fieldC?.MethodC();
    }

    public void MethodB()
    {
        // Some logic
    }
}

public class ComplexC
{
    private ComplexA? _fieldA;

    public void UseA()
    {
        _fieldA?.UseB1();
    }

    public void MethodC()
    {
        // Some logic
    }
}

// Property-based cycles
public class PropertyCycleA
{
    public PropertyCycleB? RelatedB { get; set; }

    public void ProcessA()
    {
        RelatedB?.ProcessB();
    }
}

public class PropertyCycleB
{
    public PropertyCycleA? RelatedA { get; set; }

    public void ProcessB()
    {
        RelatedA?.ProcessA();
    }
}