using Mile.Xaml;
using ExplorerRevolution;
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
    }
}
