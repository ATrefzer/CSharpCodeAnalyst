# Guide to Roslyn Concepts (Generated via Claude.ai)

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
