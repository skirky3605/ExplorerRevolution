using ExplorerRevolution;
using ExplorerRevolution.UI;
using Mile.Xaml;
using System;
using System.Windows.Forms;
using Windows.UI.Xaml;

namespace ExplorerRevolution
{
    public partial class MainForm : Form
    {
        WindowsXamlHost xamlHost = new WindowsXamlHost();

        public MainForm()
        {
            InitializeComponent();
            this.Controls.Add(xamlHost);
            xamlHost.AutoSize = true;
            xamlHost.Dock = DockStyle.Fill;
            xamlHost.Child = new MainPage();
        }
        protected override void WndProc(ref Message m)
        {
            const int WM_ACTIVATE = 0x0006;

            if (m.Msg == WM_ACTIVATE)
            {
                // 强制忽略 inactive 状态
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
