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
            xamlHost.Child = new DesktopIcons();
        }
    }
}
