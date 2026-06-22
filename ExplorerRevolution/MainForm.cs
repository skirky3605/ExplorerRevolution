using Mile.Xaml;
using MileXamlBlankAppNetFrameworkModern;
using System;
using System.Windows.Forms;
using Windows.UI.Xaml;

namespace MileXamlBlankAppNetFramework
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

            this.Load += Form1_Load;

            //ContainerControl control = new ContainerControl();
            //this.Controls.Add(control);
            //control.Dock = DockStyle.Fill;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
