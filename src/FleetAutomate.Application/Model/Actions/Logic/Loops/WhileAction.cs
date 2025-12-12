using FleetAutomate.Model.Flow;

using System.Runtime.Serialization;
using System.Collections.ObjectModel;
using System.Linq;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Model.Actions.Logic.Loops
{
    [DataContract]
    [KnownType(typeof(SetVariableAction<object>))]
    [KnownType(typeof(WhileLoopAction))]
    [KnownType(typeof(ForLoopAction))]
    [KnownType(typeof(IfAction))]
    [KnownType(typeof(TestFlow))]
    [KnownType(typeof(System.LaunchApplicationAction))]
    [KnownType(typeof(UIAutomation.WaitForElementAction))]
    [KnownType(typeof(UIAutomation.ClickElementAction))]
    public class WhileLoopAction : ILogicAction, ISyntaxValidator, ICompositeAction
    {
        [DataMember]
        public Environment Environment { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public bool IsEnabled { get; set; }

        [DataMember]
        public object Condition { get; set; }


        [DataMember]
        public ActionState State { get; set; }

        public ObservableCollection<IAction> Body { get; } = [];

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

        public void Cancel()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the child actions from the Body collection.
        /// </summary>
        /// <returns>The collection of child actions in the Body.</returns>
        public ObservableCollection<IAction> GetChildActions()
        {
            return Body;
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
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
            foreach (var action in Body)
            {
                await action.ExecuteAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {

                    return false;
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
                errors.Add(new SyntaxError(this, "While loop condition cannot be null", "Condition", SyntaxErrorSeverity.Critical));
            }
            else if (Condition is not bool && Condition is not ExpressionBase<bool>)
            {
                errors.Add(new SyntaxError(this,
                    $"While loop condition must be a boolean value or Expression<bool>, but found {Condition.GetType().Name}",
                    "Condition",
                    SyntaxErrorSeverity.Critical)
                {
                    Context = Condition.GetType()
                });
            }

            // Validate body
            if (Body == null)
            {
                errors.Add(new SyntaxError(this, "While loop body cannot be null", "Body", SyntaxErrorSeverity.Critical));
            }
            else if (Body.Count == 0)
            {
                errors.Add(new SyntaxError(this, "While loop body is empty - infinite loop if condition is true", "Body", SyntaxErrorSeverity.Warning));
            }

            return errors;
        }
    }
}
