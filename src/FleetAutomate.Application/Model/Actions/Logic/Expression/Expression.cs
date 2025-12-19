using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace FleetAutomate.Model.Actions.Logic
{
    [DataContract]
    public abstract class ExpressionBase<TResult>
    {
        [DataMember]
        public string RawText { get; set; }

        [DataMember]
        public TResult Result { get; set; }

        /// <summary>
        /// Environment for resolving variables. Not serialized.
        /// </summary>
        [IgnoreDataMember]
        public Environment? Environment { get; set; }

        public abstract void Evaluate();
    }
}
