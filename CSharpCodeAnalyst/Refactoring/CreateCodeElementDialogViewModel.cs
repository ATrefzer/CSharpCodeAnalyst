using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Refactoring;

public sealed class CreateCodeElementDialogViewModel : INotifyPropertyChanged
{
    private readonly ICodeElementNaming _naming;
    private readonly CodeElement? _parent;
    private string _elementName;
    private CodeElementType _selectedElementType;

    public CreateCodeElementDialogViewModel(CodeElement? parent, List<CodeElementType> validTypes, ICodeElementNaming naming)
    {
        _parent = parent;
        _naming = naming;

        // Get valid element types for this context
        ValidElementTypes = new ObservableCollection<CodeElementType>(validTypes);

        Debug.Assert(ValidElementTypes.Any());
        _selectedElementType = ValidElementTypes.First();
        _elementName = _naming.GetDefaultName(_selectedElementType);
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
                ElementName = _naming.GetDefaultName(_selectedElementType);
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

    public CodeElementSpecs? GetCodeElementSpecs()
    {
        if (string.IsNullOrWhiteSpace(ElementName))
        {
            return null;
        }

        return new CodeElementSpecs(SelectedElementType, ElementName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public bool IsValid()
    {
        return _naming.IsValid(SelectedElementType, ElementName);
    }
}