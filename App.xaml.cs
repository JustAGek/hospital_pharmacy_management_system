using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;

namespace WpfApp1
{
    public partial class App : Application
    {
        public static IConfigurationRoot Configuration { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Load appsettings.json
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            base.OnStartup(e);
            // StartupUri launches LoginWindow automatically
        }
    }
}
