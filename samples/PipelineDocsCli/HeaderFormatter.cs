// ============================================================================
// HEADER FORMATTER - Generate Pipeline Documentation Headers
// ============================================================================

namespace PipelineDocsCli;

class HeaderFormatter
{
    public static string Format(PipelineInfo info)
    {
        var header = $@"// ============================================================================
// PIPELINE DOCUMENTATION (auto-generated)
// ============================================================================
// PIPELINE: This file is called by: {info.CalledBy}
// PIPELINE: This file calls: {info.Calls}
// PIPELINE: Flow: {info.Flow}
// PIPELINE: Dependencies: {info.Dependencies}
// PIPELINE: Output: {info.Output}
// PIPELINE: Audio Integration: {info.AudioIntegration}
// PIPELINE: Effect Integration: {info.EffectIntegration}
// ============================================================================

";
        
        return header;
    }
}
