// ============================================================================
// CODE ANALYZER - Roslyn-Based Syntax Analysis
// ============================================================================
// Uses Microsoft.CodeAnalysis.CSharp to extract method calls and dependencies
// ============================================================================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PipelineDocsCli;

class Analyzer
{
    private readonly int _maxCalls;
    
    public Analyzer(int maxCalls = 10)
    {
        _maxCalls = maxCalls;
    }
    
    public CodeAnalysis Analyze(string source, string filePath)
    {
        var analysis = new CodeAnalysis();
        
        try
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();
            
            // Extract method/constructor calls
            ExtractCalls(root, analysis);
            
            // Extract dependencies from usings and type references
            ExtractDependencies(root, analysis);
            
            // Detect audio and effect integration
            DetectIntegrations(source, analysis);
        }
        catch
        {
            // Silently fail analysis - we'll use heuristics instead
        }
        
        return analysis;
    }
    
    private void ExtractCalls(CompilationUnitSyntax root, CodeAnalysis analysis)
    {
        var calls = new Dictionary<string, int>();
        
        // Find method invocations
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string? methodName = null;
            
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                methodName = memberAccess.Name.Identifier.ValueText;
            }
            else if (invocation.Expression is IdentifierNameSyntax identifier)
            {
                methodName = identifier.Identifier.ValueText;
            }
            
            if (!string.IsNullOrEmpty(methodName) && !IsCommonMethod(methodName))
            {
                calls[methodName] = calls.GetValueOrDefault(methodName, 0) + 1;
            }
        }
        
        // Find object creations
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (creation.Type is IdentifierNameSyntax identifier)
            {
                var typeName = identifier.Identifier.ValueText;
                if (!IsCommonType(typeName))
                {
                    calls[typeName] = calls.GetValueOrDefault(typeName, 0) + 1;
                }
            }
        }
        
        // Take top N calls
        analysis.TopCalls = calls.OrderByDescending(kv => kv.Value)
            .Take(_maxCalls)
            .Select(kv => kv.Key)
            .ToList();
    }
    
    private void ExtractDependencies(CompilationUnitSyntax root, CodeAnalysis analysis)
    {
        var dependencies = new HashSet<string>();
        
        // Extract from using directives
        foreach (var usingDirective in root.Usings)
        {
            var ns = usingDirective.Name?.ToString();
            if (!string.IsNullOrEmpty(ns) && !ns.StartsWith("System") && !ns.StartsWith("Microsoft"))
            {
                dependencies.Add(ns);
            }
        }
        
        analysis.Dependencies = dependencies.ToList();
    }
    
    private void DetectIntegrations(string source, CodeAnalysis analysis)
    {
        // Audio-related patterns
        var audioPatterns = new[] {
            "IAudioFeatures", "AudioFeatures", "NAudioService",
            "IWaveProvider", "ISampleProvider", "Spectrum", "Waveform"
        };
        
        analysis.HasAudio = audioPatterns.Any(p => source.Contains(p, StringComparison.Ordinal));
        
        // Effect-related patterns
        var effectPatterns = new[] {
            "IPhoenixCanvas", "PhoenixCanvas", "PhoenixGpuCanvas",
            "IVisualizerPlugin", "EffectRegistry", "RenderFrame"
        };
        
        analysis.HasEffects = effectPatterns.Any(p => source.Contains(p, StringComparison.Ordinal));
    }
    
    private bool IsCommonMethod(string name)
    {
        // Filter out extremely common BCL methods
        var common = new[] {
            "ToString", "GetHashCode", "Equals", "GetType",
            "GetEnumerator", "Dispose", "Add", "Remove", "Contains",
            "Count", "Any", "Where", "Select", "First", "Last"
        };
        
        return common.Contains(name, StringComparer.Ordinal);
    }
    
    private bool IsCommonType(string name)
    {
        // Filter out common BCL types
        var common = new[] {
            "String", "Int32", "Float", "Double", "Boolean",
            "List", "Dictionary", "Array", "Object"
        };
        
        return common.Contains(name, StringComparer.Ordinal);
    }
}

class CodeAnalysis
{
    public List<string> TopCalls { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public bool HasAudio { get; set; }
    public bool HasEffects { get; set; }
}
