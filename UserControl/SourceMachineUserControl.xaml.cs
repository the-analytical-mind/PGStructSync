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
    /// Interaction logic for SourceMachineUserControl.xaml
    /// </summary>
    public partial class SourceMachineUserControl : System.Windows.Controls.UserControl
    {
        SourceMachineLogicClass SourceLogic = new SourceMachineLogicClass();
        public SourceMachineUserControl()
        {
            InitializeComponent();
        }

        private async void btnconectsource_Click(object sender, RoutedEventArgs e)
        {
            

            string Server = servername.Text;
            string UserName = username.Text;
            string PassWord = password.Password;
            string Port = portnumber.Text;

            if(string.IsNullOrEmpty(Server)|| string.IsNullOrEmpty(UserName)|| string.IsNullOrEmpty(PassWord)|| string.IsNullOrEmpty(Port))
            {
                MessageBox.Show("Please fill all fields");
                return;
            }

            string CS = $"Host={Server}; Port={Port}; Database=postgres;Username={UserName};Password={PassWord};";

            await SourceLogic.CheckSourceConnection(CS);
            if (AppGlobals.IsSourceConnected)
            {
               
                sourcedblinkstatus.Content = "Connected";
                sourcedblinkstatus.Foreground = Brushes.Green;

               
            }
            else
            {
                sourcedblinkstatus.Content = "Not Connected";
                sourcedblinkstatus.Foreground = Brushes.Red;
                MessageBox.Show("Connection Failed. Please check your credentials and try again.");
                return;

            }
            await LoadAllDataBases();
        }

        private async Task LoadAllDataBases()
        {
            if (AppGlobals.IsSourceConnected)
            {
                
                List<ComboBoxItem> DbList = await SourceLogic.GetAllDatabases(AppGlobals.SourceConnectionString);
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

        private async void btn_generate_file_Click(object sender, RoutedEventArgs e)
        {
            if (AppGlobals.IsSourceConnected)
            {
                if (cmb_database.SelectedIndex != -1)
                {
                    ComboBoxItem selectedDb = (ComboBoxItem)cmb_database.SelectedItem;
                    string DatabaseName = selectedDb.Tag.ToString();
                    string SnapshotfilePath = sourcescheamafilepath.Text;
                    if (string.IsNullOrEmpty(SnapshotfilePath))
                        {
                        MessageBox.Show("Please select a file path to save the snapshot.");
                        return;
                    }

                    AppGlobals.SourceDatabaseName = DatabaseName;
                    bool isSuccess = await SourceLogic.GenerateDatabaseSnapshot(SnapshotfilePath);
                    if (isSuccess)
                    {
                        MessageBox.Show("Snapshot file generated successfully.");
                    }
                    else
                    {
                        MessageBox.Show("Failed to generate snapshot file.");
                    }
                }
                else
                {
                    MessageBox.Show("Please select a database.");
                    return;
                }
            }
        }

       

        // Generate .sql file for source database structure
        private void opefilemodal_Click(object sender, RoutedEventArgs e)
        {

            if (cmb_database.SelectedIndex != -1)
            { // Create a SaveFileDialog instance
                ComboBoxItem selectedDb = (ComboBoxItem)cmb_database.SelectedItem;
                string DatabaseName = selectedDb.Tag.ToString();
                string TimeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string DefaultFileName = $"{DatabaseName}_Snapshot_{TimeStamp}.sql";
                Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Select location to save Snapshot file",
                    Filter = "SQL Files (*.sql)|*.sql",
                    DefaultExt = "sql",
                    FileName = DefaultFileName, // Default file name
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                // Show the dialog and check if the user selected a location
                if (saveFileDialog.ShowDialog() == true)
                {
                    // Store the selected file path in the TextBox
                    sourcescheamafilepath.Text = saveFileDialog.FileName;

                    // Enable the "Generate File" button
                    btn_generate_file.IsEnabled = true;
                }
            }
            else
            {
                MessageBox.Show("Please select a database.");
                return;
            }

        }
    }
}
