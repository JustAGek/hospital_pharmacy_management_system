using System;
using System.IO;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;

namespace WpfApp1
{
    public static class DbHelper
    {
        // lazy-load the IConfiguration
        private static IConfiguration _config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        public static MySqlConnection GetConnection()
        {
            // pull your named connection string
            var cs = _config.GetConnectionString("PharmacyDb");
            if (string.IsNullOrEmpty(cs))
                throw new InvalidOperationException("Connection string 'PharmacyDb' not found.");

            var conn = new MySqlConnection(cs);
            conn.Open();
            return conn;
        }
    }
}
