using System.Diagnostics;
using System.Windows;
using CSharpCodeAnalyst.Areas.TableArea;
using Prism.Commands;

namespace CSharpCodeAnalyst;

// 3. ViewModel für Daten
public class PersonViewModel
{
    public string Name { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public string Email { get; set; }
}

public class SamplePluginTableData : IPluginTableData
{
    private readonly List<PersonViewModel> _data;

    public SamplePluginTableData()
    {
        _data = new List<PersonViewModel>
        {
            new PersonViewModel { Name = "John Doe", Age = 30, IsActive = true, Email = "john@example.com" },
            new PersonViewModel { Name = "Jane Smith", Age = 25, IsActive = false, Email = "jane@example.com" }
        };
    }

    public IEnumerable<IPluginColumnDefinition> GetColumns()
    {
        return new List<IPluginColumnDefinition>
        {
            new PluginColumnDefinition
            {
                PropertyName = "Name",
                DisplayName = "Name",
                Type = ColumnType.Text,
                Width = 150
            },
            new PluginColumnDefinition
            {
                PropertyName = "Age",
                DisplayName = "Alter",
                Type = ColumnType.Text,
                Width = 80
            },
            new PluginColumnDefinition
            {
                PropertyName = "Email",
                DisplayName = "E-Mail",
                Type = ColumnType.Link,
                Width = 200,
                ClickCommand = new DelegateCommand<string>(email =>
                {
                    // Email-Client öffnen
                    Process.Start($"mailto:{email}");
                })
            },
            new PluginColumnDefinition
            {
                PropertyName = "IsActive",
                DisplayName = "Aktiv",
                Type = ColumnType.Toggle,
                Width = 80
            }
        };
    }

    public IEnumerable<object> GetData() => _data;
    public DataTemplate? GetRowDetailsTemplate() => null;
    public string? GetTitle() => "Personen-Liste";
}