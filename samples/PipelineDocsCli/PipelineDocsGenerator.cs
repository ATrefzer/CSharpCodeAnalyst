// ============================================================================
// PIPELINE DOCUMENTATION GENERATOR - Main Orchestrator
// ============================================================================

using System.Text;

namespace PipelineDocsCli;

class PipelineDocsGenerator
{
    private readonly GeneratorOptions _options;
    private readonly PipelineHeuristics _heuristics;
    private readonly Analyzer _analyzer;
    private readonly HeaderEditor _editor;
    
    public PipelineDocsGenerator(GeneratorOptions options)
    {
        _options = options;
        _heuristics = new PipelineHeuristics();
        _analyzer = new Analyzer(options.MaxCalls);
        _editor = new HeaderEditor();
    }
    
    public GeneratorResult Run()
    {
        var result = new GeneratorResult();
        var files = DiscoverFiles();
        
        foreach (var file in files)
        {
            if (_options.Verbose)
            {
                Console.WriteLine($"  Processing: {Path.GetFileName(file)}");
            }
            
            try
            {
                var content = File.ReadAllText(file, Encoding.UTF8);
                
                // Skip if file is too small or already has manual (non-auto) documentation
                if (content.Length < 100)
                {
                    result.FilesSkipped++;
                    continue;
                }
                
                if (content.Contains("// PIPELINE DOCUMENTATION (auto-generated)", StringComparison.Ordinal) &&
                    !_options.UpdateExisting)
                {
                    result.FilesSkipped++;
                    continue;
                }

                if (_editor.HasManualDocumentation(content) && !_options.ReplaceManual)
                {
                    result.FilesSkipped++;
                    continue;
                }
                
                // Analyze the file
                var analysis = _analyzer.Analyze(content, file);
                var pipelineInfo = _heuristics.GetPipelineInfo(file, _options.ProjectDir, analysis);
                
                // Generate header
                var header = HeaderFormatter.Format(pipelineInfo);
                
                // Update or insert header
                var newContent = _editor.UpdateOrInsertHeader(content, header, _options.ReplaceManual);
                
                // Write if changed
                if (newContent != content)
                {
                    if (!_options.DryRun)
                    {
                        File.WriteAllText(file, newContent, new UTF8Encoding(false)); // UTF-8 without BOM
                    }
                    result.FilesUpdated++;
                    
                    if (_options.Verbose)
                    {
                        Console.WriteLine($"    ✅ Updated");
                    }
                }
                else
                {
                    result.FilesSkipped++;
                }
                
                result.FilesProcessed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error processing {Path.GetFileName(file)}: {ex.Message}");
                result.FilesSkipped++;
            }
        }
        
        return result;
    }
    
    private List<string> DiscoverFiles()
    {
        // Use provided file list if available
        if (_options.Files.Count > 0)
        {
            return _options.Files.Where(f => File.Exists(f) && f.EndsWith(".cs")).ToList();
        }
        
        // Otherwise scan project directory
        var projectDir = Path.GetFullPath(_options.ProjectDir);
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => ShouldProcess(f, projectDir))
            .ToList();
        
        return csFiles;
    }
    
    private bool ShouldProcess(string filePath, string projectRoot)
    {
        var relativePath = Path.GetRelativePath(projectRoot, filePath);
        var parts = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        // Match path segments only — substring "obj" wrongly matches "Objects".
        var excludedSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "obj", "bin", "backup", "Backup1", ".broken",
            "AvaloniaEdit", "AvaloniaUI.MCP", "Matplotlib.Net",
            "node_modules", "packages"
        };

        if (parts.Any(p => excludedSegments.Contains(p)))
            return false;

        // Also skip common dead suffixes in file names
        var name = Path.GetFileName(filePath);
        if (name.Contains(".broken", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
