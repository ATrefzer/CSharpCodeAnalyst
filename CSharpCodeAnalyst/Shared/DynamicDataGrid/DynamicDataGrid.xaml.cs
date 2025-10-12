using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.Attributes;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Shared.DynamicDataGrid;

public partial class DynamicDataGrid
{
    public static readonly DependencyProperty TableDataProperty =
        DependencyProperty.Register(
            nameof(TableData),
            typeof(Table),
            typeof(DynamicDataGrid),
            new PropertyMetadata(null, OnTableDataChanged));

    public static readonly DependencyProperty SelfDescribingDataProperty =
        DependencyProperty.Register(
            nameof(SelfDescribingData),
            typeof(IEnumerable),
            typeof(DynamicDataGrid),
            new PropertyMetadata(null, OnSelfDescribingDataChanged));


    public DynamicDataGrid()
    {
        InitializeComponent();
        ShowEmptyState(true);
    }

    public IEnumerable? SelfDescribingData
    {
        get => (IEnumerable)GetValue(SelfDescribingDataProperty);
        set => SetValue(SelfDescribingDataProperty, value);
    }

    public Table? TableData
    {
        get => (Table)GetValue(TableDataProperty);
        set => SetValue(TableDataProperty, value);
    }

    /// <summary>
    ///     Helper to get property values via reflection
    /// </summary>
    private static object? GetPropertyValue(object obj, string propertyName)
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
            control.RebuildDataGridFromTable();
        }
    }

    private static void OnSelfDescribingDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DynamicDataGrid control)
        {
            return;
        }

        control.RebuildDataGridFromSelfDescribingData();
    }

    private void RebuildDataGridFromSelfDescribingData()
    {
        try
        {
            ClearColumns();

            var items = SelfDescribingData?.OfType<object>().ToArray();
            if (items is null || items.Length == 0)
            {
                ShowEmptyState(true);
                return;
            }

            if (!GenerateColumnsFromAttributes(items))
            {
                ShowEmptyState(true);
                return;
            }

            // Bind data
            MainDataGrid.ItemsSource = SelfDescribingData;

            ShowEmptyState(!items.Any());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error rebuilding DataGrid: {ex.Message}");
            ShowEmptyState(true, Strings.DynamicGrid_LoadError);
        }
    }

    private void RebuildDataGridFromTable()
    {
        try
        {
            ClearColumns();

            if (TableData == null)
            {
                ShowEmptyState(true);
                return;
            }

            if (GenerateColumnsFromTable())
            {
                ShowEmptyState(true);
                return;
            }

            // Bind data
            var data = TableData.GetData();
            MainDataGrid.ItemsSource = data;

            ShowEmptyState(!data.Any());
            SetupRowDetailsFromTable();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error rebuilding DataGrid: {ex.Message}");
            ShowEmptyState(true, Strings.DynamicGrid_LoadError);
        }
    }

    private bool GenerateColumnsFromTable()
    {
        // Create columns
        var columns = TableData?.GetColumns().ToArray();
        if (columns is null || !columns.Any())
        {
            ShowEmptyState(true);
            return true;
        }

        foreach (var columnDef in columns)
        {
            var column = CreateDataGridColumn(columnDef);
            MainDataGrid.Columns.Add(column);
        }

        return false;
    }

    private void ClearColumns()
    {
        MainDataGrid.Columns.Clear();
        MainDataGrid.ItemsSource = null;
    }

    private void SetupRowDetailsFromTable()
    {
        // If given, set row details template
        var rowDetailsTemplate = TableData?.GetRowDetailsTemplate();
        if (rowDetailsTemplate != null)
        {
            MainDataGrid.RowDetailsTemplate = rowDetailsTemplate;

            // Default value is collapsed
            MainDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
        }
    }

    private DataGridColumn CreateDataGridColumn(TableColumnDefinition columnDef)
    {
        if (columnDef.IsExpandable)
        {
            // Limitation. We only show the text if this is an expandable column.
            Debug.Assert(columnDef.Type == ColumnType.Text);
            return CreateExpandableColumn(columnDef);
        }

        return columnDef.Type switch
        {
            ColumnType.Text => CreateTextColumn(columnDef),
            ColumnType.Link => CreateLinkColumn(columnDef),
            ColumnType.Icon => CreateIconColumn(columnDef),
            ColumnType.Toggle => CreateToggleColumn(columnDef),
            _ => CreateTextColumn(columnDef)
        };
    }

    /// <summary>
    ///     Creates an expandable column.
    /// </summary>
    private static DataGridTemplateColumn CreateExpandableColumn(TableColumnDefinition columnDef)
    {
        var column = new DataGridTemplateColumn
        {
            Header = columnDef.Header,
            Width = columnDef.Width == 0 ? DataGridLength.Auto : new DataGridLength(columnDef.Width)
        };


        // Template with DockPanel and ToggleButton 
        // An expandable colum can only be text.
        var xamlTemplate = $$"""


                             <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                             xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
                                 <DockPanel LastChildFill='True'>
                                     
                                     <DockPanel.Resources>
                                         <ResourceDictionary>
                                             <ResourceDictionary.MergedDictionaries>
                                                 <ResourceDictionary Source="/Styles/ButtonStyles.xaml" />
                                                 <ResourceDictionary Source="/Styles/DataGridStyles.xaml" />
                                             </ResourceDictionary.MergedDictionaries>
                                         </ResourceDictionary>
                                     </DockPanel.Resources>
                                                   
                                     <ToggleButton DockPanel.Dock="Left"
                                                 Style="{StaticResource ExpandCollapseButtonStyle}"
                                                 IsChecked="{Binding IsExpanded, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                                 Margin="0,0,5,0" />

                                     <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                                         <TextBlock Text='{Binding {{columnDef.PropertyName}}}' />
                                     </StackPanel>
                                 </DockPanel>
                             </DataTemplate>
                             """;

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

    private static DataGridTextColumn CreateTextColumn(TableColumnDefinition columnDef)
    {
        var column = new DataGridTextColumn
        {
            Header = columnDef.Header,
            Binding = new Binding(columnDef.PropertyName),
            Width = columnDef.Width == 0 ? DataGridLength.Auto : new DataGridLength(columnDef.Width)
        };

        if (columnDef.SortMemberName != null)
        {
            column.SortMemberPath = columnDef.SortMemberName;
        }

        return column;
    }

    private DataGridTemplateColumn CreateLinkColumn(TableColumnDefinition columnDef)
    {
        var column = new DataGridTemplateColumn
        {
            Header = columnDef.Header,
            Width = columnDef.Width == 0 ? DataGridLength.Auto : new DataGridLength(columnDef.Width)
        };

        var cellTemplate = new DataTemplate();

        var buttonFactory = new FrameworkElementFactory(typeof(Button));
        buttonFactory.SetValue(ContentProperty, new Binding(columnDef.PropertyName));
        buttonFactory.SetValue(ForegroundProperty, Brushes.Blue);
        buttonFactory.SetValue(CursorProperty, Cursors.Hand);
        buttonFactory.SetValue(BorderThicknessProperty, new Thickness(0));
        buttonFactory.SetValue(BackgroundProperty, Brushes.Transparent);
        buttonFactory.SetValue(HorizontalContentAlignmentProperty, HorizontalAlignment.Left);
        buttonFactory.SetValue(PaddingProperty, new Thickness(0));

        // Style for underline
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(TemplateProperty, CreateLinkButtonTemplate()));
        buttonFactory.SetValue(StyleProperty, style);

        if (columnDef.ClickCommand != null)
        {
            buttonFactory.SetValue(ButtonBase.CommandProperty, columnDef.ClickCommand);
            buttonFactory.SetValue(ButtonBase.CommandParameterProperty, new Binding(columnDef.PropertyName));
        }

        cellTemplate.VisualTree = buttonFactory;
        column.CellTemplate = cellTemplate;

        return column;
    }

    private static ControlTemplate CreateLinkButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
        textBlockFactory.SetValue(TextBlock.TextProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
        textBlockFactory.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
        textBlockFactory.SetValue(TextBlock.ForegroundProperty, Brushes.Blue);
        template.VisualTree = textBlockFactory;
        return template;
    }

    private static DataGridTemplateColumn CreateIconColumn(TableColumnDefinition columnDef)
    {
        var column = new DataGridTemplateColumn
        {
            Header = columnDef.Header,
            Width = columnDef.Width == 0 ? new DataGridLength(20) : new DataGridLength(columnDef.Width)
        };

        var cellTemplate = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(Image));
        factory.SetValue(Image.SourceProperty, new Binding(columnDef.PropertyName));
        factory.SetValue(HeightProperty, 16.0);
        factory.SetValue(WidthProperty, 16.0);
        factory.SetValue(Image.StretchProperty, Stretch.None);

        cellTemplate.VisualTree = factory;
        column.CellTemplate = cellTemplate;

        return column;
    }

    private static DataGridTemplateColumn CreateToggleColumn(TableColumnDefinition columnDef)
    {
        var column = new DataGridTemplateColumn
        {
            Header = columnDef.Header,
            Width = columnDef.Width == 0 ? new DataGridLength(100) : new DataGridLength(columnDef.Width)
        };

        var cellTemplate = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(ToggleButton));
        factory.SetBinding(ToggleButton.IsCheckedProperty, new Binding(columnDef.PropertyName));

        if (columnDef.ClickCommand != null)
        {
            factory.SetValue(ButtonBase.CommandProperty, columnDef.ClickCommand);
            factory.SetValue(ButtonBase.CommandParameterProperty, new Binding()); // whole data context
        }

        cellTemplate.VisualTree = factory;
        column.CellTemplate = cellTemplate;

        return column;
    }

    private void RowOnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Dynamically fill context menu on data grid row.
        if (sender is not DataGridRow row || row.ContextMenu is null)
        {
            e.Handled = true;
            return;
        }

        row.IsSelected = true;

        row.ContextMenu.Items.Clear();

        // Only Table may provide commands.
        var commands = TableData?.GetCommands() ?? [];
        if (commands.Count == 0)
        {
            e.Handled = true;
            return;
        }

        var dataContext = row.DataContext;
        foreach (var command in commands)
        {
            row.ContextMenu.Items.Add(new MenuItem { Header = command.Header, Command = command.Command, CommandParameter = dataContext });
        }

        row.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    ///     Updates RowDetails Visibility based on IsExpanded Property (TableRow)
    /// </summary>
    private void UpdateRowDetailsVisibility(DataGridRow row)
    {
        if (row.DataContext != null)
        {
            var isExpandedValue = GetPropertyValue(row.DataContext, nameof(TableRow.IsExpanded));
            if (isExpandedValue is bool isExpanded)
            {
                row.DetailsVisibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void ShowEmptyState(bool show, string? message = null)
    {
        if (message == null)
        {
            message = Strings.DynamicGrid_NoData;
        }


        EmptyStateText.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Text = message;
        MainDataGrid.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }


    private bool GenerateColumnsFromAttributes(object[] items)
    {
        var properties = items[0].GetType().GetProperties();

        foreach (var prop in properties)
        {
            var ignoreAttr = prop.GetCustomAttribute<IgnoreColumnAttribute>();
            if (ignoreAttr != null)
            {
                continue;
            }

            var displayAttr = prop.GetCustomAttribute<DisplayColumnAttribute>();

            var column = new DataGridTextColumn
            {
                Header = displayAttr?.Header ?? prop.Name,

                // We could use this to format the output
                //Binding = new Binding(prop.Name)
                //{
                //    StringFormat = displayAttr?.Format
                //},

                Binding = new Binding(prop.Name),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto)
            };

            // Special formatting for certain types
            if (prop.PropertyType == typeof(DateTime))
            {
                column.Binding.StringFormat = "yyyy-MM-dd";
            }
            else if (prop.PropertyType == typeof(decimal))
            {
                column.Binding.StringFormat = "C2";
            }

            MainDataGrid.Columns.Add(column);
        }

        // Column created?
        return MainDataGrid.Columns.Count > 0;
    }

    /// <summary>
    ///     Event handler for LoadingRow
    ///     - sets DataGridRow.DetailsVisibility based auf IsExpanded
    ///     - Registers handler for context menu opening
    /// </summary>
    private void MainDataGrid_OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        // Only Table may provide row details.
        if (e.Row.DataContext is INotifyPropertyChanged viewModel && TableData != null)
        {
            UpdateRowDetailsVisibility(e.Row);

            // PropertyChanged to react to IsExpanded. It did not work when set in the DataGridRow style.
            // It was fine when the DataGrid was not dynamically created.
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(TableRow.IsExpanded))
                {
                    UpdateRowDetailsVisibility(e.Row);
                }
            };
        }

        e.Row.ContextMenuOpening += RowOnContextMenuOpening;
    }
    
}