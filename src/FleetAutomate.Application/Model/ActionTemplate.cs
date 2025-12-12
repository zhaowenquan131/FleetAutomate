using System;

namespace FleetAutomate.Model
{
    /// <summary>
    /// Represents an action template for the ToolBox.
    /// </summary>
    public class ActionTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Type ActionType { get; set; } = typeof(object);
        public string Description { get; set; } = string.Empty;

        public ActionTemplate(string name, string category, string icon, Type actionType, string description = "")
        {
            Name = name;
            Category = category;
            Icon = icon;
            ActionType = actionType;
            Description = description;
        }

        public ActionTemplate() { }
    }
}