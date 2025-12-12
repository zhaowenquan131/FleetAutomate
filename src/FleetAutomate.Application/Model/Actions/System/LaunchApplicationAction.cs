using System.Diagnostics;
using System.Runtime.Serialization;
using System.ComponentModel;
using Canvas.TestRunner.Model.Flow;

namespace Canvas.TestRunner.Model.Actions.System
{
    /// <summary>
    /// Action to launch an application using a command or executable path.
    /// </summary>
    [DataContract]
    public class LaunchApplicationAction : IAction, INotifyPropertyChanged
    {
        public string Name => "Launch Application";

        [DataMember]
        public string Description { get; set; } = "Launch an application or execute a command";

        [DataMember]
        private ActionState _state = ActionState.Ready;

        public ActionState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
                }
            }
        }

        public bool IsEnabled => true;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// The executable path or command to run (e.g., "notepad.exe", "C:\Program Files\App\app.exe", "cmd /c dir")
        /// </summary>
        [DataMember]
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Command-line arguments to pass to the executable
        /// </summary>
        [DataMember]
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// Working directory for the process (optional)
        /// </summary>
        [DataMember]
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Whether to wait for the application to exit before completing the action
        /// </summary>
        [DataMember]
        public bool WaitForCompletion { get; set; } = false;

        /// <summary>
        /// Maximum time in milliseconds to wait for the application to exit (if WaitForCompletion is true)
        /// </summary>
        [DataMember]
        public int TimeoutMilliseconds { get; set; } = 30000;

        /// <summary>
        /// The process that was launched (not serialized)
        /// </summary>
        [IgnoreDataMember]
        private Process? _launchedProcess;

        public void Cancel()
        {
            // Kill the process if it's still running
            if (_launchedProcess != null && !_launchedProcess.HasExited)
            {
                try
                {
                    _launchedProcess.Kill();
                }
                catch
                {
                    // Process may have already exited
                }
            }
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            State = ActionState.Running;
            try
            {
                if (string.IsNullOrWhiteSpace(ExecutablePath))
                {
                    throw new InvalidOperationException("Executable path cannot be empty");
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    Arguments = Arguments ?? string.Empty,
                    UseShellExecute = false
                };

                // Set working directory if provided
                if (!string.IsNullOrWhiteSpace(WorkingDirectory))
                {
                    processInfo.WorkingDirectory = WorkingDirectory;
                }

                // Start the process
                _launchedProcess = Process.Start(processInfo);

                if (_launchedProcess == null)
                {
                    throw new InvalidOperationException($"Failed to start process: {ExecutablePath}");
                }

                // Wait for completion if requested
                if (WaitForCompletion)
                {
                    var exitedInTime = _launchedProcess.WaitForExit(TimeoutMilliseconds);

                    if (!exitedInTime)
                    {
                        // Timeout - kill the process
                        Cancel();
                        State = ActionState.Failed;
                        return false;
                    }

                    // Check exit code
                    if (_launchedProcess.ExitCode != 0)
                    {
                        State = ActionState.Failed;
                        return false;
                    }
                }

                State = ActionState.Completed;
                return true;
            }
            catch (OperationCanceledException)
            {
                Cancel();
                State = ActionState.Failed;
                return false;
            }
            catch (Exception ex)
            {
                State = ActionState.Failed;
                // Could log exception here if needed
                return false;
            }
        }
    }
}
