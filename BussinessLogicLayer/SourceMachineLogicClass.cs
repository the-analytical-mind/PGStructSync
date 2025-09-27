using Npgsql;
using StructSync.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace StructSync.BussinessLogicLayer
{
    public class SourceMachineLogicClass
    {


        public async Task CheckSourceConnection(string CS)
        {
            try
            {
                 using NpgsqlConnection conn = new NpgsqlConnection(CS);
                await conn.OpenAsync();   // try to open connection
                AppGlobals.IsSourceConnected= true;
                await AppGlobals.SetSourceConnectionString(CS);

            }
            catch (Exception ex) { AppGlobals.IsSourceConnected= false; }
        }

        internal async Task<List<ComboBoxItem>> GetAllDatabases(string SourceConnectionString)
        {
            try
            {
                using NpgsqlConnection conn = new NpgsqlConnection(SourceConnectionString);
                await conn.OpenAsync();

                List<ComboBoxItem> dbList = new List<ComboBoxItem>();
                using (var cmd = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datistemplate = false and datname<>'postgres';", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dbList.Add(new ComboBoxItem { Content = reader["datname"].ToString(), Tag = reader["datname"].ToString() });
                    }
                }
                return dbList;
            }
            catch(Exception ex)
            {
                MessageBox.Show("Try Again or reconnect then try your operation.");
                return null;
               
            }
        }

        // separate source connection string to database , server, port, username, password
        public (string server, string port, string database, string username, string password) SeparateConnectionString(string CS)
        {
            var parts = CS.Split(';');
            string server = "", port = "", database = "", username = "", password = "";
            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim().ToLower();
                    var value = keyValue[1].Trim();
                    switch (key)
                    {
                        case "host":
                            server = value;
                            break;
                        case "port":
                            port = value;
                            break;
                        case "database":
                            database = value;
                            break;
                        case "username":
                            username = value;
                            break;
                        case "password":
                            password = value;
                            break;
                    }
                }
            }
            return (server, port, database, username, password);
        }

        public async Task<bool> GenerateDatabaseSnapshot(string snapshotFilePath)
        {
            try
            {
                var pgDumpPath = "pg_dump"; // Or full path if not in environment PATH
              
               

                var args = $"-h {AppGlobals.SourceHost} -p {AppGlobals.SourcePort} -U {AppGlobals.SourceUsername} -d {AppGlobals.SourceDatabaseName} --schema-only -f \"{snapshotFilePath}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = args,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Environment = { ["PGPASSWORD"] = AppGlobals.SourcePassword } // pass password safely
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    string errorOutput = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"pg_dump failed with error: {errorOutput}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log or handle error
                MessageBox.Show($"Error generating SQL dump: {ex.Message}");
                return false;
            }
        }
    }
}
