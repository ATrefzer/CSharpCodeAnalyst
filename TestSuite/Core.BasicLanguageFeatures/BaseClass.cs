using System;

namespace Core.BasicLanguageFeatures;

public class BaseClass
{
    public BaseClass()
    {
        // Constructor with body
    }
    
    protected string ProtectedField = "base";

    public virtual string GetMessage()
    {
        return "Base message";
    }

    protected void BaseMethod()
    {
        Console.WriteLine("Base method");
    }

    public void HasLocalFunction()
    {
        void LocalFunction()
        {
            var obj = new CreatableClass();
        }
        LocalFunction();
    }
}