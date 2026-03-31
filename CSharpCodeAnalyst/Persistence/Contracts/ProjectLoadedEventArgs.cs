using CSharpCodeAnalyst.Persistence.Dto;

namespace CSharpCodeAnalyst.Persistence.Contracts;

public sealed class ProjectLoadedEventArgs(ProjectData data, string filePath) : EventArgs
{
    public ProjectData Data { get; } = data;
    public string FilePath { get; } = filePath;
}
