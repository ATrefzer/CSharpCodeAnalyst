# Uncovered C# Syntax

I think its an pointless task to cover all dependencies possible in a language like C#. Some of them would cause large effort for very little  benefit. Instead I focus on the most important code elements.

Following is a list of potential uncovered language constructs. I'm sure there are many more. I did not test yet.

- Generic Type Constraints:

```csharp
public class MyClass<T> where T : IComparable<T>
{
    // The dependency on IComparable<T> might be missed
}
```

- Attribute Usage:

```csharp
[Serializable]
public class MySerializableClass
{
    // The dependency on the Serializable attribute might be missed
}
```

- Extension Methods:

```csharp
public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string str)
    {
        // The dependency of this extension method on string might be missed
    }
}
```


- LINQ Query Expressions:

```csharp
var query = from item in myList
            where item.Property > 0
            select item;
// Dependencies introduced by the LINQ query might be missed
```

- Implicit Conversions:

```csharp
public class MyClass
{
    public static implicit operator int(MyClass myClass)
    {
        return 0;
    }
    // This implicit conversion to int might be missed
}
```

- Tuple Deconstruction:

```csharp
public (int, string) GetValues()
{
    return (1, "test");
}

var (number, text) = GetValues();
// The dependency on the tuple return type might be missed
```

- Default Interface Methods (C# 8.0+):

```csharp
public interface IMyInterface
{
    void MyMethod()
    {
        // Default implementation
    }
}
// Dependencies within default interface methods might be missed
```

- Using Declarations (C# 8.0+):

```csharp
using var file = new StreamReader("myfile.txt");
// The dependency on StreamReader and its disposal might be missed
```

- Reflection:

```csharp
var type = Type.GetType("MyNamespace.MyClass");
var instance = Activator.CreateInstance(type);
// Dependencies created through reflection might be missed
```

- Dynamic objects:

```csharp
dynamic dynamicObject = GetSomeObject();
dynamicObject.SomeMethod();
// Dependencies on methods called on dynamic objects might be missed
```

- Unsafe code and pointers:

```csharp
unsafe
{
    int* ptr = stackalloc int[10];
    // Dependencies in unsafe code blocks might be missed
}
```

- Conditional compilation:

```csharp
#if DEBUG
Debug.WriteLine("Debug mode");
#endif
// Dependencies in conditional compilation blocks might be missed
```

- Partial classes and methods:

```csharp
public partial class MyClass
{
    partial void MyPartialMethod();
}

public partial class MyClass
{
    partial void MyPartialMethod()
    {
        // Implementation
    }
}
// Dependencies across partial class implementations might be missed
```

- Local functions:

```csharp
public void OuterMethod()
{
    void LocalFunction()
    {
        // Some code
    }
    LocalFunction();
}
// Dependencies involving local functions might be missed
```

- Expression-bodied members:

```csharp
public class Person
{
    public string Name { get; set; }
    public string Greeting => $"Hello, {Name}!";
}
// Dependencies in expression-bodied members might be missed
```

- Pattern matching:

```csharp
if (obj is Person { Name: var name, Age: > 18 })
{
    // Use name
}
// Dependencies introduced by pattern matching might be missed
```

- Indexers:

```csharp
public class MyCollection
{
    public object this[string key] => GetValueForKey(key);
}
// Dependencies in indexers might be missed
```

- Async/await pattern:

```csharp
public async Task<int> GetValueAsync()
{
    var result = await SomeAsyncMethod();
    return result;
}
// Dependencies introduced by async/await might be missed
```

- Finalizers:

```csharp
public class MyClass
{
    ~MyClass()
    {
        // Cleanup code
    }
}
// Dependencies in finalizers might be missed
```

- Operator overloading:

```csharp
public static MyClass operator +(MyClass a, MyClass b)
{
    // Implementation
}
// Dependencies introduced by operator overloading might be missed
```

- Caller Information Attributes:

```csharp
public void LogMessage(string message,
    [CallerMemberName] string memberName = "",
    [CallerFilePath] string sourceFilePath = "",
    [CallerLineNumber] int sourceLineNumber = 0)
{
    // Dependencies on the calling context might be missed
}
```

- Interop with native code:

```csharp
[DllImport("user32.dll")]
public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
// Dependencies on external DLLs might be missed
```

- XML Documentation Comments:

```csharp
/// <see cref="OtherClass"/>
public class MyClass { }
// Dependencies mentioned in documentation might be missed
```

- Query expressions with let clauses:

```csharp
var query = from n in numbers
            let square = n * n
            where square > 100
            select new { Number = n, Square = square };
// Dependencies introduced in 'let' clauses might be missed
```

- Span<T> and Memory<T>:

```csharp
Span<int> numbers = stackalloc int[100];
// Dependencies related to memory management might be missed
```

- Switch expressions (C# 8.0+):

```csharp
string GetNthDay(int n) => n switch
{
    1 => "Monday",
    2 => "Tuesday",
    _ => "Invalid day"
};
// Dependencies in switch expressions might be missed
```

- Init-only setters (C# 9.0+):

```csharp
public class Person
{
    public string Name { get; init; }
}
// Dependencies related to immutability might be missed
```

- Top-level statements (C# 9.0+):

```csharp
// Program.cs
using System;
Console.WriteLine("Hello World!");
// Dependencies in top-level statements might be missed
```

- Function pointers (C# 9.0+):

```csharp
unsafe delegate*<int, int> functionPointer = &SomeMethod;
// Dependencies through function pointers might be missed
```

# Implemented

- Delegates and Event Handlers:

```c#
public delegate void MyEventHandler(object sender, EventArgs e);

public class MyClass
{
    public event MyEventHandler MyEvent;
    // The dependency on MyEventHandler might be missed
}
```

- Record types (C# 9.0+):

```csharp
public record Person(string FirstName, string LastName);
// Dependencies introduced by the compiler-generated members of records might be missed
```



