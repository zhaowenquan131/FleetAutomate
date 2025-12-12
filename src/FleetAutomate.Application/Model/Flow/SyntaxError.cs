using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model.Flow
{
    /// <summary>
    /// Represents a syntax error found during validation of a Flow.
    /// </summary>
    public class SyntaxError
    {
        /// <summary>
        /// Gets or sets the action where the error occurred.
        /// </summary>
        public IAction Action { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the property name where the error occurred (optional).
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Gets or sets the severity of the error.
        /// </summary>
        public SyntaxErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the path to the action in the Flow tree (for nested structures).
        /// </summary>
        public string ActionPath { get; set; }

        /// <summary>
        /// Gets or sets additional context information about the error.
        /// </summary>
        public object Context { get; set; }

        public SyntaxError(IAction action, string message, SyntaxErrorSeverity severity = SyntaxErrorSeverity.Error)
        {
            Action = action;
            Message = message;
            Severity = severity;
        }

        public SyntaxError(IAction action, string message, string propertyName, SyntaxErrorSeverity severity = SyntaxErrorSeverity.Error)
        {
            Action = action;
            Message = message;
            PropertyName = propertyName;
            Severity = severity;
        }

        public override string ToString()
        {
            var path = string.IsNullOrEmpty(ActionPath) ? "" : $" at {ActionPath}";
            var property = string.IsNullOrEmpty(PropertyName) ? "" : $".{PropertyName}";
            return $"[{Severity}]{path}{property}: {Message}";
        }
    }

    /// <summary>
    /// Represents the severity of a syntax error.
    /// </summary>
    public enum SyntaxErrorSeverity
    {
        /// <summary>
        /// Warning - the Flow can still execute but may not behave as expected.
        /// </summary>
        Warning,
        /// <summary>
        /// Error - the Flow cannot execute properly.
        /// </summary>
        Error,
        /// <summary>
        /// Critical - the Flow will definitely fail during execution.
        /// </summary>
        Critical
    }
}