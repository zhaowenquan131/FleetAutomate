using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FleetAutomate.Model.Actions.Logic
{
    public interface ILogicAction : IAction
    {
        public Environment Environment { get; set; }

    }
}
