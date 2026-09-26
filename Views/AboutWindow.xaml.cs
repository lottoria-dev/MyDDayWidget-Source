using System.Windows;
using System.Windows.Input;

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

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
