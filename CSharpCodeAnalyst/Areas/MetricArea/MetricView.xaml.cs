using CSharpCodeAnalyst.Shared.Contracts;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CSharpCodeAnalyst.Areas.MetricArea;

public partial class MetricView
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(ObservableCollection<IMetric>), typeof(MetricView),
            new PropertyMetadata(null, OnDataChanged));

    public MetricView()
    {
        InitializeComponent();
        GenerateColumns();
        GenerateRows();
    }

    public ObservableCollection<IMetric>? Data
    {
        get => (ObservableCollection<IMetric>)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MetricView control)
        {
            control.OnDataChanged(e);
        }
    }

    private void OnDataChanged(DependencyPropertyChangedEventArgs _)
    {
        GenerateColumns();
        GenerateRows();
    }

    private void GenerateRows()
    {
        DynamicDataGrid.ItemsSource = Data;
    }

    private void GenerateColumns()
    {
        DynamicDataGrid.Columns.Clear();

        if (Data is null)
        {
            return;
        }

        DynamicDataGrid.Columns.Clear();

        if (Data.Count > 0)
        {
            var properties = Data[0].GetType().GetProperties();

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

                DynamicDataGrid.Columns.Add(column);
            }
        }
    }
}