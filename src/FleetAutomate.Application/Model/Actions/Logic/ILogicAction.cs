using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model.Actions.Logic
{
    public interface ILogicAction : IAction
    {
        public Environment Environment { get; set; }

    }
}
