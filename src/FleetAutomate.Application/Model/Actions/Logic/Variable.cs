using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model.Actions.Logic
{
    [DataContract]
    public class Variable
    {
        [DataMember]
        public string Name { get; set; } = string.Empty;


        [DataMember]
        public object? Value { get; set; }


        public Type Type { get; set; } = typeof(object);

        /// <summary>
        /// Type name for XML serialization.
        /// </summary>
        [DataMember]
        public string TypeName
        {
            get => Type?.AssemblyQualifiedName ?? typeof(object).AssemblyQualifiedName!;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    Type = Type.GetType(value) ?? typeof(object);
                }
            }
        }

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public Variable()
        {
        }

        /// <summary>
        /// Constructor with parameters.
        /// </summary>
        public Variable(string name, object? value, Type type)
        {
            Name = name;
            Value = value;
            Type = type;
        }
    }
}
