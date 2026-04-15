using FleetAutomate.Model.Flow;

using System.Runtime.Serialization;

namespace FleetAutomate.Model.Actions.Logic
{
    [DataContract]
    public partial class SetVariableAction<TResult> : ILogicAction
    {
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
        public ActionState State { get; set; } = ActionState.Ready;

        public string Name => $"Set {Variable.ShortTypeName} {Variable.Name} = {Variable.Value}";

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

                if (existingVariable != null)
                {
                    existingVariable.Value = Variable.Value;
                    existingVariable.Type = Variable.Type;
                    Variable = existingVariable;
                }
                else
                {
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
    }
}
