using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Actions.Logic;
using NLog;

namespace FleetAutomate.Model.Actions.System
{
    /// <summary>
    /// Log levels for LogAction
    /// </summary>
    [DataContract]
    public enum LogLevel
    {
        [EnumMember]
        Trace,
        [EnumMember]
        Debug,
        [EnumMember]
        Info,
        [EnumMember]
        Warn,
        [EnumMember]
        Error,
        [EnumMember]
        Fatal
    }

    /// <summary>
    /// Action to log a message with variable resolution to the output.
    /// Supports variable references in the format {variableName}.
    /// </summary>
    [DataContract]
    public class LogAction : ILogicAction, INotifyPropertyChanged
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string Name => "Log";

        [DataMember]
        private string _description = "Log a message to the output";
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
        /// Environment for resolving variables. Not serialized.
        /// </summary>
        [IgnoreDataMember]
        public Logic.Environment Environment { get; set; }

        /// <summary>
        /// The log level for this message
        /// </summary>
        [DataMember]
        private LogLevel _logLevel = LogLevel.Info;
        public LogLevel LogLevel
        {
            get => _logLevel;
            set
            {
                if (_logLevel != value)
                {
                    _logLevel = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogLevel)));
                }
            }
        }

        /// <summary>
        /// The message to log. Supports variable references in format {variableName}.
        /// </summary>
        [DataMember]
        private string _message = string.Empty;
        public string Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
                }
            }
        }

        public void Cancel()
        {
            // Nothing to cancel for logging
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            State = ActionState.Running;
            await Task.Yield();

            try
            {
                // Resolve variables in the message
                string resolvedMessage = ResolveVariables(Message);

                // Log at the appropriate level
                switch (LogLevel)
                {
                    case LogLevel.Trace:
                        Logger.Trace(resolvedMessage);
                        break;
                    case LogLevel.Debug:
                        Logger.Debug(resolvedMessage);
                        break;
                    case LogLevel.Info:
                        Logger.Info(resolvedMessage);
                        break;
                    case LogLevel.Warn:
                        Logger.Warn(resolvedMessage);
                        break;
                    case LogLevel.Error:
                        Logger.Error(resolvedMessage);
                        break;
                    case LogLevel.Fatal:
                        Logger.Fatal(resolvedMessage);
                        break;
                }

                State = ActionState.Completed;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"[LogAction] ERROR logging message: {ex.Message}");
                State = ActionState.Failed;
                return false;
            }
        }

        /// <summary>
        /// Resolves variable references in the format {variableName} by looking them up in Environment.
        /// </summary>
        private string ResolveVariables(string message)
        {
            if (string.IsNullOrEmpty(message) || Environment == null)
            {
                return message;
            }

            // Match {variableName} patterns
            return Regex.Replace(message, @"\{(\w+)\}", match =>
            {
                string variableName = match.Groups[1].Value;
                var variable = Environment.Variables.FirstOrDefault(v => v.Name == variableName);

                if (variable != null)
                {
                    // Return the variable value as string (using ToString())
                    return variable.Value?.ToString() ?? string.Empty;
                }

                // If variable not found, return the original placeholder
                return match.Value;
            });
        }
    }
}
