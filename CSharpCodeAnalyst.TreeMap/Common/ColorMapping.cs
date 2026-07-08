using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CSharpCodeAnalyst.TreeMap.Common
{
    public class ColorMapping : INotifyPropertyChanged
    {
        public required string Name
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public required Color Color
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}