using System;
using System.Collections.Generic;

namespace Regression.SpecificBugs.EventDeRegistrationInLambda;


public class Source
{
    event EventHandler<ExtendedType> MyEvent;
}

public class EventDeRegistrationInLambda
{
    void Do()
    {
        
        
        
    }
}


static class Extensions
{
    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var item in enumerable)
        {
            action(item);
        }
    }
}
