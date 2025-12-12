using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;

using FleetAutomate.Model.Flow;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FleetAutomate.Model.Flow
{
    /// <summary>
    /// Provides comprehensive syntax validation for Flow objects and their action trees.
    /// </summary>
    public class FlowValidator
    {
        /// <summary>
        /// Validates the entire Flow and all its nested actions.
        /// </summary>
        /// <param name="flow">The Flow to validate.</param>
        /// <param name="options">Validation options.</param>
        /// <returns>A collection of all syntax errors found in the Flow.</returns>
        public static IEnumerable<SyntaxError> ValidateFlow(TestFlow flow, SyntaxValidationOptions? options = null)
        {
            if (flow == null)
            {
                yield return new SyntaxError(null, "Flow cannot be null", SyntaxErrorSeverity.Critical);
                yield break;
            }

            options ??= new SyntaxValidationOptions();
            var context = new SyntaxValidationContext
            {
                Environment = flow.Environment,
                CurrentPath = "Flow",
                Options = options
            };

            // Validate the Flow itself
            foreach (var error in ValidateFlowProperties(flow, context))
            {
                yield return error;
            }

            // Validate all actions in the Flow
            if (options.ValidateNested && flow.Actions != null)
            {
                for (int i = 0; i < flow.Actions.Count; i++)
                {
                    var action = flow.Actions[i];
                    var childContext = context.CreateChildContext($"Actions[{i}]", flow);
                    
                    foreach (var error in ValidateAction(action, childContext, 0))
                    {
                        yield return error;
                    }
                }
            }
        }

        /// <summary>
        /// Validates a single action and its nested structure.
        /// </summary>
        /// <param name="action">The action to validate.</param>
        /// <param name="context">The validation context.</param>
        /// <param name="depth">Current recursion depth.</param>
        /// <returns>A collection of syntax errors found in the action.</returns>
        public static IEnumerable<SyntaxError> ValidateAction(IAction action, SyntaxValidationContext context, int depth = 0)
        {
            if (action == null)
            {
                yield return new SyntaxError(null, "Action cannot be null", SyntaxErrorSeverity.Critical)
                {
                    ActionPath = context.CurrentPath
                };
                yield break;
            }

            if (depth > context.Options.MaxDepth)
            {
                yield return new SyntaxError(action, $"Maximum validation depth ({context.Options.MaxDepth}) exceeded", SyntaxErrorSeverity.Warning)
                {
                    ActionPath = context.CurrentPath
                };
                yield break;
            }

            // If the action implements ISyntaxValidator, use its custom validation
            if (action is ISyntaxValidator validator)
            {
                foreach (var error in validator.ValidateSyntax(context))
                {
                    error.ActionPath = context.CurrentPath;
                    yield return error;
                }
            }
            else
            {
                // Perform default validation
                foreach (var error in ValidateActionDefaults(action, context))
                {
                    yield return error;
                }
            }

            // Validate nested actions if this action contains them
            if (context.Options.ValidateNested)
            {
                foreach (var error in ValidateNestedActions(action, context, depth + 1))
                {
                    yield return error;
                }
            }
        }

        /// <summary>
        /// Validates basic Flow properties.
        /// </summary>
        private static IEnumerable<SyntaxError> ValidateFlowProperties(TestFlow flow, SyntaxValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(flow.Name))
            {
                yield return new SyntaxError(flow, "Flow name cannot be null or empty", "Name", SyntaxErrorSeverity.Warning)
                {
                    ActionPath = context.CurrentPath
                };
            }

            if (flow.Environment == null)
            {
                yield return new SyntaxError(flow, "Flow environment cannot be null", "Environment", SyntaxErrorSeverity.Error)
                {
                    ActionPath = context.CurrentPath
                };
            }

            if (flow.Actions == null)
            {
                yield return new SyntaxError(flow, "Flow actions collection cannot be null", "Actions", SyntaxErrorSeverity.Critical)
                {
                    ActionPath = context.CurrentPath
                };
            }
            else if (flow.Actions.Count == 0)
            {
                yield return new SyntaxError(flow, "Flow contains no actions", "Actions", SyntaxErrorSeverity.Warning)
                {
                    ActionPath = context.CurrentPath
                };
            }
        }

        /// <summary>
        /// Performs default validation for actions that don't implement ISyntaxValidator.
        /// </summary>
        private static IEnumerable<SyntaxError> ValidateActionDefaults(IAction action, SyntaxValidationContext context)
        {
            // Check basic IAction properties
            var nameErrors = ValidateActionName(action, context);
            foreach (var error in nameErrors)
            {
                yield return error;
            }

            var descriptionErrors = ValidateActionDescription(action, context);
            foreach (var error in descriptionErrors)
            {
                yield return error;
            }

            // Validate specific action types
            foreach (var error in ValidateSpecificActionTypes(action, context))
            {
                yield return error;
            }
        }

        /// <summary>
        /// Validates specific known action types with their unique requirements.
        /// </summary>
        private static IEnumerable<SyntaxError> ValidateSpecificActionTypes(IAction action, SyntaxValidationContext context)
        {
            switch (action)
            {
                case WhileLoopAction whileLoop:
                    if (whileLoop.Condition == null)
                    {
                        yield return new SyntaxError(action, "While loop condition cannot be null", "Condition", SyntaxErrorSeverity.Critical)
                        {
                            ActionPath = context.CurrentPath
                        };
                    }
                    else if (whileLoop.Condition is not bool && whileLoop.Condition is not ExpressionBase<bool>)
                    {
                        yield return new SyntaxError(action, "While loop condition must be a boolean value or Expression<bool>", "Condition", SyntaxErrorSeverity.Critical)
                        {
                            ActionPath = context.CurrentPath,
                            Context = whileLoop.Condition?.GetType()?.Name
                        };
                    }
                    break;

                case ForLoopAction forLoop:
                    if (forLoop.Condition == null)
                    {
                        yield return new SyntaxError(action, "For loop condition cannot be null", "Condition", SyntaxErrorSeverity.Critical)
                        {
                            ActionPath = context.CurrentPath
                        };
                    }
                    else if (forLoop.Condition is not bool && forLoop.Condition is not ExpressionBase<bool>)
                    {
                        yield return new SyntaxError(action, "For loop condition must be a boolean value or Expression<bool>", "Condition", SyntaxErrorSeverity.Critical)
                        {
                            ActionPath = context.CurrentPath,
                            Context = forLoop.Condition?.GetType()?.Name
                        };
                    }

                    if (forLoop.Initialization != null && forLoop.Initialization is not IAction)
                    {
                        yield return new SyntaxError(action, "For loop initialization must be an IAction or null", "Initialization", SyntaxErrorSeverity.Error)
                        {
                            ActionPath = context.CurrentPath,
                            Context = forLoop.Initialization?.GetType()?.Name
                        };
                    }

                    if (forLoop.Increment != null && forLoop.Increment is not IAction)
                    {
                        yield return new SyntaxError(action, "For loop increment must be an IAction or null", "Increment", SyntaxErrorSeverity.Error)
                        {
                            ActionPath = context.CurrentPath,
                            Context = forLoop.Increment?.GetType()?.Name
                        };
                    }
                    break;
            }
        }

        /// <summary>
        /// Validates nested actions within an action.
        /// </summary>
        private static IEnumerable<SyntaxError> ValidateNestedActions(IAction action, SyntaxValidationContext context, int depth)
        {
            // Use reflection to find ObservableCollection<IAction> properties
            var actionCollectionProperties = action.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(ObservableCollection<IAction>).IsAssignableFrom(p.PropertyType))
                .ToList();

            foreach (var property in actionCollectionProperties)
            {
                if (property.GetValue(action) is ObservableCollection<IAction> collection)
                {
                    for (int i = 0; i < collection.Count; i++)
                    {
                        var nestedAction = collection[i];
                        var childContext = context.CreateChildContext($"{property.Name}[{i}]", action);
                        
                        foreach (var error in ValidateAction(nestedAction, childContext, depth))
                        {
                            yield return error;
                        }
                    }
                }
            }

            // Also check for single IAction properties
            var singleActionProperties = action.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(IAction).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(IAction[]) && !typeof(ObservableCollection<IAction>).IsAssignableFrom(p.PropertyType))
                .ToList();

            foreach (var property in singleActionProperties)
            {
                if (property.GetValue(action) is IAction nestedAction)
                {
                    var childContext = context.CreateChildContext(property.Name, action);
                    
                    foreach (var error in ValidateAction(nestedAction, childContext, depth))
                    {
                        yield return error;
                    }
                }
            }
        }

        /// <summary>
        /// Validates the Name property of an action.
        /// </summary>
        private static IEnumerable<SyntaxError> ValidateActionName(IAction action, SyntaxValidationContext context)
        {
            var errors = new List<SyntaxError>();
            
            try
            {
                var name = action.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new SyntaxError(action, "Action name cannot be null or empty", "Name", SyntaxErrorSeverity.Warning)
                    {
                        ActionPath = context.CurrentPath
                    });
                }
            }
            catch (NotImplementedException)
            {
                errors.Add(new SyntaxError(action, "Action Name property is not implemented", "Name", SyntaxErrorSeverity.Warning)
                {
                    ActionPath = context.CurrentPath
                });
            }
            
            return errors;
        }

        /// <summary>
        /// Validates the Description property of an action.
        /// </summary>
        private static IEnumerable<SyntaxError> ValidateActionDescription(IAction action, SyntaxValidationContext context)
        {
            var errors = new List<SyntaxError>();
            
            try
            {
                var description = action.Description;
                if (string.IsNullOrWhiteSpace(description))
                {
                    errors.Add(new SyntaxError(action, "Action description is empty", "Description", SyntaxErrorSeverity.Warning)
                    {
                        ActionPath = context.CurrentPath
                    });
                }
            }
            catch (NotImplementedException)
            {
                errors.Add(new SyntaxError(action, "Action Description property is not implemented", "Description", SyntaxErrorSeverity.Warning)
                {
                    ActionPath = context.CurrentPath
                });
            }
            
            return errors;
        }
    }
}