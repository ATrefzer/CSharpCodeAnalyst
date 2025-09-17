namespace Core.BasicLanguageFeatures;

public class DerivedClass : BaseClass
{
    public override string GetMessage()
    {
        // Base call
        var baseMessage = base.GetMessage();
        return $"Derived: {baseMessage}";
    }

    public void TestBaseAccess()
    {
        // Access protected members
        ProtectedField = "derived";
        BaseMethod();
    }
}