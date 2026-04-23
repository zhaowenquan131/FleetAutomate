using FleetAutomate.Application.Commanding;
using NLog;
using System.Windows;

namespace FleetAutomate
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private UiSessionHost? _uiSessionHost;
        private bool _uiSessionHostStartupAttempted;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            LogManager.Setup().LoadConfigurationFromFile("NLog.config");
            Logger.Info("FleetAutomate application started");
            Startup += OnApplicationStartupCompleted;
            Activated += OnApplicationActivated;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Startup -= OnApplicationStartupCompleted;
            Activated -= OnApplicationActivated;

            if (_uiSessionHost != null)
            {
                _uiSessionHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            Logger.Info("FleetAutomate application exiting");
            LogManager.Shutdown();
            base.OnExit(e);
        }

        private void OnApplicationStartupCompleted(object sender, StartupEventArgs e)
        {
            TryStartUiSessionHost();
        }

        private void OnApplicationActivated(object? sender, System.EventArgs e)
        {
            TryStartUiSessionHost();
        }

        private void TryStartUiSessionHost()
        {
            if (_uiSessionHost != null || _uiSessionHostStartupAttempted)
            {
                return;
            }

            if (Current?.MainWindow is not FleetAutomate.MainWindow mainWindow)
            {
                Logger.Warn("UI session host startup deferred because MainWindow is not ready yet.");
                return;
            }

            try
            {
                _uiSessionHostStartupAttempted = true;
                _uiSessionHost = new UiSessionHost(mainWindow.ViewModel);
                _uiSessionHost.Start();
                Logger.Info("UI session host started for process {0}", Environment.ProcessId);
            }
            catch (Exception ex)
            {
                _uiSessionHostStartupAttempted = false;
                _uiSessionHost = null;
                Logger.Error(ex, "Failed to start UI session host.");
            }
        }

        internal void EnsureUiSessionHost(FleetAutomate.MainWindow mainWindow)
        {
            if (_uiSessionHost != null)
            {
                return;
            }

            try
            {
                _uiSessionHostStartupAttempted = true;
                _uiSessionHost = new UiSessionHost(mainWindow.ViewModel);
                _uiSessionHost.Start();
                Logger.Info("UI session host started from MainWindow for process {0}", Environment.ProcessId);
            }
            catch (Exception ex)
            {
                _uiSessionHostStartupAttempted = false;
                _uiSessionHost = null;
                Logger.Error(ex, "Failed to start UI session host from MainWindow.");
            }
        }
    }
}
