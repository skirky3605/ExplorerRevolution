using System;

namespace ExplorerRevolution.Common
{
    public class TaskbarWindow
    {
        public IntPtr Hwnd { get; set; }

        public bool IsApplicationView { get; set; }

        public IApplicationView ApplicationView { get; set; }

        public string Title { get; set; }


        public override int GetHashCode()
        {
            return Hwnd.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is TaskbarWindow w &&
                   w.Hwnd == Hwnd;
        }
    }
}