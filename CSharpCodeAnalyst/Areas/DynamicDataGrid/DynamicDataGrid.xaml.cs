using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.TabularData;

namespace CSharpCodeAnalyst.Areas.DynamicDataGrid;

public partial class DynamicDataGrid : UserControl
{
    public static readonly DependencyProperty TableDataProperty =
        DependencyProperty.Register(
            nameof(TableData),
            typeof(Table),
            typeof(DynamicDataGrid),
            new PropertyMetadata(null, OnTableDataChanged));



    public DynamicDataGrid()
    {
        InitializeComponent();
        ShowEmptyState(true);
    }

    public Table? TableData
    {
        get => (Table)GetValue(TableDataProperty);
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
            // Clear DataGrid
            MainDataGrid.Columns.Clear();
            MainDataGrid.ItemsSource = null;

            if (TableData == null)
            {
                ShowEmptyState(true);
                return;
            }

            // Create columns
            var columns = TableData.GetColumns().ToArray();
            if (!columns.Any())
            {
                ShowEmptyState(true);
                return;
            }

            foreach (var columnDef in columns)
            {
                var column = CreateDataGridColumn(columnDef);
                MainDataGrid.Columns.Add(column);
            }

            // Bind data
            var data = TableData.GetData();
            MainDataGrid.ItemsSource = data;

            ShowEmptyState(!data.Any());

            // If given, set row details template
            if (TableData.GetRowDetailsTemplate() != null)
            {
                MainDataGrid.RowDetailsTemplate = TableData.GetRowDetailsTemplate();

                // Default value is collapsed
                MainDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;

                // Event Handler to manage IsExpanded per row.
                MainDataGrid.LoadingRow += OnDataGridLoadingRow;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error rebuilding DataGrid: {ex.Message}");
            ShowEmptyState(true, Strings.DynamicGrid_LoadError);
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
    private DataGridTemplateColumn CreateExpandableColumn(TableColumnDefinition columnDef)
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

    private DataGridTextColumn CreateTextColumn(TableColumnDefinition columnDef)
    {
        var column = new DataGridTextColumn
        {
            Header = columnDef.Header,
            Binding = new Binding(columnDef.PropertyName),
            Width = columnDef.Width == 0 ? DataGridLength.Auto : new DataGridLength(columnDef.Width)
        };

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

    private DataGridTemplateColumn CreateIconColumn(TableColumnDefinition columnDef)
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

    private DataGridTemplateColumn CreateToggleColumn(TableColumnDefinition columnDef)
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


    /// <summary>
    ///     Event handler for LoadingRow
    ///     - sets DataGridRow.DetailsVisibility based auf IsExpanded
    ///     - Registers handler for context menu opening
    /// </summary>
    private void OnDataGridLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is INotifyPropertyChanged viewModel)
        {
            UpdateRowDetailsVisibility(e.Row);

            // PropertyChanged to react to IsExpanded. It did not work when set in the DataGridRow style.
            // It was fine when the DataGrid was not dynamically created.
            viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(TableRow.IsExpanded))
                {
                    UpdateRowDetailsVisibility(e.Row);
                }
            };


            e.Row.ContextMenuOpening += RowOnContextMenuOpening;
        }
    }

    private void RowOnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Dynamically fill context menu on data grid row.
        if (sender is not DataGridRow row || row.ContextMenu is null)
        {
            e.Handled = true;
            return;
        }

        row.ContextMenu.Items.Clear();

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
}