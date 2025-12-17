using System.Diagnostics;
using System.Runtime.Serialization;
using System.ComponentModel;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Model.Actions.System
{
    /// <summary>
    /// Action to launch an application using a command or executable path.
    /// </summary>
    [DataContract]
    public class LaunchApplicationAction : IAction, INotifyPropertyChanged
    {
        public string Name => "Launch Application";

        [DataMember]
        private string _description = "Launch an application or execute a command";
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

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
        private string _executablePath = string.Empty;
        public string ExecutablePath
        {
            get => _executablePath;
            set
            {
                if (_executablePath != value)
                {
                    _executablePath = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExecutablePath)));
                }
            }
        }

        /// <summary>
        /// Command-line arguments to pass to the executable
        /// </summary>
        [DataMember]
        private string _arguments = string.Empty;
        public string Arguments
        {
            get => _arguments;
            set
            {
                if (_arguments != value)
                {
                    _arguments = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Arguments)));
                }
            }
        }

        /// <summary>
        /// Working directory for the process (optional)
        /// </summary>
        [DataMember]
        private string _workingDirectory = string.Empty;
        public string WorkingDirectory
        {
            get => _workingDirectory;
            set
            {
                if (_workingDirectory != value)
                {
                    _workingDirectory = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WorkingDirectory)));
                }
            }
        }

        /// <summary>
        /// Whether to wait for the application to exit before completing the action
        /// </summary>
        [DataMember]
        private bool _waitForCompletion = false;
        public bool WaitForCompletion
        {
            get => _waitForCompletion;
            set
            {
                if (_waitForCompletion != value)
                {
                    _waitForCompletion = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WaitForCompletion)));
                }
            }
        }

        /// <summary>
        /// Maximum time in milliseconds to wait for the application to exit (if WaitForCompletion is true)
        /// </summary>
        [DataMember]
        private int _timeoutMilliseconds = 30000;
        public int TimeoutMilliseconds
        {
            get => _timeoutMilliseconds;
            set
            {
                if (_timeoutMilliseconds != value)
                {
                    _timeoutMilliseconds = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeoutMilliseconds)));
                }
            }
        }

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
            // Yield to allow UI to update the action state immediately
            await Task.Yield();
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
