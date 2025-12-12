using Canvas.TestRunner.Model.Flow;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model
{

    public interface IAction
    {
        /// <summary>
        /// Gets the name of the action.
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Gets the description of the action.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Executes the action.
        /// </summary>

        public ActionState State { get; set; }

        Task<bool> ExecuteAsync(CancellationToken cancellationToken);

        void Cancel();
        /// <summary>
        /// Gets a value indicating whether the action is enabled.
        /// </summary>
        bool IsEnabled { get; }
    }
    public interface IAction<TResult> : IAction
    {

        public TResult Result { get; set; }

    }
}
