# GetSymbolInfo vs GetDeclaredSymbol in Roslyn

## GetSymbolInfo

- **Use Case**: Use `GetSymbolInfo` when you want to get information about a symbol that is being referenced or used.
- **Typical Scenarios**:
  - Attribute usages
  - Method invocations
  - Type references
  - Variable usages
- **Returns**: `SymbolInfo` struct, which may contain the symbol if successfully bound, or information about why the symbol couldn't be determined.
- **Example**:
  ```csharp
  var invocation = node as InvocationExpressionSyntax;
  var symbolInfo = semanticModel.GetSymbolInfo(invocation);
  var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
  ```

## GetDeclaredSymbol

- **Use Case**: Use `GetDeclaredSymbol` when you want to get the symbol for a declaration in your code.
- **Typical Scenarios**:
  - Class declarations
  - Method declarations
  - Property declarations
  - Variable declarations
- **Returns**: The `ISymbol` directly if the node represents a declaration, or null if it doesn't.
- **Example**:
  ```csharp
  var classDeclaration = node as ClassDeclarationSyntax;
  var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
  ```

## Key Differences

1. **Usage vs Declaration**: 
   - `GetSymbolInfo` is for uses of symbols.
   - `GetDeclaredSymbol` is for declarations of symbols.

2. **Return Type**: 
   - `GetSymbolInfo` returns a `SymbolInfo` struct.
   - `GetDeclaredSymbol` returns an `ISymbol` or null.

3. **Binding Information**: 
   - `GetSymbolInfo` can provide information even if the symbol couldn't be determined (e.g., in case of errors).
   - `GetDeclaredSymbol` only returns a symbol if it's a valid declaration.

## In Your Parser

- Use `GetDeclaredSymbol` in the hierarchy-building phase when processing declarations.
- Use `GetSymbolInfo` in the dependency-analysis phase when examining usages, including attributes.

## Example in Context

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

Remember, always use `GetDeclaredSymbol` for nodes that represent declarations, and `GetSymbolInfo` for nodes that represent usages or references.

