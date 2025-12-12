using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Canvas.TestRunner.Model.Actions.Logic
{
    [XmlRoot("Environment")]
    public class Environment
    {
        public Environment()
        {

        }
        /// <summary>
        /// Gets or sets the list of variables in the environment.
        /// </summary>
        [XmlArray("Variables")]
        [XmlArrayItem("Variable")]
        public List<Variable> Variables { get; set; } = [];
        
        /// <summary>
        /// Gets or sets the list of actions in the environment.
        /// </summary>
        [XmlArray("Actions")]
        [XmlArrayItem("Action")]
        public List<IAction<object>> Actions { get; set; } = [];
        
    }
}
