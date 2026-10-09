using System.Windows;
using System.Windows.Controls;
using DDay3.Services;

namespace DDay3.Views
{
    public partial class AboutContent : UserControl
    {
        public AboutContent()
        {
            InitializeComponent();
            VersionText.Text = "Version " + App.Version;
        }
        private void Website_OnClick(object sender, RoutedEventArgs e)
        { StartupService.OpenUrl("https://mathtime.kr/dday.html"); }
    }
}
