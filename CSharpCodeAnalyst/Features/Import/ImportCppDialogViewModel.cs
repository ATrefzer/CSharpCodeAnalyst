using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.Import;

public class ImportCppDialogViewModel : INotifyPropertyChanged
{
    private string _projectName = string.Empty;
    private bool _projectNameEditedByUser;
    private string _sourceDirectory = string.Empty;

    public string Description
    {
        get => Strings.ImportCpp_Description;
    }

    public string SourceDirectory
    {
        get => _sourceDirectory;
        set
        {
            if (_sourceDirectory == value)
            {
                return;
            }

            _sourceDirectory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceDirectoryError));
            OnPropertyChanged(nameof(CanAccept));

            // Suggest the directory name as project name until the user typed an own one.
            if (!_projectNameEditedByUser)
            {
                var suggestion = Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrEmpty(suggestion))
                {
                    _projectName = suggestion;
                    OnPropertyChanged(nameof(ProjectName));
                    OnPropertyChanged(nameof(CanAccept));
                }
            }
        }
    }

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (_projectName == value)
            {
                return;
            }

            _projectName = value;
            _projectNameEditedByUser = value.Length > 0;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanAccept));
        }
    }

    public string SourceDirectoryError
    {
        get => _sourceDirectory.Length > 0 && !Directory.Exists(_sourceDirectory)
            ? Strings.ImportCpp_DirectoryDoesNotExist
            : string.Empty;
    }

    public bool CanAccept
    {
        get => Directory.Exists(_sourceDirectory) && !string.IsNullOrWhiteSpace(_projectName);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}