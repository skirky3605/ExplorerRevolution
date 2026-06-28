using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace ExplorerRevolution.Data
{
    class TaskBarIcon
    {
        public string Title;
        public BitmapSource Icon;
        //public int status;
        public Visibility IsActive;
        public Visibility ButtonTitleVisibility;
        public bool IsForeground; 
    }
}
