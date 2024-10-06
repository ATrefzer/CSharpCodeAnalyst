using Contracts.Colors;
using Contracts.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CSharpCodeAnalyst.Help
{
    public partial class LegendDialog : Window
    {
        public Brush NamespaceColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Namespace)));
        public Brush ClassColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Class)));
        public Brush InterfaceColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Interface)));
        public Brush StructColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Struct)));
        public Brush EnumColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Enum)));
        public Brush MethodColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Method)));
        public Brush PropertyColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Property)));
        public Brush FieldColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Field)));
        public Brush EventColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Event)));
        public Brush DelegateColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Delegate)));
        public Brush RecordColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Record)));
        public Brush AssemblyColor { get; } = new SolidColorBrush(GetColorFromRgb(ColorDefinitions.GetRbgOf(CodeElementType.Assembly)));

        public LegendDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static Color GetColorFromRgb(int rgb)
        {
            return Color.FromRgb(
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF)
            );
        }
    }
}
