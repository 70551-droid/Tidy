using System.Windows;

namespace Tidy
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tidy is working!");
        }
    }
}
