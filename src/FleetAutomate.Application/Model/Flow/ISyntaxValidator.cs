using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FleetAutomate.Model.Flow
{
    /// <summary>
    /// Interface for actions that can validate their own syntax.
    /// </summary>
    public interface ISyntaxValidator
    {
        /// <summary>
        /// Validates the syntax of this action and returns any errors found.
        /// </summary>
        /// <param name="context">The validation context containing environment and parent information.</param>
        /// <returns>A collection of syntax errors, or empty if no errors found.</returns>
        IEnumerable<SyntaxError> ValidateSyntax(SyntaxValidationContext context);
    }

    /// <summary>
    /// Context information for syntax validation.
    /// </summary>
    public class SyntaxValidationContext
    {
        /// <summary>
        /// Gets or sets the environment containing variables and other context.
        /// </summary>
        public Actions.Logic.Environment Environment { get; set; }

        /// <summary>
        /// Gets or sets the current path in the Flow tree.
        /// </summary>
        public string CurrentPath { get; set; } = "";

        /// <summary>
        /// Gets or sets the parent action (for nested validation).
        /// </summary>
        public IAction Parent { get; set; }

        /// <summary>
        /// Gets or sets additional validation options.
        /// </summary>
        public SyntaxValidationOptions Options { get; set; } = new SyntaxValidationOptions();

        /// <summary>
        /// Creates a child context for validating nested actions.
        /// </summary>
        /// <param name="childName">The name/identifier of the child action.</param>
        /// <param name="parent">The parent action.</param>
        /// <returns>A new context for the child action.</returns>
        public SyntaxValidationContext CreateChildContext(string childName, IAction parent)
        {
            return new SyntaxValidationContext
            {
                Environment = Environment,
                CurrentPath = string.IsNullOrEmpty(CurrentPath) ? childName : $"{CurrentPath}.{childName}",
                Parent = parent,
                Options = Options
            };
        }
    }

    /// <summary>
    /// Options for syntax validation.
    /// </summary>
    public class SyntaxValidationOptions
    {
        /// <summary>
        /// Gets or sets whether to validate nested actions recursively.
        /// </summary>
        public bool ValidateNested { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to include warnings in the validation results.
        /// </summary>
        public bool IncludeWarnings { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum depth for recursive validation.
        /// </summary>
        public int MaxDepth { get; set; } = 100;
    }
}