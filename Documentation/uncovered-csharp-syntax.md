# Uncovered C# Syntax

According to GPT, we say a type depends on another when there's a relationship between them that affects compilation 
or runtime behavior. Here are the main scenarios where type dependencies occur:

1. Inheritance: When a class inherits from another class or implements an interface.

2. Composition: When a type contains a field or property of another type.

3. Method parameters: When a type uses another type as a parameter in its methods.

4. Return types: When a method returns an instance of another type.

5. Generic type arguments: When a type is used as a generic type argument.

6. Local variables: When a type is used as a local variable within methods of another type.

7. Static member access: When a type uses static members of another type.

8. Attribute usage: When a type is decorated with attributes of another type.

9. Events: When a type defines events using delegate types.

10. Exception handling: When a type uses another type in catch blocks or throws exceptions of that type.

These dependencies can be direct or indirect. Direct dependencies occur when a type explicitly references another type. Indirect dependencies happen when a type depends on another type through intermediary types.

**These are the basic dependencies this application should focus on.**

**I think it is a pointless task to cover all dependencies possible in a language like C#. Some of them would cause large effort for very little benefit.**

## Know limitations

- Attributes are not captured at method parameter level.

## Known uncovered code constructs

Following is a list of uncovered language constructs. However, there are many more.

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
// Dependencies involving local functions are displayed wrong. It creates a call dependency to the containing claSS:
// Innter functions should be removed completely.
```

- Generic Type Constraints:

```csharp
public class MyClass<T> where T : IComparable<T>
{
    // The dependency on IComparable<T> might be missed
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
