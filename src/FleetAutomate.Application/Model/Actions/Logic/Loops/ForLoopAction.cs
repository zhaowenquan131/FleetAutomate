using Canvas.TestRunner.Model.Actions.Logic;
using Canvas.TestRunner.Model.Flow;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;


namespace Canvas.TestRunner.Model.Actions.Logic.Loops
{
    [DataContract]
    [KnownType(typeof(SetVariableAction<object>))]
    [KnownType(typeof(WhileLoopAction))]
    [KnownType(typeof(ForLoopAction))]
    [KnownType(typeof(IfAction))]
    [KnownType(typeof(Flow.TestFlow))]
    [KnownType(typeof(System.LaunchApplicationAction))]
    [KnownType(typeof(UIAutomation.WaitForElementAction))]
    [KnownType(typeof(UIAutomation.ClickElementAction))]
    public class ForLoopAction : ILogicAction, ISyntaxValidator, ICompositeAction
    {
        [DataMember]
        public Environment Environment { get; set; }

        [DataMember]
        public string Name => throw new NotImplementedException();

        [DataMember]
        public string Description => throw new NotImplementedException();

        [DataMember]
        public bool IsEnabled => throw new NotImplementedException();

        [DataMember]
        public object Initialization { get; set; }


        [DataMember]
        public object Condition { get; set; }


        [DataMember]
        public object Increment { get; set; }

        public ObservableCollection<IAction> Body { get; } = [];
        
        [DataMember]
        public ActionState State { get; set; }

        /// <summary>
        /// XML serialization property for Body collection.
        /// </summary>
        [DataMember]
        public IAction[] BodyArray
        {
            get => Body.ToArray();
            set
            {
                Body.Clear();
                if (value != null)
                {
                    foreach (var action in value)
                    {
                        Body.Add(action);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the child actions from the Body collection.
        /// </summary>
        /// <returns>The collection of child actions in the Body.</returns>
        public ObservableCollection<IAction> GetChildActions()
        {
            return Body;
        }

        public void Cancel()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            // Execute initialization
            if (Initialization is IAction initAction)
            {
                await initAction.ExecuteAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
            }

            // Loop while condition is true
            while (true)
            {
                // Evaluate condition
                if (Condition is ExpressionBase<bool> boolExp)
                {
                    boolExp.Evaluate();
                    Condition = boolExp.Result;
                }
                if (Condition is not bool conditionValue)
                {
                    // If we reach this point, it means validation was skipped or failed
                    // Return false to stop execution gracefully instead of throwing
                    State = ActionState.Failed;
                    return false;
                }

                if (!conditionValue)
                {
                    break;
                }

                // Execute body
                foreach (var action in Body)
                {
                    await action.ExecuteAsync(cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return false;
                    }
                }

                // Execute increment
                if (Increment is IAction incrementAction)
                {
                    await incrementAction.ExecuteAsync(cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public IEnumerable<SyntaxError> ValidateSyntax(SyntaxValidationContext context)
        {
            var errors = new List<SyntaxError>();

            // Validate condition
            if (Condition == null)
            {
                errors.Add(new SyntaxError(this, "For loop condition cannot be null", "Condition", SyntaxErrorSeverity.Critical));
            }
            else if (Condition is not bool && Condition is not ExpressionBase<bool>)
            {
                errors.Add(new SyntaxError(this, 
                    $"For loop condition must be a boolean value or Expression<bool>, but found {Condition.GetType().Name}", 
                    "Condition", 
                    SyntaxErrorSeverity.Critical)
                {
                    Context = Condition.GetType()
                });
            }

            // Validate initialization
            if (Initialization != null && Initialization is not IAction)
            {
                errors.Add(new SyntaxError(this, 
                    $"For loop initialization must be an IAction or null, but found {Initialization.GetType().Name}", 
                    "Initialization", 
                    SyntaxErrorSeverity.Error)
                {
                    Context = Initialization.GetType()
                });
            }

            // Validate increment
            if (Increment != null && Increment is not IAction)
            {
                errors.Add(new SyntaxError(this, 
                    $"For loop increment must be an IAction or null, but found {Increment.GetType().Name}", 
                    "Increment", 
                    SyntaxErrorSeverity.Error)
                {
                    Context = Increment.GetType()
                });
            }

            // Validate body
            if (Body == null)
            {
                errors.Add(new SyntaxError(this, "For loop body cannot be null", "Body", SyntaxErrorSeverity.Critical));
            }
            else if (Body.Count == 0)
            {
                errors.Add(new SyntaxError(this, "For loop body is empty - loop will have no effect", "Body", SyntaxErrorSeverity.Warning));
            }

            return errors;
        }
    }
}
