using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace ExplorerRevolution.Data
{

    public class TaskBarIcon : INotifyPropertyChanged
    {
        private IntPtr _intPtr;
        public IntPtr IntPtr
        {
            get => _intPtr;
            set => SetProperty(ref _intPtr, value);
        }

        private
     string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private BitmapSource _icon;
        public BitmapSource Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private Visibility _isActive;
        public Visibility IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private Visibility _buttonTitleVisibility;
        public Visibility ButtonTitleVisibility
        {
            get => _buttonTitleVisibility;
            set => SetProperty(ref _buttonTitleVisibility, value);
        }

        private bool _isForeground;
        public bool IsForeground
        {
            get => _isForeground;
            set => SetProperty(ref _isForeground, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}