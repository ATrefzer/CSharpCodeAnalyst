using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CSharpCodeAnalyst.TreeMap
{
    /// <summary>
    /// Interaction logic for ColorBrewer.xaml
    /// </summary>
    public sealed partial class ColorBrewer : UserControl
    {
        public ColorBrewer()
        {
            InitializeComponent();
            BrewedB = 30;
            BrewedR = 100;
            BrewedG = 80;
            BrewedColor = Color.FromRgb((byte) BrewedR, (byte) BrewedG, (byte) BrewedB);
        }

        // Using a DependencyProperty as the backing store for BrewedR.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BrewedRProperty =
                DependencyProperty.Register("BrewedR", typeof(int), typeof(ColorBrewer), new PropertyMetadata(0, OnColorRgbChanged));

        // Using a DependencyProperty as the backing store for BrewedR.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BrewedGProperty =
                DependencyProperty.Register("BrewedG", typeof(int), typeof(ColorBrewer), new PropertyMetadata(0, OnColorRgbChanged));

        // Using a DependencyProperty as the backing store for BrewedR.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BrewedBProperty =
                DependencyProperty.Register("BrewedB", typeof(int), typeof(ColorBrewer), new PropertyMetadata(0, OnColorRgbChanged));


        // Using a DependencyProperty as the backing store for MyColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BrewedColorProperty =
                DependencyProperty.Register("BrewedColor", typeof(Color), typeof(ColorBrewer), new PropertyMetadata(default(Color), OnColorChanged));



        public Color BrewedColor
        {
            get => (Color) GetValue(BrewedColorProperty);
            set => SetValue(BrewedColorProperty, value);
        }


        public int BrewedR
        {
            get => (int) GetValue(BrewedRProperty);
            set => SetValue(BrewedRProperty, value);
        }


        public int BrewedG
        {
            get => (int) GetValue(BrewedGProperty);
            set => SetValue(BrewedGProperty, value);
        }


        public int BrewedB
        {
            get => (int) GetValue(BrewedBProperty);
            set => SetValue(BrewedBProperty, value);
        }

        private static void OnColorRgbChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorBrewer brewer)
            {
                brewer.BrewedColor = Color.FromRgb((byte) brewer.BrewedR, (byte) brewer.BrewedG, (byte) brewer.BrewedB);
            }
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorBrewer brewer)
            {
                brewer.BrewedR = brewer.BrewedColor.R;
                brewer.BrewedG = brewer.BrewedColor.G;
                brewer.BrewedB = brewer.BrewedColor.B;
            }
        }


        private void PreviewRgbText(object sender, TextCompositionEventArgs e)
        {
            // Reject non numeric characters. Byte range may still be exceeded.
            if (ValidateInput(e.Text))
            {
                return;
            }

            e.Handled = true;
        }

        private bool ValidateInput(string text)
        {
            return byte.TryParse(text, out var value);
        }
    }
}