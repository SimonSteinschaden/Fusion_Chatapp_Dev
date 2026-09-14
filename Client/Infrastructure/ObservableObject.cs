using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_Fusion.Infrastructure
{
    /// <summary>
    /// Base class implementing INotifyPropertyChanged for MVVM property change notification.
    /// </summary>
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }
    }
}