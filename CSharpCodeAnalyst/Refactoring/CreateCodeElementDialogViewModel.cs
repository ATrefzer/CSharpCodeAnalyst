using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Refactoring;

public class CreateCodeElementDialogViewModel : INotifyPropertyChanged
{
    private readonly CodeElement? _parent;
    private readonly VirtualRefactoringService _refactoringService;
    private string _elementName;
    private CodeElementType _selectedElementType;

    public CreateCodeElementDialogViewModel(VirtualRefactoringService refactoringService, CodeElement? parent)
    {
        _refactoringService = refactoringService;
        _parent = parent;

        // Get valid element types for this context
        var validTypes = refactoringService.GetValidChildTypes(parent);
        ValidElementTypes = new ObservableCollection<CodeElementType>(validTypes);

        Debug.Assert(ValidElementTypes.Any());
        _selectedElementType = ValidElementTypes.First();
        _elementName = refactoringService.GetDefaultName(_selectedElementType, parent);
    }

    public ObservableCollection<CodeElementType> ValidElementTypes { get; }

    public CodeElementType SelectedElementType
    {
        get => _selectedElementType;
        set
        {
            if (_selectedElementType != value)
            {
                _selectedElementType = value;
                OnPropertyChanged();

                // Update default name when type changes
                ElementName = _refactoringService.GetDefaultName(value, _parent);
            }
        }
    }

    public string ElementName
    {
        get => _elementName;
        set
        {
            if (_elementName != value)
            {
                _elementName = value;
                OnPropertyChanged();
            }
        }
    }

    public string ParentContext
    {
        get
        {
            if (_parent == null)
            {
                return "Root (no parent)";
            }

            return $"Parent: {_parent.FullName} ({_parent.ElementType})";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CodeElement? CreateElement()
    {
        if (string.IsNullOrWhiteSpace(ElementName))
        {
            return null;
        }

        return _refactoringService.CreateVirtualElement(SelectedElementType, ElementName.Trim(), _parent);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}