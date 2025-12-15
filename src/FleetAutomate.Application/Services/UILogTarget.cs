using NLog;
using NLog.Targets;
using System.Collections.ObjectModel;
using System.Windows;

namespace FleetAutomate.Services
{
    /// <summary>
    /// Custom NLog target that captures log messages and makes them available for UI display.
    /// </summary>
    [Target("UILog")]
    public sealed class UILogTarget : TargetWithLayout
    {
        private static readonly object _lock = new object();
        private static ObservableCollection<LogEntry>? _logEntries;
        private const int MaxLogEntries = 1000;

        /// <summary>
        /// Gets the log entries collection. This should be bound to the UI.
        /// </summary>
        public static ObservableCollection<LogEntry> LogEntries
        {
            get
            {
                if (_logEntries == null)
                {
                    _logEntries = new ObservableCollection<LogEntry>();
                }
                return _logEntries;
            }
        }

        /// <summary>
        /// Writes the log event to the collection.
        /// </summary>
        protected override void Write(LogEventInfo logEvent)
        {
            string message = Layout.Render(logEvent);

            var logEntry = new LogEntry
            {
                Timestamp = logEvent.TimeStamp,
                Level = logEvent.Level.Name,
                Logger = logEvent.LoggerName ?? string.Empty,
                Message = message
            };

            // Dispatch to UI thread if needed
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_lock)
                    {
                        LogEntries.Add(logEntry);

                        // Keep only the most recent entries
                        while (LogEntries.Count > MaxLogEntries)
                        {
                            LogEntries.RemoveAt(0);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Clears all log entries.
        /// </summary>
        public static void Clear()
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_lock)
                    {
                        LogEntries.Clear();
                    }
                });
            }
        }
    }

    /// <summary>
    /// Represents a single log entry.
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Logger { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {Message}";
        }
    }
}
