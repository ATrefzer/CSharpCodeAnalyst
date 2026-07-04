using System.Collections.ObjectModel;
using CSharpCodeAnalyst.CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Artifacts from the first phase of the parser.
///     This information is needed to build the relationships in phase 2.
/// </summary>
public class Artifacts(
    ReadOnlyCollection<INamedTypeSymbol> allNamedTypesInSolution,
    ReadOnlyDictionary<string, ISymbol> elementIdToSymbolMap,
    ReadOnlyDictionary<IAssemblySymbol, List<GlobalStatementSyntax>> globalStatementsByAssembly,
    ReadOnlyDictionary<string, CodeElement> symbolKeyToElementMap,
    ReadOnlyDictionary<string, List<INamedTypeSymbol>> interfaceImplementations)
{
    public ReadOnlyCollection<INamedTypeSymbol> AllNamedTypesInSolution { get; } = allNamedTypesInSolution;
    public ReadOnlyDictionary<string, ISymbol> ElementIdToSymbolMap { get; } = elementIdToSymbolMap;
    public ReadOnlyDictionary<IAssemblySymbol, List<GlobalStatementSyntax>> GlobalStatementsByAssembly { get; } = globalStatementsByAssembly;
    public ReadOnlyDictionary<string, CodeElement> SymbolKeyToElementMap { get; } = symbolKeyToElementMap;

    /// <summary>
    ///     Maps an interface's <see cref="SymbolExtensions.Key" /> to all named types that have that interface
    ///     in their <see cref="INamedTypeSymbol.AllInterfaces" /> (directly or via a base type). Precomputed
    ///     once in phase 1 so phase 2 can resolve "who implements this interface" in O(1) instead of scanning
    ///     every type for every interface member.
    /// </summary>
    public ReadOnlyDictionary<string, List<INamedTypeSymbol>> InterfaceImplementations { get; } = interfaceImplementations;
}