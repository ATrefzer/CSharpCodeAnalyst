using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace CSharpCodeAnalyst.Areas.TableArea
{
    public partial class DynamicDataGrid : UserControl
    {
        public static readonly DependencyProperty TableDataProperty =
            DependencyProperty.Register(
                nameof(TableData),
                typeof(ITableData),
                typeof(DynamicDataGrid),
                new PropertyMetadata(null, OnTableDataChanged));



        public DynamicDataGrid()
        {
            InitializeComponent();
        }

        public ITableData TableData
        {
            get => (ITableData)GetValue(TableDataProperty);
            set => SetValue(TableDataProperty, value);
        }

        /// <summary>
        ///     Helper to get property values via reflection
        /// </summary>
        private object? GetPropertyValue(object obj, string propertyName)
        {
            try
            {
                var type = obj.GetType();
                var property = type.GetProperty(propertyName);
                return property?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        private static void OnTableDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DynamicDataGrid control)
            {
                control.RebuildDataGrid();
            }
        }

        private void RebuildDataGrid()
        {
            try
            {
                // DataGrid leeren
                MainDataGrid.Columns.Clear();
                MainDataGrid.ItemsSource = null;

                if (TableData == null)
                {
                    ShowEmptyState(true);
                    return;
                }

                // Spalten aufbauen
                var columns = TableData.GetColumns();
                if (columns == null || !columns.Any())
                {
                    ShowEmptyState(true);
                    return;
                }

                foreach (var columnDef in columns)
                {
                    var column = CreateDataGridColumn(columnDef);
                    if (column != null)
                    {
                        MainDataGrid.Columns.Add(column);
                    }
                }

                // Daten binden
                var data = TableData.GetData();
                MainDataGrid.ItemsSource = data;

                // Empty State verwalten
                ShowEmptyState(data == null || !data.Any());

                // Row Details Template setzen falls vorhanden
                if (TableData.GetRowDetailsTemplate() != null)
                {
                    MainDataGrid.RowDetailsTemplate = TableData.GetRowDetailsTemplate();

                    // RowDetailsVisibilityMode auf Collapsed setzen als Standard
                    MainDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;

                    // Event Handler für LoadingRow hinzufügen um IsExpanded pro Zeile zu verwalten
                    MainDataGrid.LoadingRow += OnDataGridLoadingRow;
                }
            }
            catch (Exception ex)
            {
                // Error Handling - in Production Logger verwenden
                Debug.WriteLine($"Error rebuilding DataGrid: {ex.Message}");
                ShowEmptyState(true, "Fehler beim Laden der Daten");
            }
        }

        private DataGridColumn CreateDataGridColumn(ITableColumnDefinition columnDef)
        {
            // Wenn es eine expandable Spalte ist, erweitern wir sie um den Toggle-Button
            if (columnDef.IsExpandable)
            {
                return CreateExpandableColumn(columnDef);
            }

            return columnDef.Type switch
            {
                ColumnType.Text => CreateTextColumn(columnDef),
                ColumnType.Link => CreateLinkColumn(columnDef),
                ColumnType.Image => CreateImageColumn(columnDef),
                ColumnType.Toggle => CreateToggleColumn(columnDef),
                ColumnType.Custom => CreateCustomColumn(columnDef),
                _ => CreateTextColumn(columnDef)
            };
        }

        /// <summary>
        ///     Erstellt eine expandierbare Spalte mit Toggle-Button (wie im Original)
        /// </summary>
        private DataGridTemplateColumn CreateExpandableColumn(ITableColumnDefinition columnDef)
        {
            var column = new DataGridTemplateColumn
            {
                Header = columnDef.DisplayName,
                Width = columnDef.Width == 0 ? DataGridLength.Auto : new DataGridLength(columnDef.Width)
            };


            // Template mit DockPanel und ToggleButton erstellen (wie im Original)
            var xamlTemplate = $@"

<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <DockPanel LastChildFill='True'>
        
        <DockPanel.Resources>
            <ResourceDictionary>
                <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source=""/Styles/ButtonStyles.xaml"" />
                    <ResourceDictionary Source=""/Styles/DataGridStyles.xaml"" />
                </ResourceDictionary.MergedDictionaries>
            </ResourceDictionary>
        </DockPanel.Resources>
                      
        <ToggleButton DockPanel.Dock=""Left""
                    Style=""{{StaticResource ExpandCollapseButtonStyle}}""
                    IsChecked=""{{Binding IsExpanded, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}""
                    Margin=""0,0,5,0"" />

        <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
            <TextBlock Text='{{Binding {columnDef.PropertyName}}}' FontWeight='Bold' />
        </StackPanel>
    </DockPanel>
</DataTemplate>";

            try
            {
                var template = (DataTemplate)XamlReader.Parse(xamlTemplate);
                column.CellTemplate = template;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating expandable template: {ex.Message}");
            }

            return column;
        }

        private DataGridTextColumn CreateTextColumn(ITableColumnDefinition columnDef)
        {
            var column = new DataGridTextColumn
            {
                Header = columnDef.DisplayName,
                Binding = new Binding(columnDef.PropertyName),
                Width = columnDef.Width == 0 ? DataGridLength.Auto : new DataGridLength(columnDef.Width)
            };

            return column;
        }

        private DataGridTemplateColumn CreateLinkColumn(ITableColumnDefinition columnDef)
        {
            var column = new DataGridTemplateColumn
            {
                Header = columnDef.DisplayName,
                Width = columnDef.Width == 0 ? DataGridLength.Auto : new DataGridLength(columnDef.Width)
            };

            // Template programmatisch erstellen - EINFACHER ANSATZ
            var cellTemplate = new DataTemplate();

            // Button als FrameworkElementFactory erstellen
            var buttonFactory = new FrameworkElementFactory(typeof(Button));
            buttonFactory.SetValue(Button.ContentProperty, new Binding(columnDef.PropertyName));
            buttonFactory.SetValue(Button.ForegroundProperty, Brushes.Blue);
            buttonFactory.SetValue(Button.CursorProperty, Cursors.Hand);
            buttonFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
            buttonFactory.SetValue(Button.BackgroundProperty, Brushes.Transparent);
            buttonFactory.SetValue(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Left);
            buttonFactory.SetValue(Button.PaddingProperty, new Thickness(0));

            // Style für Underline setzen
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.TemplateProperty, CreateLinkButtonTemplate()));
            buttonFactory.SetValue(Button.StyleProperty, style);

            // Command direkt setzen - das ist der Schlüssel!
            if (columnDef.ClickCommand != null)
            {
                buttonFactory.SetValue(Button.CommandProperty, columnDef.ClickCommand);
                buttonFactory.SetValue(Button.CommandParameterProperty, new Binding(columnDef.PropertyName));
            }

            cellTemplate.VisualTree = buttonFactory;
            column.CellTemplate = cellTemplate;

            return column;
        }

        private ControlTemplate CreateLinkButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetValue(TextBlock.TextProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            textBlockFactory.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
            textBlockFactory.SetValue(TextBlock.ForegroundProperty, Brushes.Blue);
            template.VisualTree = textBlockFactory;
            return template;
        }

        private DataGridTemplateColumn CreateImageColumn(ITableColumnDefinition columnDef)
        {
            var column = new DataGridTemplateColumn
            {
                Header = columnDef.DisplayName,
                Width = columnDef.Width == 0 ? new DataGridLength(50) : new DataGridLength(columnDef.Width)
            };

            var cellTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(Image));
            factory.SetValue(Image.SourceProperty, new Binding(columnDef.PropertyName));
            factory.SetValue(Image.HeightProperty, 24.0);
            factory.SetValue(Image.WidthProperty, 24.0);
            factory.SetValue(Image.StretchProperty, Stretch.Uniform);

            cellTemplate.VisualTree = factory;
            column.CellTemplate = cellTemplate;

            return column;
        }

        private DataGridTemplateColumn CreateToggleColumn(ITableColumnDefinition columnDef)
        {
            var column = new DataGridTemplateColumn
            {
                Header = columnDef.DisplayName,
                Width = columnDef.Width == 0 ? new DataGridLength(100) : new DataGridLength(columnDef.Width)
            };

            var cellTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(ToggleButton));
            factory.SetBinding(ToggleButton.IsCheckedProperty, new Binding(columnDef.PropertyName));

            // Command direkt setzen - EINFACH UND FUNKTIONIERT
            if (columnDef.ClickCommand != null)
            {
                factory.SetValue(ToggleButton.CommandProperty, columnDef.ClickCommand);
                factory.SetValue(ToggleButton.CommandParameterProperty, new Binding()); // Ganzes DataContext
            }

            cellTemplate.VisualTree = factory;
            column.CellTemplate = cellTemplate;

            return column;
        }

        private DataGridColumn CreateCustomColumn(ITableColumnDefinition columnDef)
        {
            // Für Custom-Spalten sollte das Plugin ein Template liefern
            if (columnDef is ICustomColumnDefinition customDef && customDef.CellTemplate != null)
            {
                return new DataGridTemplateColumn
                {
                    Header = columnDef.DisplayName,
                    Width = columnDef.Width == 0 ? DataGridLength.Auto : new DataGridLength(columnDef.Width),
                    CellTemplate = customDef.CellTemplate
                };
            }

            // Fallback auf Text-Spalte
            return CreateTextColumn(columnDef);
        }

        /// <summary>
        ///     Event Handler für LoadingRow - setzt RowDetails Visibility basierend auf IsExpanded
        /// </summary>
        private void OnDataGridLoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is INotifyPropertyChanged viewModel)
            {
                // Initial die Visibility setzen
                UpdateRowDetailsVisibility(e.Row);

                // PropertyChanged abonnieren um auf IsExpanded Änderungen zu reagieren
                viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == "IsExpanded")
                    {
                        UpdateRowDetailsVisibility(e.Row);
                    }
                };
            }
        }

        /// <summary>
        ///     Aktualisiert die RowDetails Visibility basierend auf IsExpanded Property
        /// </summary>
        private void UpdateRowDetailsVisibility(DataGridRow row)
        {
            if (row.DataContext != null)
            {
                var isExpandedValue = GetPropertyValue(row.DataContext, "IsExpanded");
                if (isExpandedValue is bool isExpanded)
                {
                    row.DetailsVisibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        ///     Zeigt oder versteckt den Empty State
        /// </summary>
        private void ShowEmptyState(bool show, string message = "Keine Daten verfügbar")
        {
            EmptyStateText.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            EmptyStateText.Text = message;
            MainDataGrid.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}