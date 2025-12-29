using FleetAutomate.Model.Flow;

namespace FleetAutomate.Model.Actions.UIAutomation
{
    /// <summary>
    /// Interface for UI automation actions that interact with UI elements.
    /// Provides common properties for element identification and search scope.
    /// </summary>
    public interface IUIElementAction : IAction
    {
        /// <summary>
        /// The identifier to search for (XPath, AutomationId, Name, or ClassName).
        /// </summary>
        string ElementIdentifier { get; set; }

        /// <summary>
        /// The type of identifier: "XPath", "AutomationId", "Name", "ClassName".
        /// </summary>
        string IdentifierType { get; set; }

        /// <summary>
        /// The key in the GlobalElementDictionary to use as the search root.
        /// If null or empty, search from desktop.
        /// </summary>
        string? SearchScope { get; set; }

        /// <summary>
        /// Whether to add the found element to the GlobalElementDictionary.
        /// </summary>
        bool AddToGlobalDictionary { get; set; }

        /// <summary>
        /// The key to use when adding to the GlobalElementDictionary.
        /// Defaults to ElementIdentifier if not specified.
        /// </summary>
        string? GlobalDictionaryKey { get; set; }

        /// <summary>
        /// Reference to the TestFlow's GlobalElementDictionary.
        /// Set at runtime before execution.
        /// </summary>
        GlobalElementDictionary? ElementDictionary { get; set; }
    }
}
