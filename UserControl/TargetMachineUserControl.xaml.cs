using StructSync.BussinessLogicLayer;
using StructSync.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StructSync.UserControl
{
    /// <summary>
    /// Interaction logic for TargetMachineUserControl.xaml
    /// </summary>
    public partial class TargetMachineUserControl : System.Windows.Controls.UserControl
    {
        TargetMachineLogicClass TargetLogic = new TargetMachineLogicClass();
        public TargetMachineUserControl()
        {
            InitializeComponent();
        }

        private async void btnconecttarget_Click(object sender, RoutedEventArgs e)
        {
            string Server = servername.Text;
            string UserName = username.Text;
            string PassWord = password.Password;
            string Port = portnumber.Text;

            if (string.IsNullOrEmpty(Server) || string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(PassWord) || string.IsNullOrEmpty(Port))
            {
                MessageBox.Show("Please fill all fields");
                return;
            }

            string CS = $"Host={Server}; Port={Port}; Database=postgres;Username={UserName};Password={PassWord};";

            await TargetLogic.CheckTargetConnection(CS);
            if (AppGlobals.IsTargetConnected)
            {

                targetdblinkstatus.Content = "Connected";
                targetdblinkstatus.Foreground = Brushes.Green;


            }
            else
            {
                targetdblinkstatus.Content = "Not Connected";
                targetdblinkstatus.Foreground = Brushes.Red;
                MessageBox.Show("Connection Failed. Please check your credentials and try again.");
                return;

            }
        }


        // open file dialog to select snapshot file
        private async void selectsnapshotfile_Click(object sender, RoutedEventArgs e)
        {

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "SQL files (*.sql)|*.sql|All files (*.*)|*.*";
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                string filePath = openFileDialog.FileName;
                snapshotfilepath.Text = filePath;

                await LoadAllDataBases();
            }
            else
            {
                snapshotfilepath.Text = "";
            }
        }

        private async Task LoadAllDataBases()
        {
            if (AppGlobals.IsTargetConnected)
            {

                List<ComboBoxItem> DbList = await TargetLogic.GetAllDatabases(AppGlobals.TargetConnectionString);
                if (DbList != null && DbList.Count > 0)
                {
                    cmb_database.ItemsSource = DbList;
                    cmb_database.IsEnabled = true;

                }
                else if (DbList != null && DbList.Count == 0)
                {
                    cmb_database.IsEnabled = false;
                    MessageBox.Show("No databases found on the server.");
                    return;
                }
            }


        }

        private async void processsnapshot_Click(object sender, RoutedEventArgs e)
        {
            if (cmb_database.SelectedIndex != -1)
            {
                if (string.IsNullOrEmpty(snapshotfilepath.Text))
                {
                    MessageBox.Show("Please select a snapshot file.");
                    return;
                }
                var selectedDb = (ComboBoxItem)cmb_database.SelectedItem;
                string DbName = selectedDb.Tag.ToString();

                SnapshotParser Parser = new SnapshotParser();
                var Result = await Parser.ParseSnapshot(snapshotfilepath.Text);


            }
            else
            {
                MessageBox.Show("Please select a database.");
                return;
            }
        }
    }
}
