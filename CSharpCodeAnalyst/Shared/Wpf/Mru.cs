using System.Windows.Input;

namespace CSharpCodeAnalyst.Shared.Wpf;

internal class Mru(string path, ICommand command)
{
    public string Path { get; set; } = path;
    public string? ImageSource { get; set; }
    public ICommand Command { get; set; } = command;
}