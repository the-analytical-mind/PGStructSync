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
                AppGlobals.IsTargetConnected= true;
                await AppGlobals.SetTargetConnectionString(CS);

            }
            catch (Exception ex) { AppGlobals.IsTargetConnected= false; }
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
            catch(Exception ex)
            {
                MessageBox.Show("Try Again or reconnect then try your operation.");
                return null;
               
            }
        }

        

      
    }
}
