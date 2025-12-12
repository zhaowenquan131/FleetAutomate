using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model.Flow
{
    /// <summary>
    /// Provides a summary of Flow syntax validation results.
    /// </summary>
    public class FlowValidationSummary
    {
        /// <summary>
        /// Gets or sets the total number of syntax errors found.
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of critical errors found.
        /// </summary>
        public int CriticalErrors { get; set; }

        /// <summary>
        /// Gets or sets the number of regular errors found.
        /// </summary>
        public int Errors { get; set; }

        /// <summary>
        /// Gets or sets the number of warnings found.
        /// </summary>
        public int Warnings { get; set; }

        /// <summary>
        /// Gets or sets all syntax errors found during validation.
        /// </summary>
        public IList<SyntaxError> AllErrors { get; set; } = new List<SyntaxError>();

        /// <summary>
        /// Gets or sets whether the Flow is considered valid (no critical or regular errors).
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the error summary formatted as a readable string.
        /// </summary>
        public string Summary => $"Validation: {(IsValid ? "PASSED" : "FAILED")} - " +
                                $"Critical: {CriticalErrors}, Errors: {Errors}, Warnings: {Warnings}";

        /// <summary>
        /// Gets a detailed report of all errors.
        /// </summary>
        public string DetailedReport
        {
            get
            {
                if (!AllErrors.Any())
                {
                    return "No syntax errors found.";
                }

                var report = new StringBuilder();
                report.AppendLine($"Syntax Validation Report:");
                report.AppendLine($"========================");
                report.AppendLine(Summary);
                report.AppendLine();

                if (CriticalErrors > 0)
                {
                    report.AppendLine("CRITICAL ERRORS:");
                    foreach (var error in AllErrors.Where(e => e.Severity == SyntaxErrorSeverity.Critical))
                    {
                        report.AppendLine($"  - {error}");
                    }
                    report.AppendLine();
                }

                if (Errors > 0)
                {
                    report.AppendLine("ERRORS:");
                    foreach (var error in AllErrors.Where(e => e.Severity == SyntaxErrorSeverity.Error))
                    {
                        report.AppendLine($"  - {error}");
                    }
                    report.AppendLine();
                }

                if (Warnings > 0)
                {
                    report.AppendLine("WARNINGS:");
                    foreach (var error in AllErrors.Where(e => e.Severity == SyntaxErrorSeverity.Warning))
                    {
                        report.AppendLine($"  - {error}");
                    }
                }

                return report.ToString();
            }
        }

        /// <summary>
        /// Gets errors grouped by their severity.
        /// </summary>
        public IEnumerable<IGrouping<SyntaxErrorSeverity, SyntaxError>> ErrorsBySeverity =>
            AllErrors.GroupBy(e => e.Severity);

        /// <summary>
        /// Gets errors grouped by the action they occurred in.
        /// </summary>
        public IEnumerable<IGrouping<IAction, SyntaxError>> ErrorsByAction =>
            AllErrors.GroupBy(e => e.Action);

        /// <summary>
        /// Gets errors grouped by their location path.
        /// </summary>
        public IEnumerable<IGrouping<string, SyntaxError>> ErrorsByPath =>
            AllErrors.GroupBy(e => e.ActionPath ?? "Unknown");
    }
}