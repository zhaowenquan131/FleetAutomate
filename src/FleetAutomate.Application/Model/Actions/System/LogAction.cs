using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using FleetAutomate.Expressions;
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

    [DataContract]
    public enum LogMessageMode
    {
        [EnumMember]
        Literal,

        [EnumMember]
        Expression
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

        [IgnoreDataMember]
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
        public Logic.Environment Environment { get; set; } = new();

        [IgnoreDataMember]
        public string LastResolvedMessage { get; private set; } = string.Empty;

        [IgnoreDataMember]
        public IExpressionUiQueryService UiQueryService { get; set; } = DefaultExpressionUiQueryService.Instance;

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

        [DataMember]
        private LogMessageMode _messageMode = LogMessageMode.Literal;
        public LogMessageMode MessageMode
        {
            get => _messageMode;
            set
            {
                if (_messageMode != value)
                {
                    _messageMode = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MessageMode)));
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
                string resolvedMessage = await ResolveMessageAsync(cancellationToken);
                LastResolvedMessage = resolvedMessage;

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

        private async Task<string> ResolveMessageAsync(CancellationToken cancellationToken)
        {
            if (MessageMode == LogMessageMode.Expression)
            {
                return await ResolveInterpolatedExpressionsAsync(Message, cancellationToken);
            }

            return ResolveVariables(Message);
        }

        private async Task<string> ResolveInterpolatedExpressionsAsync(string message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            var engine = new SimpleExpressionEngine();
            var context = new ExpressionContext(Environment ?? new Logic.Environment(), UiQueryService);
            var matches = Regex.Matches(message, @"\{([^{}]+)\}");
            if (matches.Count == 0)
            {
                var result = await engine.EvaluateAsync(message, context, cancellationToken);
                return Convert.ToString(result.Value, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            }

            var resolvedMessage = message;

            foreach (Match match in matches.Cast<Match>().Reverse())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var expressionText = match.Groups[1].Value.Trim();
                var result = await engine.EvaluateAsync(expressionText, context, cancellationToken);
                var replacement = Convert.ToString(result.Value, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                resolvedMessage = resolvedMessage.Remove(match.Index, match.Length).Insert(match.Index, replacement);
            }

            return resolvedMessage;
        }
    }
}
