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
    public class TargetMachineLogicClass
    {


        public async Task CheckTargetConnection(string CS)
        {
            try
            {
                using NpgsqlConnection conn = new NpgsqlConnection(CS);
                await conn.OpenAsync();   // try to open connection
                AppGlobals.IsTargetConnected = true;
                await AppGlobals.SetTargetConnectionString(CS);

            }
            catch (Exception ex) { AppGlobals.IsTargetConnected = false; }
        }

        internal async Task<List<ComboBoxItem>> GetAllDatabases(string TargetConnectionString)
        {
            try
            {
                using NpgsqlConnection conn = new NpgsqlConnection(TargetConnectionString);
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
            catch (Exception ex)
            {
                MessageBox.Show("Try Again or reconnect then try your operation.");
                return null;

            }
        }



        public async Task<bool> GenerateDatabaseSnapshot(string DbName, string tempfilePath)
        {
            try
            {
                var pgDumpPath = "pg_dump"; // Or full path if not in environment PATH



                var args = $"-h {AppGlobals.TargetHost} -p {AppGlobals.TargetPort} -U {AppGlobals.TargetUsername} -d {DbName} --schema-only -f \"{tempfilePath}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = args,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Environment = { ["PGPASSWORD"] = AppGlobals.TargetPassword } // pass password safely
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



        // get temp file path
        public async Task<string> GetTempFilePath()
        {
            string tempFileName = Path.GetTempFileName();
            string tempFilePath = Path.ChangeExtension(tempFileName, ".sql");
            File.Move(tempFileName, tempFilePath); // Rename to .sql
            return tempFilePath;



        }
    }
}
