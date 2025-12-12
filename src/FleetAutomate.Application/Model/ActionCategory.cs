using System.Collections.ObjectModel;

namespace FleetAutomate.Model
{
    /// <summary>
    /// Represents a category of actions in the toolbox hierarchy.
    /// </summary>
    public class ActionCategory
    {
        /// <summary>
        /// Gets or sets the name of the category.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the icon (emoji) representing the category.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Gets the collection of actions in this category.
        /// </summary>
        public ObservableCollection<ActionTemplate> Actions { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionCategory"/> class.
        /// </summary>
        /// <param name="name">The category name.</param>
        /// <param name="icon">The category icon.</param>
        public ActionCategory(string name, string icon)
        {
            Name = name;
            Icon = icon;
            Actions = new ObservableCollection<ActionTemplate>();
        }

        /// <summary>
        /// Parameterless constructor for serialization/binding.
        /// </summary>
        public ActionCategory()
        {
            Name = string.Empty;
            Icon = string.Empty;
            Actions = new ObservableCollection<ActionTemplate>();
        }
    }
}
