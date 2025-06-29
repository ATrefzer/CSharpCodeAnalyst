# Guide to Roslyn Concepts (Generated via Claude.ai)

This guide covers key Roslyn concepts that are essential for building code analysis tools. It focuses on practical distinctions that often cause confusion when working with the Roslyn APIs.

## Table of Contents
[TOC]

## GetSymbolInfo vs GetDeclaredSymbol

### GetSymbolInfo
- **Use Case**: Get information about a symbol being referenced or used.
- **Typical Scenarios**: Attribute usages, method invocations, type references, variable usages.
- **Returns**: `SymbolInfo` struct, which may contain the symbol if successfully bound, or information about why the symbol couldn't be determined.
- **Example**:
  ```csharp
  var invocation = node as InvocationExpressionSyntax;
  var symbolInfo = semanticModel.GetSymbolInfo(invocation);
  var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
  ```

### GetDeclaredSymbol
- **Use Case**: Get the symbol for a declaration in your code.
- **Typical Scenarios**: Class declarations, method declarations, property declarations, variable declarations.
- **Returns**: The `ISymbol` directly if the node represents a declaration, or null if it doesn't.
- **Example**:
  ```csharp
  var classDeclaration = node as ClassDeclarationSyntax;
  var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
  ```

### Key Differences
1. **Usage vs Declaration**: 
   - `GetSymbolInfo` is for uses of symbols.
   - `GetDeclaredSymbol` is for declarations of symbols.

2. **Return Type**: 
   - `GetSymbolInfo` returns a `SymbolInfo` struct.
   - `GetDeclaredSymbol` returns an `ISymbol` or null.

3. **Binding Information**: 
   - `GetSymbolInfo` can provide information even if the symbol couldn't be determined (e.g., in case of errors).
   - `GetDeclaredSymbol` only returns a symbol if it's a valid declaration.

### In the Context of Parsing
- Use `GetDeclaredSymbol` in the hierarchy-building phase when processing declarations.
- Use `GetSymbolInfo` in the dependency-analysis phase when examining usages, including attributes.

### Example in Context
```csharp
// In hierarchy-building phase (for declarations)
case ClassDeclarationSyntax classDeclaration:
    symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
    elementType = CodeElementType.Class;
    break;

// In dependency-analysis phase (for usages, including attributes)
var attributeSymbol = semanticModel.GetSymbolInfo(attribute).Symbol;
if (attributeSymbol is IMethodSymbol attributeCtorSymbol)
{
    // Process attribute usage
}
```

## SymbolInfo vs ISymbol

### ISymbol (Direct Symbol)
- **What it is**: A direct reference to a semantic entity (class, method, property, etc.)
- **When you get it**: From `GetDeclaredSymbol()` on syntax nodes that **declare** something
- **Guarantees**: Always represents a valid, declared symbol
- **Use case**: When you know the syntax node represents a declaration

### SymbolInfo (Symbol Resolution Result)
- **What it is**: The result of trying to resolve a symbol from an expression
- **When you get it**: From `GetSymbolInfo()` on expressions that **reference** something
- **Guarantees**: May or may not have a resolved symbol
- **Use case**: When you're analyzing expressions and need to handle ambiguity

### Examples from Your Codebase

### ISymbol - Direct Declaration
```csharp
// In HierarchyAnalyzer.cs - Phase 1
case ClassDeclarationSyntax:
    symbol = semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
    // This ALWAYS returns a symbol because ClassDeclarationSyntax declares a class
```

### SymbolInfo - Expression Resolution
```csharp
// In RelationshipAnalyzer.cs - Phase 2
var symbolInfo = semanticModel.GetSymbolInfo(invocationSyntax);
if (symbolInfo.Symbol is IMethodSymbol calledMethod)
{
    // This might be null if the method call is ambiguous or unresolved
    AddCallsRelationship(sourceElement, calledMethod, location);
}
```

### Why This Distinction Matters

#### 1. Ambiguity Handling
```csharp
// Consider this code:
var result = Process(); // Which Process() method?

var symbolInfo = semanticModel.GetSymbolInfo(invocationSyntax);
if (symbolInfo.Symbol != null)
{
    // We found exactly one match
}
else if (symbolInfo.CandidateReason == CandidateReason.Ambiguous)
{
    // Multiple candidates - need to handle ambiguity
    foreach (var candidate in symbolInfo.CandidateSymbols)
    {
        // Handle each possible match
    }
}
```

