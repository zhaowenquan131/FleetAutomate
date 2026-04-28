using FleetAutomate.Model.Flow;
using FleetAutomate.Expressions;

using System.ComponentModel;
using System.Runtime.Serialization;

namespace FleetAutomate.Model.Actions.Logic
{
    [DataContract]
    public enum SetVariableValueMode
    {
        [EnumMember]
        Literal,

        [EnumMember]
        Expression
    }

    [DataContract]
    public partial class SetVariableAction<TResult> : ILogicAction, INotifyPropertyChanged
    {
        private ActionState _state = ActionState.Ready;

        public event PropertyChangedEventHandler? PropertyChanged;

        public SetVariableAction() { }
        public SetVariableAction(string name, TResult value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Variable name cannot be null or empty.", nameof(name));
            }
            else
            {
                // Only look up existing variable if Environment is already set
                // Environment will be set via property initialization after constructor completes
                Variable? variable = Environment?.Variables.FirstOrDefault(v => v.Name.Equals(name));
                if (variable != null)
                {
                    variable.Value = value;
                    Variable = variable;
                }
                else
                {
                    Variable = new Variable
                    {
                        Name = name,
                        Value = value,
                        Type = typeof(TResult)
                    };
                }
            }
        }

        [DataMember]
        public string Description { get; set; } = "Leave your comment here.";

        [DataMember]
        public Variable Variable { get; set; }
        
        [IgnoreDataMember]
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

        public string Name => $"Set {Variable.ShortTypeName} {Variable.Name} = {Variable.Value}";

        [DataMember]
        public SetVariableValueMode ValueMode { get; set; } = SetVariableValueMode.Literal;

        [DataMember]
        public string ExpressionText { get; set; } = string.Empty;

        public TResult Result
        {
            get => (TResult)Variable.Value;
            set
            {
                throw new NotImplementedException($"Result property is not implemented in {nameof(SetVariableAction<TResult>)}.");
            }
        }

        [DataMember]
        public required Environment Environment { get; set; }
        [DataMember]
        public bool IsEnabled { get; set; } = true;

        public void Cancel()
        {
            // Variable assignment is atomic and doesn't support in-flight cancellation.
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            State = ActionState.Running;
            // Yield to allow UI to update the action state immediately
            await Task.Yield();

            try
            {
                var existingVariable = Environment.Variables.FirstOrDefault(v =>
                    string.Equals(v.Name, Variable.Name, StringComparison.Ordinal));
                var value = await ResolveValueAsync(cancellationToken);

                if (existingVariable != null)
                {
                    existingVariable.Value = value;
                    existingVariable.Type = Variable.Type;
                    Variable = existingVariable;
                }
                else
                {
                    Variable.Value = value;
                    Environment.Variables.Add(Variable);
                }

                State = ActionState.Completed;
                return true;
            }
            catch
            {
                State = ActionState.Failed;
                return false;
            }
        }

        private async Task<object?> ResolveValueAsync(CancellationToken cancellationToken)
        {
            if (ValueMode != SetVariableValueMode.Expression)
            {
                return Variable.Value;
            }

            var engine = new SimpleExpressionEngine();
            var result = await engine.EvaluateAsync(ExpressionText, new ExpressionContext(Environment), cancellationToken);
            return ConvertToVariableType(result.Value, Variable.Type);
        }

        private static object? ConvertToVariableType(object? value, Type targetType)
        {
            if (value == null || targetType == typeof(object))
            {
                return value;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, Convert.ToString(value) ?? string.Empty, ignoreCase: true);
            }

            return Convert.ChangeType(value, targetType, global::System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
