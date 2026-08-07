// ============================================================================
// HEADER EDITOR - Insert/Update Pipeline Documentation Headers
// ============================================================================
// Handles inserting new headers or updating existing auto-generated ones
// Respects manual documentation (non-auto blocks)
// ============================================================================

using System.Text;
using System.Text.RegularExpressions;

namespace PipelineDocsCli;

class HeaderEditor
{
    private const string AutoMarkerStart = "// PIPELINE DOCUMENTATION (auto-generated)";
    private const string ManualMarker = "// PIPELINE DOCUMENTATION";
    private const string BlockEnd = "// ============================================================================";
    
    public bool HasManualDocumentation(string content)
    {
        // Has manual docs if it contains the marker but NOT the auto-generated marker
        return content.Contains(ManualMarker) && !content.Contains(AutoMarkerStart);
    }
    
    public string UpdateOrInsertHeader(string content, string newHeader, bool replaceManual)
    {
        if (content.Contains(AutoMarkerStart))
        {
            return ReplacePipelineBlock(content, newHeader);
        }

        if (content.Contains(ManualMarker))
        {
            return replaceManual ? ReplacePipelineBlock(content, newHeader) : content;
        }

        return InsertAtTop(content, newHeader);
    }
    
    private string ReplacePipelineBlock(string content, string newHeader)
    {
        // Only replace a leading header block — never match PIPELINE comments embedded in the file body.
        var pattern =
            @"\A(?:\uFEFF?)(?:\s*\r?\n)*// ============================================================================\r?\n// PIPELINE DOCUMENTATION(?: \(auto-generated\))?\r?\n// ============================================================================\r?\n(?:(?!(?:// ============================================================================)).)*// ============================================================================\r?\n(?:[ \t]*\r?\n)?";
        var regex = new Regex(pattern, RegexOptions.Singleline);

        if (regex.IsMatch(content))
        {
            return regex.Replace(content, newHeader, 1);
        }

        return content;
    }
    
    private string InsertAtTop(string content, string newHeader)
    {
        // Skip any leading whitespace/empty lines to preserve file structure
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int insertIndex = 0;
        
        // Skip empty lines and BOM at the start
        while (insertIndex < lines.Length && string.IsNullOrWhiteSpace(lines[insertIndex]))
        {
            insertIndex++;
        }
        
        // If file starts with using statements, insert before them
        // Otherwise insert at the very top
        if (insertIndex > 0)
        {
            // Reconstruct with header inserted after initial whitespace
            var beforeHeader = string.Join(Environment.NewLine, lines.Take(insertIndex));
            var afterHeader = string.Join(Environment.NewLine, lines.Skip(insertIndex));
            return beforeHeader + Environment.NewLine + newHeader + afterHeader;
        }
        
        // Insert at very top
        return newHeader + content;
    }
}
