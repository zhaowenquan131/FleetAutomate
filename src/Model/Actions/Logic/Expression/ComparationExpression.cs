using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model.Actions.Logic.Expression
{
    public class GreaterThanExpression : ExpressionBase<bool>
    {
        public object OperandLeft { get; set; }
        public object OperandRight { get; set; }

        public override void Evaluate()
        {
            if (OperandLeft is IComparable comp1 && OperandRight is IComparable comp2)
            {
                Result = comp1.CompareTo(comp2) > 0;
            }
            else if (OperandLeft is ExpressionBase<int> exp1 && OperandRight is ExpressionBase<int> exp2)
            {
                exp1.Evaluate();
                exp1.Evaluate();
                Result = exp1.Result > exp2.Result;
            }
            else if (OperandLeft is ExpressionBase<double> expD1 && OperandRight is ExpressionBase<double> expD2)
            {
                expD1.Evaluate();
                expD2.Evaluate();
                Result = expD1.Result > expD2.Result;
            }
            else if (OperandLeft is ExpressionBase<float> expF1 && OperandRight is ExpressionBase<float> expF2)
            {
                expF1.Evaluate();
                expF2.Evaluate();
                Result = expF1.Result > expF2.Result;
            }
            else
            {
                throw new InvalidOperationException("Operands must implement IComparable or be of type Expression<Numeric Type>");
            }
        }
    }

    public class GreaterThanOrEqualExpression : ExpressionBase<bool>
    {
        public object OperandLeft { get; set; }
        public object OperandRight { get; set; }

        public override void Evaluate()
        {
            if (OperandLeft is IComparable comp1 && OperandRight is IComparable comp2)
            {
                Result = comp1.CompareTo(comp2) >= 0;
            }
            else if (OperandLeft is ExpressionBase<int> exp1 && OperandRight is ExpressionBase<int> exp2)
            {
                exp1.Evaluate();
                exp1.Evaluate();
                Result = exp1.Result >= exp2.Result;
            }
            else if (OperandLeft is ExpressionBase<double> expD1 && OperandRight is ExpressionBase<double> expD2)
            {
                expD1.Evaluate();
                expD2.Evaluate();
                Result = expD1.Result >= expD2.Result;
            }
            else if (OperandLeft is ExpressionBase<float> expF1 && OperandRight is ExpressionBase<float> expF2)
            {
                expF1.Evaluate();
                expF2.Evaluate();
                Result = expF1.Result >= expF2.Result;
            }
            else
            {
                throw new InvalidOperationException("Operands must implement IComparable or be of type Expression<Numeric Type>");
            }
        }
    }

    public class SmallerThanExpression : ExpressionBase<bool>
    {
        public object OperandLeft { get; set; }
        public object OperandRight { get; set; }

        public override void Evaluate()
        {
            if (OperandLeft is IComparable comp1 && OperandRight is IComparable comp2)
            {
                Result = comp1.CompareTo(comp2) < 0;
            }
            else if (OperandLeft is ExpressionBase<int> exp1 && OperandRight is ExpressionBase<int> exp2)
            {
                exp1.Evaluate();
                exp1.Evaluate();
                Result = exp1.Result < exp2.Result;
            }
            else if (OperandLeft is ExpressionBase<double> expD1 && OperandRight is ExpressionBase<double> expD2)
            {
                expD1.Evaluate();
                expD2.Evaluate();
                Result = expD1.Result < expD2.Result;
            }
            else if (OperandLeft is ExpressionBase<float> expF1 && OperandRight is ExpressionBase<float> expF2)
            {
                expF1.Evaluate();
                expF2.Evaluate();
                Result = expF1.Result < expF2.Result;
            }
            else
            {
                throw new InvalidOperationException("Operands must implement IComparable or be of type Expression<Numeric Type>");
            }
        }
    }
    public class SmallerThanOrEqualExpression : ExpressionBase<bool>
    {
        public object OperandLeft { get; set; }
        public object OperandRight { get; set; }

        public override void Evaluate()
        {
            if (OperandLeft is IComparable comp1 && OperandRight is IComparable comp2)
            {
                Result = comp1.CompareTo(comp2) <= 0;
            }
            else if (OperandLeft is ExpressionBase<int> exp1 && OperandRight is ExpressionBase<int> exp2)
            {
                exp1.Evaluate();
                exp1.Evaluate();
                Result = exp1.Result <= exp2.Result;
            }
            else if (OperandLeft is ExpressionBase<double> expD1 && OperandRight is ExpressionBase<double> expD2)
            {
                expD1.Evaluate();
                expD2.Evaluate();
                Result = expD1.Result <= expD2.Result;
            }
            else if (OperandLeft is ExpressionBase<float> expF1 && OperandRight is ExpressionBase<float> expF2)
            {
                expF1.Evaluate();
                expF2.Evaluate();
                Result = expF1.Result <= expF2.Result;
            }
            else
            {
                throw new InvalidOperationException("Operands must implement IComparable or be of type Expression<Numeric Type>");
            }
        }
    }

    /// <summary>
    /// Utility class for parsing boolean expressions from text.
    /// </summary>
    public static class BooleanExpressionParser
    {
        /// <summary>
        /// Parses a boolean expression from text format (e.g., "5 > 3", "true", "false").
        /// Returns null if the input is invalid.
        /// </summary>
        /// <param name="input">The text to parse</param>
        /// <returns>A boolean expression object, or null if parsing fails.</returns>
        public static ExpressionBase<bool>? Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var trimmedInput = input.Trim();

            // Try to parse as boolean literal first
            if (bool.TryParse(trimmedInput, out var boolValue))
            {
                return new LiteralExpression<bool>(boolValue) { RawText = input };
            }

            // Try to parse as comparison expression
            var comparisonExpression = ComparationExpressionParser.Parse(trimmedInput);
            if (comparisonExpression != null)
            {
                return comparisonExpression;
            }

            return null;
        }
    }

    /// <summary>
    /// Utility class for parsing comparison expressions from text.
    /// </summary>
    public static class ComparationExpressionParser
    {
        /// <summary>
        /// Parses a comparison expression from text format (e.g., "5 > 3", "x >= y").
        /// Returns null if the input is invalid.
        /// </summary>
        /// <param name="input">The text to parse (e.g., "5 > 3")</param>
        /// <returns>A comparison expression object, or null if parsing fails.</returns>
        public static ExpressionBase<bool>? Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            // Try to match comparison operators: >=, <=, >, <
            // Order matters: >= and <= must be checked before > and <
            var operatorPatterns = new[]
            {
                (">=", typeof(GreaterThanOrEqualExpression)),
                ("<=", typeof(SmallerThanOrEqualExpression)),
                (">", typeof(GreaterThanExpression)),
                ("<", typeof(SmallerThanExpression))
            };

            foreach (var (operatorSymbol, expressionType) in operatorPatterns)
            {
                var parts = input.Split(new[] { operatorSymbol }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var leftOperand = parts[0].Trim();
                    var rightOperand = parts[1].Trim();

                    // Validate operands are not empty
                    if (string.IsNullOrEmpty(leftOperand) || string.IsNullOrEmpty(rightOperand))
                    {
                        return null;
                    }

                    try
                    {
                        // Try to parse operands as numbers or keep as strings/variables
                        var left = ParseOperand(leftOperand);
                        var right = ParseOperand(rightOperand);

                        if (left == null || right == null)
                        {
                            return null;
                        }

                        // Create the appropriate expression type
                        var expression = (ExpressionBase<bool>)Activator.CreateInstance(expressionType)!;

                        // Set the operands using reflection
                        var leftProp = expressionType.GetProperty("OperandLeft");
                        var rightProp = expressionType.GetProperty("OperandRight");

                        if (leftProp != null && rightProp != null)
                        {
                            leftProp.SetValue(expression, left);
                            rightProp.SetValue(expression, right);
                            expression.RawText = input;
                            return expression;
                        }

                        return null;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Parses a single operand, attempting to convert it to a number if possible,
        /// otherwise returning it as a string.
        /// </summary>
        private static object? ParseOperand(string operand)
        {
            if (string.IsNullOrWhiteSpace(operand))
            {
                return null;
            }

            // Try to parse as integer
            if (int.TryParse(operand, out var intValue))
            {
                return intValue;
            }

            // Try to parse as double
            if (double.TryParse(operand, out var doubleValue))
            {
                return doubleValue;
            }

            // Try to parse as float
            if (float.TryParse(operand, out var floatValue))
            {
                return floatValue;
            }

            // Return as string for variable names or other uses
            return operand;
        }
    }
}
