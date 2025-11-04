using System;
using System.Collections.Generic;

namespace Regression.SpecificBugs.EventDeRegistrationInLambda;

public class Source
{
    public event EventHandler MyEvent;
}

public class EventDeRegistrationInLambda
{
    private void Do()
    {
        var source = new Source();
        source.MyEvent += MyHandler;


        List<Source> sources = [source];
        sources.LoopOver(x => x.MyEvent -= MyHandler);
    }

    private void MyHandler(object? sender, EventArgs e)
    {
    }
}

internal static class Extensions
{
    public static void LoopOver<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var item in enumerable)
        {
            action(item);
        }
    }
}