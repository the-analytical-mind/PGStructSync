using StructSync.BussinessLogicLayer;
using StructSync.Helpers;
using StructSync.ViewModels;
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
            processsnapshot.IsEnabled = false;

            if (cmb_database.SelectedIndex != -1)
            {
                if (string.IsNullOrEmpty(snapshotfilepath.Text))
                {
                    MessageBox.Show("Please select a snapshot file.");
                    return;
                }
                var selectedDb = (ComboBoxItem)cmb_database.SelectedItem;
                string DbName = selectedDb.Tag.ToString();

                if (string.IsNullOrEmpty(DbName))
                    {
                    MessageBox.Show("Please select a database.");
                    return;
                }

                SnapshotParser Parser = new SnapshotParser();
                List<ScheamaDetailInfo> SourceResult = await Parser.ParseSnapshot(snapshotfilepath.Text);

                // Get Path of snapshot file
                string tempfilePath = await TargetLogic.GetTempFilePath();
                // Export target database schema to temp file
               bool success = await TargetLogic.GenerateDatabaseSnapshot( DbName, tempfilePath);
                if (success)
                {
                    List<ScheamaDetailInfo> TargetResult = await Parser.ParseSnapshot(tempfilePath);
                    // Compare SourceResult and TargetResult to set Status
                    SchemaComparer comparer = new SchemaComparer();
                    await comparer.CompareSchemas(SourceResult, TargetResult);
                }

                // display in nested form inside grd_displayresult (default extended: expanded)
                DisplayParseResult(SourceResult);

                processsnapshot.IsEnabled = true;

            }
            else
            {
                MessageBox.Show("Please select a database.");
                processsnapshot.IsEnabled = true;
                return;
            }
        }

        // Renders the parsed snapshot into a nested TreeView placed in grd_displayresult.
        // The hierarchy: Schema -> Tables/Views/Functions/Procedures -> Table -> Columns/Indexes/ForeignKeys -> Query nodes
        // Uses SchemaStatus to color headers:
        // Matched => Green, NotExist => Red, Modified => Yellow
        // Default extended form: nodes are expanded by default for easy browsing.
        private void DisplayParseResult(List<ScheamaDetailInfo> result)
        {
            grd_displayresult.Children.Clear();

            if (result == null || result.Count == 0)
            {
                var tb = new TextBlock
                {
                    Text = "No objects parsed from snapshot.",
                    Margin = new Thickness(8),
                    TextWrapping = TextWrapping.Wrap
                };
                grd_displayresult.Children.Add(tb);
                return;
            }

            var tree = new TreeView
            {
                Margin = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // helper: create a header TextBlock with color based on status (nullable) and optional bold
            TextBlock CreateHeader(string text, SchemaStatus? status = null, bool bold = false)
            {
                var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
                if (status.HasValue)
                {
                    tb.Foreground = GetBrushForStatus(status.Value);
                }
                tb.FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal;
                return tb;
            }

            Brush GetBrushForStatus(SchemaStatus status)
            {
                return status switch
                {
                    SchemaStatus.Matched => Brushes.Green,
                    SchemaStatus.NotExist => Brushes.Red,
                    SchemaStatus.Modified => Brushes.Goldenrod, // yellowish
                    _ => Brushes.Black
                };
            }

            foreach (var schema in result)
            {
                var schemaItem = new TreeViewItem
                {
                    Header = CreateHeader($"Schema: {schema.ScheamaName}", schema?.Status, bold: true),
                    IsExpanded = true // expanded by default
                };

                // attach schema query (if present) to item Tag so clicking it shows the query in txt_querytool
                var schemaQuery = string.IsNullOrWhiteSpace(schema.Query) ? null : schema.Query.Trim();
                if (schemaQuery != null)
                {
                    schemaItem.Tag = schemaQuery;
                    schemaItem.Selected += TreeViewItem_Selected;
                }

                // Tables
                if (schema.Tables != null && schema.Tables.Count > 0)
                {
                    var tablesRoot = new TreeViewItem { Header = CreateHeader($"Tables ({schema.Tables.Count})", bold: true), IsExpanded = true };
                    foreach (var table in schema.Tables)
                    {
                        var tableItem = new TreeViewItem
                        {
                            Header = CreateHeader($"Table: {table.TableName}", table?.Status, bold: true),
                            IsExpanded = false
                        };

                        // attach table query
                        var tableQuery = string.IsNullOrWhiteSpace(table.Query) ? null : table.Query.Trim();
                        if (tableQuery != null)
                        {
                            tableItem.Tag = tableQuery;
                            tableItem.Selected += TreeViewItem_Selected;
                        }

                        // Columns
                        if (table.Columns != null && table.Columns.Count > 0)
                        {
                            var colsRoot = new TreeViewItem { Header = CreateHeader($"Columns ({table.Columns.Count})"), IsExpanded = false };
                            foreach (var col in table.Columns)
                            {
                                var colHeader = $"{col.ColumnName} : {col.DataType}{(col.IsPrimary ? " [PK]" : string.Empty)}";
                                var colItem = new TreeViewItem { Header = CreateHeader(colHeader, col?.Status), IsExpanded = false };

                                var colQuery = string.IsNullOrWhiteSpace(col.Query) ? null : col.Query.Trim();
                                if (colQuery != null)
                                {
                                    colItem.Tag = colQuery;
                                    colItem.Selected += TreeViewItem_Selected;
                                }

                                colsRoot.Items.Add(colItem);
                            }
                            tableItem.Items.Add(colsRoot);
                        }

                        // Indexes
                        if (table.Indexes != null && table.Indexes.Count > 0)
                        {
                            var idxRoot = new TreeViewItem { Header = CreateHeader($"Indexes ({table.Indexes.Count})"), IsExpanded = false };
                            foreach (var idx in table.Indexes)
                            {
                                var idxItem = new TreeViewItem { Header = CreateHeader(idx.IndexName, idx?.Status) , IsExpanded = false };

                                var idxQuery = string.IsNullOrWhiteSpace(idx.Query) ? null : idx.Query.Trim();
                                if (idxQuery != null)
                                {
                                    idxItem.Tag = idxQuery;
                                    idxItem.Selected += TreeViewItem_Selected;
                                }

                                idxRoot.Items.Add(idxItem);
                            }
                            tableItem.Items.Add(idxRoot);
                        }

                        // Foreign keys
                        if (table.ForeignKeys != null && table.ForeignKeys.Count > 0)
                        {
                            var fkRoot = new TreeViewItem { Header = CreateHeader($"Foreign Keys ({table.ForeignKeys.Count})"), IsExpanded = false };
                            foreach (var fk in table.ForeignKeys)
                            {
                                var fkText = fk ?? string.Empty;
                                var fkItem = new TreeViewItem { Header = CreateHeader(fkText) };
                                var fkQuery = string.IsNullOrWhiteSpace(fkText) ? null : fkText.Trim();
                                if (fkQuery != null)
                                {
                                    fkItem.Tag = fkQuery;
                                    fkItem.Selected += TreeViewItem_Selected;
                                }
                                fkRoot.Items.Add(fkItem);
                            }
                            tableItem.Items.Add(fkRoot);
                        }


                        tablesRoot.Items.Add(tableItem);
                    }
                    schemaItem.Items.Add(tablesRoot);
                }

                // Views
                if (schema.Views != null && schema.Views.Count > 0)
                {
                    var viewsRoot = new TreeViewItem { Header = CreateHeader($"Views ({schema.Views.Count})", bold: true), IsExpanded = true };
                    foreach (var v in schema.Views)
                    {
                        var vItem = new TreeViewItem { Header = CreateHeader($"View: {v.ViewName}", v?.Status), IsExpanded = false };

                        var vQuery = string.IsNullOrWhiteSpace(v.Query) ? null : v.Query.Trim();
                        if (vQuery != null)
                        {
                            vItem.Tag = vQuery;
                            vItem.Selected += TreeViewItem_Selected;
                        }

                        viewsRoot.Items.Add(vItem);
                    }
                    schemaItem.Items.Add(viewsRoot);
                }

                // Functions
                if (schema.Functions != null && schema.Functions.Count > 0)
                {
                    var funcsRoot = new TreeViewItem { Header = CreateHeader($"Functions ({schema.Functions.Count})", bold: true), IsExpanded = true };
                    foreach (var f in schema.Functions)
                    {
                        var fItem = new TreeViewItem { Header = CreateHeader($"Function: {f.FunctionName}", f?.Status), IsExpanded = false };

                        var fQuery = string.IsNullOrWhiteSpace(f.Query) ? null : f.Query.Trim();
                        if (fQuery != null)
                        {
                            fItem.Tag = fQuery;
                            fItem.Selected += TreeViewItem_Selected;
                        }

                        funcsRoot.Items.Add(fItem);
                    }
                    schemaItem.Items.Add(funcsRoot);
                }

                // Procedures
                if (schema.Procedures != null && schema.Procedures.Count > 0)
                {
                    var procsRoot = new TreeViewItem { Header = CreateHeader($"Procedures ({schema.Procedures.Count})", bold: true), IsExpanded = true };
                    foreach (var p in schema.Procedures)
                    {
                        var pItem = new TreeViewItem { Header = CreateHeader($"Procedure: {p.ProcedureName}", p?.Status), IsExpanded = false };

                        var pQuery = string.IsNullOrWhiteSpace(p.Query) ? null : p.Query.Trim();
                        if (pQuery != null)
                        {
                            pItem.Tag = pQuery;
                            pItem.Selected += TreeViewItem_Selected;
                        }

                        procsRoot.Items.Add(pItem);
                    }
                    schemaItem.Items.Add(procsRoot);
                }

                tree.Items.Add(schemaItem);
            }

            grd_displayresult.Children.Add(tree);
        }

        // When a TreeViewItem with an attached query is selected, show it in txt_querytool.
        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem tvi)
            {
                var query = tvi.Tag as string;
                try
                {
                    
                    // If txt_querytool exists in XAML, set its Text to the query. Clear if null.
                    if (this.FindName("txt_querytool") is TextBox txt)
                    {
                        txt.Text = query ?? string.Empty;
                        executequerybutton.IsEnabled = true;
                    }
                }
                finally
                {
                    // stop bubbling so parent selected handlers don't override child selection
                    e.Handled = true;
                }
            }
        }

        private async void executequerybutton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
