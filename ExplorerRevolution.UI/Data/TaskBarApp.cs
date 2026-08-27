using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace ExplorerRevolution.Data
{
    /// <summary>
    /// 任务栏按钮(按应用分组):一个应用(固定或运行中)对应一个按钮。
    /// 同一应用的多个窗口合并到一个按钮;固定应用启动后与固定图标合并,不再出现两个图标。
    /// </summary>
    public class TaskBarApp : INotifyPropertyChanged
    {
        /// <summary>分组主键(AUMID 或 exe 路径)。</summary>
        public string Key { get; set; }

        public bool IsPinned { get; set; }

        /// <summary>固定图标的 .lnk 路径(未固定为空)。</summary>
        public string ShortcutPath { get; set; }

        /// <summary>MSIX/UWP 应用的 AppUserModelId(为空表示 Win32)。</summary>
        public string AppUserModelId { get; set; }

        private string _title;
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

        private bool _isForeground;
        public bool IsForeground
        {
            get => _isForeground;
            set => SetProperty(ref _isForeground, value);
        }

        private Visibility _isActive = Visibility.Collapsed;
        /// <summary>运行中 → 下划线指示条可见。</summary>
        public Visibility IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private Visibility _buttonTitleVisibility = Visibility.Collapsed;
        public Visibility ButtonTitleVisibility
        {
            get => _buttonTitleVisibility;
            set => SetProperty(ref _buttonTitleVisibility, value);
        }

        /// <summary>该应用当前打开的窗口(未运行时为空)。</summary>
        public List<TaskBarIcon> Windows { get; } = new List<TaskBarIcon>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