#### 2. Error Recovery
```csharp
var symbolInfo = semanticModel.GetSymbolInfo(expression);
switch (symbolInfo.CandidateReason)
{
    case CandidateReason.None:
        // Perfect match found
        break;
    case CandidateReason.Ambiguous:
        // Multiple candidates
        break;
    case CandidateReason.OverloadResolutionFailure:
        // Method exists but arguments don't match
        break;
    case CandidateReason.NotReferenced:
        // Symbol exists but not accessible
        break;
}
```

#### 3. Different Analysis Contexts

**Declaration Analysis (Phase 1):**
```csharp
// We KNOW this declares something
var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
// classSymbol is guaranteed to be non-null
```

**Usage Analysis (Phase 2):**
```csharp
// We're trying to figure out what this expression refers to
var symbolInfo = semanticModel.GetSymbolInfo(methodCall);
if (symbolInfo.Symbol is IMethodSymbol method)
{
    // Successfully resolved the method call
    AddCallsRelationship(sourceElement, method, location);
}
// If symbolInfo.Symbol is null, the method call couldn't be resolved
```

### SymbolInfo Properties

```csharp
public class SymbolInfo
{
    public ISymbol? Symbol { get; }           // The best match, or null
    public ImmutableArray<ISymbol> CandidateSymbols { get; }  // All possible matches
    public CandidateReason CandidateReason { get; }  // Why resolution succeeded/failed
}
```

### Real-World Scenarios

#### Scenario 1: Perfect Resolution
```csharp
var x = new MyClass();
var symbolInfo = semanticModel.GetSymbolInfo(x);
// symbolInfo.Symbol = MyClass constructor
// symbolInfo.CandidateReason = CandidateReason.None
```

#### Scenario 2: Ambiguous Method
```csharp
Process(10); // Multiple Process methods with different signatures
var symbolInfo = semanticModel.GetSymbolInfo(invocation);
// symbolInfo.Symbol = null
// symbolInfo.CandidateReason = CandidateReason.Ambiguous
// symbolInfo.CandidateSymbols = [Process(int), Process(object)]
```

#### Scenario 3: Unresolved Reference
```csharp
UndefinedMethod(); // Method doesn't exist
var symbolInfo = semanticModel.GetSymbolInfo(invocation);
// symbolInfo.Symbol = null
// symbolInfo.CandidateReason = CandidateReason.None
// symbolInfo.CandidateSymbols = empty
```

### In Your Codebase Context

Your codebase uses this distinction perfectly:

1. **Phase 1**: Uses `GetDeclaredSymbol()` to find all declared elements (guaranteed to exist)
2. **Phase 2**: Uses `GetSymbolInfo()` to analyze relationships (may fail to resolve)

This allows your analyzer to:
- Build a complete hierarchy of declared elements
- Handle cases where method calls can't be resolved (external APIs, compilation errors, etc.)
- Provide robust analysis even when the code has some semantic issues

The distinction ensures that your code graph building is resilient to real-world code that might have unresolved references or ambiguous calls.

## OriginalDefinition vs ConstructedFrom

### OriginalDefinition
- **Available On**: Various symbol types (INamedTypeSymbol, IMethodSymbol, etc.)
- **Purpose**: Returns the original definition of a symbol, regardless of whether it's generic or not.
- **Behavior**:
  - For non-generic types/methods: Returns the symbol itself.
  - For generic types/methods (open and closed): Returns the unconstructed, open generic form.
- **Examples**:
  - `List<int>` → `List<T>`
  - `List<T>` → `List<T>`
  - `MyClass` (non-generic) → `MyClass`

### ConstructedFrom
- **Available On**: INamedTypeSymbol and IMethodSymbol only
- **Purpose**: Relevant only for constructed generic types or methods (closed generics).
- **Behavior**:
  - For constructed generic types/methods: Returns the open generic form.
  - For non-generic or unconstructed generic types/methods: Returns null.
- **Examples**:
  - `List<int>` → `List<T>`
  - `List<T>` → null
  - `MyClass` (non-generic) → null

### Key Differences
1. **Availability**: 
   - `OriginalDefinition` is more widely available.
   - `ConstructedFrom` is limited to certain symbol types.

2. **Behavior with Non-Generics**:
   - `OriginalDefinition` returns the symbol itself.
   - `ConstructedFrom` returns null.

3. **Behavior with Open Generics**:
   - `OriginalDefinition` returns the symbol itself.
   - `ConstructedFrom` returns null.

4. **Purpose**:
   - `OriginalDefinition` is universal, always giving the "template" form of a symbol.
   - `ConstructedFrom` is specific for navigating from closed to open generic forms.

### In the Context of Parsing
- `OriginalDefinition` is generally more useful for mapping any type back to its original definition in your codebase.
- `ConstructedFrom` is helpful when you need to distinguish between open and closed generics.

For handling generic base classes, `OriginalDefinition` is usually the more appropriate choice, as it works consistently for both generic and non-generic types.

