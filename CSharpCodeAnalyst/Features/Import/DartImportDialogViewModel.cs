using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Asks for the directory of a Dart or Flutter project. Unlike the doxygen import there is
///     nothing else to configure: assembly names come from the package names in the pubspec files,
///     and the analyzer decides itself which files belong to the project.
///     The validation is worth its lines - an unresolved project is the common failure mode and
///     would produce an almost empty graph instead of an error.
/// </summary>
public class DartImportDialogViewModel : INotifyPropertyChanged
{
    private string _projectDirectory = string.Empty;

    public string Description
    {
        get => Strings.ImportDart_Description;
    }

    public string ProjectDirectory
    {
        get => _projectDirectory;
        set
        {
            if (_projectDirectory == value)
            {
                return;
            }

            _projectDirectory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProjectDirectoryError));
            OnPropertyChanged(nameof(CanAccept));
        }
    }

    public string ProjectDirectoryError
    {
        get
        {
            if (_projectDirectory.Length == 0)
            {
                return string.Empty;
            }

            if (!Directory.Exists(_projectDirectory))
            {
                return Strings.ImportDart_DirectoryDoesNotExist;
            }

            if (!File.Exists(Path.Combine(_projectDirectory, "pubspec.yaml")))
            {
                return Strings.ImportDart_NoPubspec;
            }

            if (!DartRunner.IsProjectResolved(_projectDirectory))
            {
                return Strings.ImportDart_NotResolved;
            }

            return string.Empty;
        }
    }

    public bool CanAccept
    {
        get => _projectDirectory.Length > 0 && ProjectDirectoryError.Length == 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
