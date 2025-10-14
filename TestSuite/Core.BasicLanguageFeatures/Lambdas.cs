using System;

namespace Core.BasicLanguageFeatures;

internal class Lambdas
{
    private void Start()
    {
        var x = () =>
        {
            // Start -> uses -> CreatableClass
            var creatableClass = new CreatableClass();

            // Not extracted
            creatableClass.Nop();
        };


        var y = () =>
        {
            BaseClass? baseClass;
            
        };


        var z = () => Foo(Method);
    }

    private void Foo(Action method)
    {
    }

    private void Method()
    {
    }
}