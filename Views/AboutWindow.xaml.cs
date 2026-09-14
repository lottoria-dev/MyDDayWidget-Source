using System.Windows;
using System.Windows.Input;
using DDay3.Services;

namespace DDay3.Views
{
    public partial class AboutWindow : Window
    {
        internal AboutWindow()
        {
            InitializeComponent();
            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
            {
                if (args.LeftButton == MouseButtonState.Pressed) DragMove();
            };
        }

        private void Website_OnClick(object sender, RoutedEventArgs e)
        {
            StartupService.OpenUrl("https://mathtime.kr/?page=dday");
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
