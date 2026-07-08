using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.History;

public sealed class ImportHistoryDialogViewModel : INotifyPropertyChanged
{
    public string Description { get; init; } = Strings.History_ImportDialog_Description;

    public string RepositoryPath
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RepositoryPathError));
            OnPropertyChanged(nameof(CanAccept));
        }
    } = string.Empty;

    public string OutputFilePath
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputFilePathError));
            OnPropertyChanged(nameof(CanAccept));
        }
    } = string.Empty;

    public string RepositoryPathError
    {
        get => ValidateRepositoryPath(RepositoryPath);
    }

    public string OutputFilePathError
    {
        get => ValidateOutputFilePath(OutputFilePath);
    }

    public bool CanAccept
    {
        get => RepositoryPathError.Length == 0 && OutputFilePathError.Length == 0;
    }

    public bool OutputFileAlreadyExists
    {
        get => File.Exists(OutputFilePath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string ValidateRepositoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Strings.History_RepositoryPath_Required;
        }

        if (!Directory.Exists(path))
        {
            return Strings.History_RepositoryPath_NotFound;
        }

        if (!Directory.Exists(Path.Combine(path, ".git")))
        {
            return Strings.History_RepositoryPath_NotGitRepository;
        }

        return string.Empty;
    }

    private static string ValidateOutputFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Strings.History_OutputFilePath_Required;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Format(Strings.History_OutputFilePath_Invalid, ex.Message);
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return Strings.History_OutputFilePath_DirectoryNotFound;
        }

        if (File.Exists(fullPath))
        {
            // Existing file is fine, the user is asked for confirmation before it is overwritten.
            return string.Empty;
        }

        // Probe whether the file could actually be created without leaving it behind.
        try
        {
            using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
            stream.Close();
            File.Delete(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return string.Format(Strings.History_OutputFilePath_CannotCreate, ex.Message);
        }

        return string.Empty;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}