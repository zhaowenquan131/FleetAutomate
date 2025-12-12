using System;
using System.Runtime.Serialization;

namespace Canvas.TestRunner.Model.Actions.Logic.Expression
{
    /// <summary>
    /// A literal expression that holds a constant value.
    /// This is used for simple literal values like "42", "3.14", "hello", "true".
    /// </summary>
    /// <typeparam name="TResult">The type of the literal value</typeparam>
    [DataContract]
    public class LiteralExpression<TResult> : ExpressionBase<TResult>
    {
        /// <summary>
        /// The literal value that this expression represents.
        /// </summary>
        [DataMember]
        public TResult? LiteralValue { get; set; }

        public LiteralExpression() { }

        public LiteralExpression(TResult? value)
        {
            LiteralValue = value;
            Result = value;
        }

        public override void Evaluate()
        {
            // For a literal expression, the result is always the literal value
            Result = LiteralValue;
        }
    }

    /// <summary>
    /// Factory class for creating literal expressions from text values.
    /// </summary>
    public static class LiteralExpressionFactory
    {
        /// <summary>
        /// Creates a literal value (not wrapped in expression) from text.
        /// Returns the parsed value directly as an object.
        /// </summary>
        /// <param name="value">The text representation of the value</param>
        /// <param name="type">The target type ("int", "double", "string", "bool")</param>
        /// <returns>The parsed value as an object, or null if parsing fails</returns>
        public static object? CreateLiteral(string? value, string type)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                // Create default values for empty input
                return type switch
                {
                    "int" => 0,
                    "double" => 0.0,
                    "bool" => false,
                    "string" => string.Empty,
                    _ => null
                };
            }

            var trimmedValue = value.Trim();

            return type switch
            {
                "int" => ParseInt(trimmedValue),
                "double" => ParseDouble(trimmedValue),
                "bool" => ParseBool(trimmedValue),
                "string" => ParseString(trimmedValue),
                _ => null
            };
        }

        private static object? ParseInt(string value)
        {
            return int.TryParse(value, out var intValue) ? intValue : null;
        }

        private static object? ParseDouble(string value)
        {
            return double.TryParse(value, out var doubleValue) ? doubleValue : null;
        }

        private static object? ParseBool(string value)
        {
            return bool.TryParse(value, out var boolValue) ? boolValue : null;
        }

        private static object? ParseString(string value)
        {
            // For strings, just accept any non-empty value
            return value;
        }
    }
}
