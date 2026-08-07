// ============================================================================
// PIPELINE HEURISTICS - Path-Based Pipeline Information
// ============================================================================
// Ported from add_pipeline_docs.ps1
// Uses file path and content analysis to determine pipeline information
// ============================================================================

namespace PipelineDocsCli;

class PipelineHeuristics
{
    public PipelineInfo GetPipelineInfo(string filePath, string projectRoot, CodeAnalysis analysis)
    {
        var relativePath = Path.GetRelativePath(projectRoot, filePath);
        var fileName = Path.GetFileName(filePath);
        var projectName = Path.GetFileName(projectRoot);
        var projectNameLower = projectName.ToLowerInvariant();
        
        // Determine pipeline info based on file location
        if (projectNameLower.Contains("studio") && relativePath.Contains("Rendering", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "StudioRenderService, CanvasPresenter",
                Calls = analysis.TopCalls.Count > 0
                    ? string.Join(", ", analysis.TopCalls.Take(5))
                    : "StudioGpuCanvas, ShaderCompiler, RenderQueue",
                Flow = "StudioWindow → RenderService → CanvasPresenter → StudioGpuCanvas → Shader",
                Dependencies = analysis.Dependencies.Count > 0
                    ? string.Join(", ", analysis.Dependencies.Take(5))
                    : "StudioRenderService, CanvasPresenter, StudioGpuCanvas",
                Output = "Studio-specific GPU rendering frames",
                AudioIntegration = analysis.HasAudio
                    ? "Integrates with StudioAudioService for real-time audio data"
                    : "No direct audio integration",
                EffectIntegration = analysis.HasEffects
                    ? "Uses StudioEffectRegistry for effect management"
                    : "No direct effect integration"
            };
        }
        if (projectNameLower.Contains("studio") && relativePath.Contains("Views", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "StudioApp, StudioWindow",
                Calls = analysis.TopCalls.Count > 0
                    ? string.Join(", ", analysis.TopCalls.Take(5))
                    : "StudioViewModels, CanvasPresenter, StudioRenderService",
                Flow = "StudioApp → StudioWindow → View → ViewModel → RenderService",
                Dependencies = analysis.Dependencies.Count > 0
                    ? string.Join(", ", analysis.Dependencies.Take(5))
                    : "Avalonia UI, StudioViewModels, StudioRenderService",
                Output = "Studio UI and visualization controls",
                AudioIntegration = analysis.HasAudio
                    ? "Binds to StudioAudioService settings and live audio status"
                    : "No direct audio integration",
                EffectIntegration = analysis.HasEffects
                    ? "Provides effect configuration UI"
                    : "No direct effect integration"
            };
        }
        if (projectNameLower.Contains("studio") && relativePath.Contains("ViewModels", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "Studio Views",
                Calls = analysis.TopCalls.Count > 0
                    ? string.Join(", ", analysis.TopCalls.Take(5))
                    : "StudioServices, StudioModels, Commands",
                Flow = "StudioWindow → View → ViewModel → StudioService/Model",
                Dependencies = analysis.Dependencies.Count > 0
                    ? string.Join(", ", analysis.Dependencies.Take(5))
                    : "StudioServices, StudioModels, Commands, INotifyPropertyChanged",
                Output = "Studio UI state and command bindings",
                AudioIntegration = analysis.HasAudio
                    ? "Exposes audio-reactive controls and settings"
                    : "No direct audio integration",
                EffectIntegration = analysis.HasEffects
                    ? "Manages effect parameter bindings"
                    : "No direct effect integration"
            };
        }
        if (relativePath.Contains("Visuals", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "PluginRegistrationService (plugin discovery)",
                Calls = analysis.TopCalls.Count > 0 
                    ? string.Join(", ", analysis.TopCalls.Take(5)) 
                    : "IPhoenixCanvas (rendering), IAudioFeatures (audio data)",
                Flow = "MainWindow → RenderSurface → PluginRegistrationService → Visualizer → IPhoenixCanvas",
                Dependencies = analysis.Dependencies.Count > 0 
                    ? string.Join(", ", analysis.Dependencies.Take(5)) 
                    : "IVisualizerPlugin, IPhoenixCanvas, IAudioFeatures",
                Output = "Audio-reactive visualizations",
                AudioIntegration = analysis.HasAudio 
                    ? "Uses IAudioFeatures for spectrum, waveform, and beat data" 
                    : "No direct audio integration",
                EffectIntegration = analysis.HasEffects 
                    ? "Implements IVisualizerPlugin interface for plugin system" 
                    : "No direct effect integration"
            };
        }
        else if (relativePath.Contains("Services", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "MainWindow, SettingsWindow, PluginRegistrationService",
                Calls = analysis.TopCalls.Count > 0 ? string.Join(", ", analysis.TopCalls.Take(5)) : "VisualizerSettings, PluginRegistry, FileSystem",
                Flow = "MainWindow → Service → Configuration/Data Processing",
                Dependencies = analysis.Dependencies.Count > 0 ? string.Join(", ", analysis.Dependencies.Take(5)) : "VisualizerSettings, PluginRegistry, FileSystem APIs",
                Output = "Service operations and data management",
                AudioIntegration = "May process audio-related configuration and settings",
                EffectIntegration = "May manage plugin registration and effect discovery"
            };
        }
        else if (relativePath.Contains("ViewModels", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "Views (XAML code-behind), MainWindow",
                Calls = analysis.TopCalls.Count > 0 ? string.Join(", ", analysis.TopCalls.Take(5)) : "Services, Models, Commands",
                Flow = "MainWindow → View → ViewModel → Service/Model",
                Dependencies = analysis.Dependencies.Count > 0 ? string.Join(", ", analysis.Dependencies.Take(5)) : "Services, Models, Commands, INotifyPropertyChanged",
                Output = "UI data binding and business logic",
                AudioIntegration = "May bind to audio settings and visualizer parameters",
                EffectIntegration = "May manage effect parameters and visualizer selection"
            };
        }
        else if (relativePath.Contains("Views", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "MainWindow, Application startup",
                Calls = analysis.TopCalls.Count > 0 ? string.Join(", ", analysis.TopCalls.Take(5)) : "ViewModels, Services, Controls",
                Flow = "MainWindow → View → ViewModel → Service",
                Dependencies = analysis.Dependencies.Count > 0 ? string.Join(", ", analysis.Dependencies.Take(5)) : "Avalonia UI, ViewModels, Controls",
                Output = "User interface and user interactions",
                AudioIntegration = "May display audio settings and visualizer controls",
                EffectIntegration = "May provide UI for effect parameter editing"
            };
        }
        else if (relativePath.Contains("Core", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "Visualizers, Services, Rendering pipeline",
                Calls = analysis.TopCalls.Count > 0 ? string.Join(", ", analysis.TopCalls.Take(5)) : "Audio processing, Rendering APIs, Effect nodes",
                Flow = "MainWindow → RenderSurface → Core → Audio/Rendering",
                Dependencies = analysis.Dependencies.Count > 0 ? string.Join(", ", analysis.Dependencies.Take(5)) : "Audio processing, Rendering APIs, Effect system",
                Output = "Core functionality and data processing",
                AudioIntegration = "Processes audio data and provides analysis",
                EffectIntegration = "Manages effect nodes and processing pipeline"
            };
        }
        else if (relativePath.Contains("Rendering", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "MainWindow, RenderSurface, Visualizers",
                Calls = analysis.TopCalls.Count > 0 ? string.Join(", ", analysis.TopCalls.Take(5)) : "IPhoenixCanvas, GPU APIs, Skia/OpenGL",
                Flow = "MainWindow → RenderSurface → Renderer → Canvas/GPU",
                Dependencies = analysis.Dependencies.Count > 0 ? string.Join(", ", analysis.Dependencies.Take(5)) : "IPhoenixCanvas, GPU APIs, Skia/OpenGL",
                Output = "Rendered visualizations and graphics",
                AudioIntegration = "Renders audio-reactive visual elements",
                EffectIntegration = "Renders effect outputs and visualizations"
            };
        }
        else if (relativePath.Contains("Controls", StringComparison.OrdinalIgnoreCase))
        {
            return new PipelineInfo
            {
                CalledBy = "Views, MainWindow",
                Calls = analysis.TopCalls.Count > 0 ? string.Join(", ", analysis.TopCalls.Take(5)) : "ViewModels, Data binding, UI events",
                Flow = "MainWindow → View → Control → ViewModel",
                Dependencies = analysis.Dependencies.Count > 0 ? string.Join(", ", analysis.Dependencies.Take(5)) : "Avalonia UI, ViewModels, Data binding",
                Output = "Custom UI controls and components",
                AudioIntegration = "May display audio waveforms or visualizer previews",
                EffectIntegration = "May provide effect parameter controls"
            };
        }
        else
        {
            return new PipelineInfo
            {
                CalledBy = "Various components",
                Calls = analysis.TopCalls.Count > 0 ? string.Join(", ", analysis.TopCalls.Take(5)) : "Related services and APIs",
                Flow = "MainWindow → Component → Service/API",
                Dependencies = analysis.Dependencies.Count > 0 ? string.Join(", ", analysis.Dependencies.Take(5)) : "Core services, APIs, Models",
                Output = "Component-specific functionality",
                AudioIntegration = "May process or display audio-related data",
                EffectIntegration = "May interact with effect system"
            };
        }
    }
}

class PipelineInfo
{
    public string CalledBy { get; set; } = "";
    public string Calls { get; set; } = "";
    public string Flow { get; set; } = "";
    public string Dependencies { get; set; } = "";
    public string Output { get; set; } = "";
    public string AudioIntegration { get; set; } = "";
    public string EffectIntegration { get; set; } = "";
}
