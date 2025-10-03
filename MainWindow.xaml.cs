using StructSync.UserControl;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StructSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void sourcemachine_Click(object sender, RoutedEventArgs e)
        {
            maingrid.Children.Clear();

            // Create an instance of your UserControl
            SourceMachineUserControl sourceMachineControl = new SourceMachineUserControl();

            // Add the UserControl to the grid
            maingrid.Children.Add(sourceMachineControl);

            sourcemachine.Background = Brushes.LightBlue;

            targetmachine.Background = Brushes.LightGray;
        }

        private void targetmachine_Click(object sender, RoutedEventArgs e)
        {
            maingrid.Children.Clear();
            TargetMachineUserControl targetMachineControl = new TargetMachineUserControl();
            maingrid.Children.Add(targetMachineControl);
            sourcemachine.Background = Brushes.LightGray;

            targetmachine.Background = Brushes.LightBlue;

        }
    }
}