using System.Collections.ObjectModel;
using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Artifacts from the first phase of the parser.
///     This information is needed to build the relationships in phase 2.
/// </summary>
public class Artifacts(
    ReadOnlyCollection<INamedTypeSymbol> allNamedTypesInSolution,
    ReadOnlyDictionary<string, ISymbol> elementIdToSymbolMap,
    ReadOnlyDictionary<IAssemblySymbol, List<GlobalStatementSyntax>> globalStatementsByAssembly,
    ReadOnlyDictionary<string, CodeElement> symbolKeyToElementMap)
{
    public ReadOnlyCollection<INamedTypeSymbol> AllNamedTypesInSolution { get; } = allNamedTypesInSolution;
    public ReadOnlyDictionary<string, ISymbol> ElementIdToSymbolMap { get; } = elementIdToSymbolMap;
    public ReadOnlyDictionary<IAssemblySymbol, List<GlobalStatementSyntax>> GlobalStatementsByAssembly { get; } = globalStatementsByAssembly;
    public ReadOnlyDictionary<string, CodeElement> SymbolKeyToElementMap { get; } = symbolKeyToElementMap;
}