## TypeParameters vs TypeArguments

### TypeParameters
- **Definition**: Represent the generic type parameters declared on a method or type.
- **Nature**: Placeholders for types that will be specified when the method is called or the type is used.
- **Example Declaration**:
  ```csharp
  public T MyMethod<T>(T input) { ... }
  ```
  Here, `T` is a TypeParameter.

### TypeArguments
- **Definition**: Represent the actual types used when a generic method is invoked or a generic type is constructed.
- **Nature**: Concrete types that replace the type parameters at the call site or usage point.
- **Example Usage**:
  ```csharp
  int result = MyMethod<int>(5);
  ```
  Here, `int` is a TypeArgument.

### In the Context of Roslyn's IMethodSymbol
- `TypeParameters` property: 
  - Contains the list of type parameters declared on the method.
  - Always present, regardless of whether you're examining a declaration or invocation.

- `TypeArguments` property:
  - Populated for method invocations or when the generic method is part of a constructed type.
  - Typically empty for method declarations.

### Example


```csharp
// Declaration analysis
public void ProcessList<T>(List<T> items) where T : IComparable<T>
{
    // methodSymbol.TypeParameters[0] = T
    // methodSymbol.TypeArguments is empty (this is the declaration)
}

// Usage analysis
ProcessList<string>(myStringList);
// methodSymbol.TypeParameters[0] = T (still there)
// methodSymbol.TypeArguments[0] = string (concrete type used)
```


### Key Points
- For method declarations:
  - `TypeParameters` will be populated.
  - `TypeArguments` will typically be empty.

- For method invocations:
  - Both `TypeParameters` and `TypeArguments` will be populated.
  - `TypeParameters` contains the original type parameters.
  - `TypeArguments` contains the concrete types used in the invocation.

### Implications for Code Analysis
- When analyzing method declarations, focus on `TypeParameters`.
- When analyzing method invocations, focus on `TypeArguments` to see the concrete types being used.

Understanding these distinctions is crucial for correctly analyzing dependencies and usage of generic methods in code parsing and analysis tasks.

## What is a Compilation Unit?

- In C# and Roslyn, a "compilation unit" is essentially a single source file. In Roslyn, each .cs file is parsed into a SyntaxTree, and the root node of that tree is a CompilationUnitSyntax.

- However, in the context of the Roslyn API, a Compilation object represents the entire process of compiling a set of source files (all the code in a project, including references, etc.) into an assembly.

------

### Why can a symbol appear as different instances in different compilations?

- Symbols in Roslyn (like INamedTypeSymbol, IMethodSymbol, etc.) are not just static representations of code elements. They are bound to a specific Compilation (i.e., a specific project build context).

- If you have multiple projects referencing the same file or code (e.g., shared code, linked files, or multi-targeting), each project’s Compilation will create its own symbol instances for the same code element.

- Even if the code is declared in only one place, each compilation context creates its own symbol graph. So, the same class in two projects will have two different INamedTypeSymbol instances, even though they represent the same source code.

------

### Why do you see duplicates for every symbol, not just partial classes?

- Because you are likely iterating over all projects in the solution, and each project’s compilation produces its own set of symbol instances—even for the same source file.

- If two projects include the same file (via linking, shared projects, or multi-targeting), or if you analyze multiple projects in a solution, you will get multiple symbol instances for the same code element.

------

### Example

Suppose you have:

- ProjectA and ProjectB both include Class1.cs.

- When you call GetCompilationAsync() for each project, you get two Compilation objects.

- Each Compilation will have its own INamedTypeSymbol for Class1, even though it’s the same file.

------

### Summary Table

| Term             | Meaning in Roslyn/C#                                         |
| :--------------- | :----------------------------------------------------------- |
| Compilation Unit | A single source file (.cs file)                              |
| Compilation      | The entire project’s code, references, and metadata, built into an assembly |
| Symbol           | An object representing a code element (class, method, etc.), bound to a specific Compilation |

------

### Why deduplication is needed

- Because you process all projects, and each project’s compilation creates its own symbol instances, you must deduplicate by a stable property (like full name) rather than by symbol instance.

------

Partial classes are a special case where a symbol is declared in multiple files, but the above applies to all symbols when analyzing multiple projects/compilations.

------



## Handling of generic methods

Looking at the selected code on line 604:

  ```csharp

if (methodSymbol.IsGenericMethod && FindCodeElement(methodSymbol) is null)

{

  methodSymbol = methodSymbol.OriginalDefinition;

}
  ```

This code is handling a specific issue with generic method invocations in the relationship analyzer. Let me explain why this change is necessary:

### The Problem with Generic Methods

