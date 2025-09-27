using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StructSync.Helpers
{
    public static class AppGlobals
    {
        public static string SourceConnectionString { get; set; }
        public static string SourceHost { get; set; }
        public static string SourcePort { get; set; }
        public static string SourceDatabaseName { get; set; }
        public static string SourceUsername { get; set; }
        public static string SourcePassword { get; set; }
        public static bool IsSourceConnected { get; set; } = false;




        public static string TargetConnectionString { get; set; }
        public static string TargetHost { get; set; }
        public static string TargetPort { get; set; }
        public static string TargetDatabaseName { get; set; }
        public static string TargetUsername { get; set; }
        public static string TargetPassword { get; set; }
        public static bool IsTargetConnected { get; set; } = false;



        public static async Task SetSourceConnectionString(string cs)
        {
            SourceConnectionString = cs;
            SpearteSourceConnectionString();
        }
        public static async Task SetTargetConnectionString(string cs)
        {
            TargetConnectionString = cs;
            SpearteTargetConnectionString();
        }

        private static async Task SpearteSourceConnectionString()
        {
            var CSChunck = SourceConnectionString.Split(";");
            foreach (var part in CSChunck)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim().ToLower();
                    var value = keyValue[1].Trim();
                    switch (key)
                    {
                        case "host":
                            SourceHost = value;
                            break;
                        case "port":
                            SourcePort = value;
                            break;
                        case "database":
                            SourceDatabaseName = value;
                            break;
                        case "username":
                            SourceUsername = value;
                            break;
                        case "password":
                            SourcePassword = value;
                            break;
                    }
                }
            }

        }

        private static async Task SpearteTargetConnectionString()
        {
            var CSChunck = TargetConnectionString.Split(";");
            foreach (var part in CSChunck)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim().ToLower();
                    var value = keyValue[1].Trim();
                    switch (key)
                    {
                        case "host":
                            TargetHost = value;
                            break;
                        case "port":
                            TargetPort = value;
                            break;
                        case "database":
                            TargetDatabaseName = value;
                            break;
                        case "username":
                            TargetUsername = value;
                            break;
                        case "password":
                            TargetPassword = value;
                            break;
                    }
                }
            }

        }
    }
}
