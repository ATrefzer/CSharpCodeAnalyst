using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using CSharpCodeAnalyst.Areas.TableArea;
using Prism.Commands;

namespace CSharpCodeAnalyst;

// 3. ViewModel für Daten
public class PersonViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }
    public string Name { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public string Email { get; set; }
    public string Department { get; set; }
    public DateTime HireDate { get; set; }
    public List<string> Skills { get; set; }
    public string Notes { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}


/// <summary>
/// Konkrete Implementierung für Beispiel-Tabellendaten mit RowDetails
/// </summary>
public class SamplePersonTableData : IPluginTableData
{
    private readonly List<PersonViewModel> _persons;

    public SamplePersonTableData()
    {
        // Beispiel-Daten erstellen
        _persons = new List<PersonViewModel>
            {
                new PersonViewModel
                {
                    Name = "Max Mustermann",
                    Age = 32,
                    IsActive = true,
                    Email = "max.mustermann@example.com",
                    Department = "IT",
                    HireDate = new DateTime(2020, 3, 15),
                    Skills = new List<string> { "C#", "WPF", "SQL Server", "Azure" },
                    Notes = "Sehr erfahrener Entwickler, Team Lead für Frontend-Projekte."
                },
                new PersonViewModel
                {
                    Name = "Anna Schmidt",
                    Age = 28,
                    IsActive = true,
                    Email = "anna.schmidt@example.com",
                    Department = "Marketing",
                    HireDate = new DateTime(2021, 7, 1),
                    Skills = new List<string> { "Social Media", "Content Creation", "Analytics", "Adobe Creative Suite" },
                    Notes = "Kreative Denkerin mit starkem Fokus auf digitales Marketing."
                },
                new PersonViewModel
                {
                    Name = "Peter Weber",
                    Age = 45,
                    IsActive = false,
                    Email = "peter.weber@example.com",
                    Department = "Vertrieb",
                    HireDate = new DateTime(2018, 1, 10),
                    Skills = new List<string> { "B2B Sales", "Customer Relations", "Negotiation" },
                    Notes = "Derzeit in Elternzeit, kehrt im nächsten Quartal zurück."
                },
                new PersonViewModel
                {
                    Name = "Lisa Müller",
                    Age = 35,
                    IsActive = true,
                    Email = "lisa.mueller@example.com",
                    Department = "HR",
                    HireDate = new DateTime(2019, 9, 20),
                    Skills = new List<string> { "Recruiting", "Employee Relations", "Training & Development" },
                    Notes = "Leitet die HR-Abteilung und ist für alle Personalangelegenheiten zuständig."
                },
                new PersonViewModel
                {
                    Name = "Tom Johnson",
                    Age = 29,
                    IsActive = false,
                    Email = "tom.johnson@example.com",
                    Department = "IT",
                    HireDate = new DateTime(2022, 5, 8),
                    Skills = new List<string> { "JavaScript", "React", "Node.js", "Docker" },
                    Notes = "Junior Developer, arbeitet derzeit remote aus dem Homeoffice."
                }
            };
    }

    public IEnumerable<IPluginColumnDefinition> GetColumns()
    {
        return new List<IPluginColumnDefinition>
            {
                // Name-Spalte mit Expand-Button - ERSTE SPALTE MIT EXPAND-FUNKTION
                new PluginColumnDefinition
                {
                    PropertyName = "Name",
                    DisplayName = "Name",
                    Type = ColumnType.Text,
                    Width = 200,
                    IsExpandable = true // WICHTIG: Diese Spalte bekommt den Expand-Button
                },

                // Avatar-Spalte (Bild)
                new PluginColumnDefinition
                {
                    DisplayName = "",
                    Type = ColumnType.Image,
                    Width = 40
                },

                // Alter-Spalte (Text)
                new PluginColumnDefinition
                {
                    PropertyName = "Age",
                    DisplayName = "Alter",
                    Type = ColumnType.Text,
                    Width = 60
                },

                // Abteilung-Spalte (Text)
                new PluginColumnDefinition
                {
                    PropertyName = "Department",
                    DisplayName = "Abteilung",
                    Type = ColumnType.Text,
                    Width = 100
                },

                // Email-Spalte (Link)
                new PluginColumnDefinition
                {
                    PropertyName = "Email",
                    DisplayName = "E-Mail",
                    Type = ColumnType.Link,
                    Width = 220,
                    ClickCommand = new DelegateCommand<string>(email =>
                    {
                        if (!string.IsNullOrEmpty(email))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = $"mailto:{email}",
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Fehler beim Öffnen des E-Mail-Clients: {ex.Message}",
                                              "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    })
                },

                // Status-Spalte (Toggle)
                new PluginColumnDefinition
                {
                    PropertyName = "IsActive",
                    DisplayName = "Aktiv",
                    Type = ColumnType.Toggle,
                    Width = 70,
                    ClickCommand = new DelegateCommand<PersonViewModel>(person =>
                    {
                        if (person != null)
                        {
                            // Hier könnte Geschäftslogik stehen
                            var status = person.IsActive ? "aktiviert" : "deaktiviert";
                            MessageBox.Show($"{person.Name} wurde {status}.",
                                          "Status geändert", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    })
                }
            };
    }

    public IEnumerable<object> GetData()
    {
        return _persons;
    }

    public DataTemplate? GetRowDetailsTemplate()
    {
        // RowDetails Template als XAML-String erstellen
        var xamlTemplate = @"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                              xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
                    <Border Background='#F8F9FA' Padding='15' Margin='5' CornerRadius='5' BorderBrush='#E9ECEF' BorderThickness='1'>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width='Auto' />
                                <ColumnDefinition Width='*' />
                                <ColumnDefinition Width='Auto' />
                                <ColumnDefinition Width='*' />
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition Height='Auto' />
                                <RowDefinition Height='Auto' />
                                <RowDefinition Height='Auto' />
                            </Grid.RowDefinitions>

                            <!-- Einstellungsdatum -->
                            <TextBlock Grid.Row='0' Grid.Column='0' Text='Eingestellt:' FontWeight='Bold' Margin='0,0,10,5' />
                            <TextBlock Grid.Row='0' Grid.Column='1' Text='{Binding HireDate, StringFormat={}{0:dd.MM.yyyy}}' Margin='0,0,20,5' />
                            
                            <!-- Skills -->
                            <TextBlock Grid.Row='0' Grid.Column='2' Text='Skills:' FontWeight='Bold' Margin='0,0,10,5' />
                            <ItemsControl Grid.Row='0' Grid.Column='3' ItemsSource='{Binding Skills}' Margin='0,0,0,5'>
                                <ItemsControl.ItemsPanel>
                                    <ItemsPanelTemplate>
                                        <WrapPanel Orientation='Horizontal' />
                                    </ItemsPanelTemplate>
                                </ItemsControl.ItemsPanel>
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate>
                                        <Border Background='#007ACC' CornerRadius='3' Padding='5,2' Margin='0,0,5,2'>
                                            <TextBlock Text='{Binding}' Foreground='White' FontSize='11' />
                                        </Border>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>

                            <!-- Notizen -->
                            <TextBlock Grid.Row='1' Grid.Column='0' Text='Notizen:' FontWeight='Bold' Margin='0,5,10,0' VerticalAlignment='Top' />
                            <TextBlock Grid.Row='1' Grid.Column='1' Grid.ColumnSpan='3' Text='{Binding Notes}' 
                                       TextWrapping='Wrap' Margin='0,5,0,0' FontStyle='Italic' />
                        </Grid>
                    </Border>
                </DataTemplate>";

        try
        {
            return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xamlTemplate);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating row details template: {ex.Message}");
            return null;
        }
    }

    public bool HasRowDetails()
    {
        return true; // Diese Implementierung hat RowDetails
    }

    public string? GetTitle()
    {
        return "Mitarbeiter-Übersicht (mit Details)";
    }

    /// <summary>
    /// Hilfsmethode um neue Person hinzuzufügen (für Demo)
    /// </summary>
    public void AddPerson(PersonViewModel person)
    {
        _persons.Add(person);
    }

    /// <summary>
    /// Hilfsmethode um Person zu entfernen (für Demo)
    /// </summary>
    public void RemovePerson(PersonViewModel person)
    {
        _persons.Remove(person);
    }

    /// <summary>
    /// Gibt die interne Liste zurück (für erweiterte Operationen)
    /// </summary>
    public List<PersonViewModel> GetPersons()
    {
        return _persons;
    }
}

//public class SamplePluginTableData : IPluginTableData
//{
//    private readonly List<PersonViewModel> _data;

//    public SamplePluginTableData()
//    {
//        _data = new List<PersonViewModel>
//        {
//            new PersonViewModel { Name = "John Doe", Age = 30, IsActive = true, Email = "john@example.com" },
//            new PersonViewModel { Name = "Jane Smith", Age = 25, IsActive = false, Email = "jane@example.com" }
//        };
//    }

//    public IEnumerable<IPluginColumnDefinition> GetColumns()
//    {
//        return new List<IPluginColumnDefinition>
//        {
//            new PluginColumnDefinition
//            {
//                PropertyName = "Name",
//                DisplayName = "Name",
//                Type = ColumnType.Text,
//                Width = 150
//            },
//            new PluginColumnDefinition
//            {
//                PropertyName = "Age",
//                DisplayName = "Alter",
//                Type = ColumnType.Text,
//                Width = 80
//            },
//            new PluginColumnDefinition
//            {
//                PropertyName = "Email",
//                DisplayName = "E-Mail",
//                Type = ColumnType.Link,
//                Width = 200,
//                ClickCommand = new DelegateCommand<string>(email =>
//                {
//                    // Email-Client öffnen
//                    Process.Start($"mailto:{email}");
//                })
//            },
//            new PluginColumnDefinition
//            {
//                PropertyName = "IsActive",
//                DisplayName = "Aktiv",
//                Type = ColumnType.Toggle,
//                Width = 80
//            }
//        };
//    }

//    public IEnumerable<object> GetData() => _data;
//    public DataTemplate? GetRowDetailsTemplate() => null;
//    public string? GetTitle() => "Personen-Liste";
//}