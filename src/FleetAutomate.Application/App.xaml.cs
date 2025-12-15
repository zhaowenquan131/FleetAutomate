using System.Configuration;
using System.Data;
using System.Windows;
using NLog;

namespace Canvas.TestRunner
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configure NLog
            var config = new NLog.Config.LoggingConfiguration();

            // Load configuration from NLog.config file
            LogManager.Setup().LoadConfigurationFromFile("NLog.config");

            Logger.Info("FleetAutomate application started");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info("FleetAutomate application exiting");
            LogManager.Shutdown();
            base.OnExit(e);
        }
    }

}
