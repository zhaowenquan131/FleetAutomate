using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;

using FleetAutomate.Model.Flow;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;


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
    public class ForLoopAction : ILogicAction, ISyntaxValidator, ICompositeAction, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        [DataMember]
        private Environment _environment;
        public Environment Environment
        {
            get => _environment;
            set
            {
                if (_environment != value)
                {
                    _environment = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Environment)));
                }
            }
        }

        [DataMember]
        private string _name = "For Loop";
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        [DataMember]
        private string _description = "For loop with initialization, condition, and increment";
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

        [DataMember]
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
                }
            }
        }

        [DataMember]
        private object _initialization;
        public object Initialization
        {
            get => _initialization;
            set
            {
                if (_initialization != value)
                {
                    _initialization = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Initialization)));
                }
            }
        }

        [DataMember]
        private object _condition;
        public object Condition
        {
            get => _condition;
            set
            {
                if (_condition != value)
                {
                    _condition = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Condition)));
                }
            }
        }

        [DataMember]
        private object _increment;
        public object Increment
        {
            get => _increment;
            set
            {
                if (_increment != value)
                {
                    _increment = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Increment)));
                }
            }
        }

        public ObservableCollection<IAction> Body { get; } = [];

        [DataMember]
        private ActionState _state;
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