When you have a generic method like:

  ```csharp

public void Process<T>(T item) { ... }

Process<string>("hello");
  ```

The compiler creates a constructed generic method (Process<string>) from the original definition (Process<T>). These are two different symbols in the Roslyn symbol system:

1. Original Definition: Process<T> - the generic method template

1. Constructed Method: Process<string> - the specific instantiation

### Why the Change is Needed

In the code graph, only the original definition (Process<T>) is stored as a CodeElement during the parsing phase. The constructed methods (Process<string>, Process<int>, etc.) are not stored because:

1. They're created dynamically by the compiler

1. There could be infinite variations (every possible type combination)

1. The relationship analyzer wants to track the actual method definition, not every possible instantiation

### What the Code Does

The condition checks:

1. methodSymbol.IsGenericMethod - Is this a generic method?

1. FindCodeElement(methodSymbol) is null - Can we find this specific method in our code graph?

If both are true, it means we have a constructed generic method that's not in our graph. The solution is to fall back to the original definition:

  ```csharp

methodSymbol = methodSymbol.OriginalDefinition;
  ```

This allows the relationship analyzer to create a "Calls" relationship to the actual method definition (Process<T>) rather than failing to find the constructed method (Process<string>).

## Reduced form

The "reduced form" is a key concept in Roslyn's handling of **extension methods**. 

When you have an extension method like this:

```csharp
public static class StringExtensions
{
    public static int WordCount(this string text) // 'this' makes it an extension method
    {
        return text.Split(' ').Length;
    }
}
```

And you call it like this:

```csharp
string message = "Hello world";
int count = message.WordCount(); // Extension method call
```

Roslyn creates **two different method symbols**:

### 1. Original Extension Method
```csharp
// This is the method as declared
WordCount(this string text) // Has 'this' parameter
```

### 2. Reduced Form
```csharp
// This is how it appears when called
WordCount(string text) // 'this' parameter becomes regular first parameter
```

### Why This Matters

The `ReducedFrom` property points from the **reduced form** back to the **original extension method**:

```csharp
// When you call message.WordCount():
var symbolInfo = semanticModel.GetSymbolInfo(invocationSyntax);
var calledMethod = symbolInfo.Symbol as IMethodSymbol;

// calledMethod is the REDUCED form (WordCount(string text))
// calledMethod.ReducedFrom points to the ORIGINAL extension method (WordCount(this string text))
```

### Example

Looking at your selected code:

```csharp
if (methodSymbol.IsExtensionMethod)
{
    // Handle calls to extension methods
    methodSymbol = methodSymbol.ReducedFrom ?? methodSymbol;
}
```

This code is saying:
1. **If** this is an extension method call
2. **Then** get the original extension method definition (not the reduced form)
3. **If** `ReducedFrom` is null, fall back to the current method

### Why This is Necessary

**Problem**: Extension methods create two different symbols

```csharp
// Original declaration
public static int WordCount(this string text) { ... }

// When called as extension method
message.WordCount() // Creates reduced form: WordCount(string text)

// When called as static method
StringExtensions.WordCount(message) // Uses original form: WordCount(this string text)
```

**Solution**: Always work with the original definition

Your codebase wants to create relationships to the **actual method definition**, not the  compiler-generated reduced form

### Example Walkthrough

```csharp
// Extension method declaration
public static class Extensions
{
    public static void Process(this MyClass obj, int value) { ... }
}

// Usage
var obj = new MyClass();
obj.Process(42); // Extension method call
```

**What Roslyn sees:**

1. **Original Method**: `Process(this MyClass obj, int value)`
2. **Reduced Form**: `Process(MyClass obj, int value)` (created during the call)
3. **Relationship**: `reducedForm.ReducedFrom == originalMethod`

**What your code does:**
```csharp
var symbolInfo = semanticModel.GetSymbolInfo(invocationSyntax);
var methodSymbol = symbolInfo.Symbol as IMethodSymbol; // This is the reduced form

if (methodSymbol.IsExtensionMethod)
{
    methodSymbol = methodSymbol.ReducedFrom ?? methodSymbol; // Get the original
}

// Now methodSymbol points to the actual extension method definition
AddCallsRelationship(sourceElement, methodSymbol, location);
```

## Common Pitfalls

### Null Symbols
- Always check if `GetDeclaredSymbol()` returns null
- `GetSymbolInfo().Symbol` can be null for unresolved references

### Generic Method Confusion
- Remember that `List<int>.Add(item)` and `List<string>.Add(item)` are different method symbols
- Use `OriginalDefinition` to get back to the generic template

### Extension Method Calls
- Extension method calls create reduced forms
- Always use `ReducedFrom ?? methodSymbol` for consistent analysis