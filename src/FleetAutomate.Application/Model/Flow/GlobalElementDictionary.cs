using FlaUI.Core.AutomationElements;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace FleetAutomate.Model.Flow
{
    /// <summary>
    /// A dictionary that stores UI automation elements by key.
    /// Keys are defined at design time, values (AutomationElements) are populated at runtime.
    /// </summary>
    [DataContract]
    public class GlobalElementDictionary
    {
        /// <summary>
        /// The registered element keys. Keys are stored for serialization,
        /// actual AutomationElement values are populated at runtime.
        /// </summary>
        [DataMember]
        public ObservableCollection<string> RegisteredKeys { get; set; } = [];

        /// <summary>
        /// Runtime dictionary mapping keys to AutomationElements.
        /// Not serialized - populated during flow execution.
        /// </summary>
        [IgnoreDataMember]
        private Dictionary<string, AutomationElement?>? _elements = [];

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            _elements = [];
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            _elements ??= [];
            RegisteredKeys ??= [];
        }

        private Dictionary<string, AutomationElement?> Elements => _elements ??= [];

        /// <summary>
        /// Gets the AutomationElement for the specified key.
        /// </summary>
        /// <param name="key">The element key.</param>
        /// <returns>The AutomationElement if found and set, null otherwise.</returns>
        public AutomationElement? GetElement(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            return Elements.TryGetValue(key, out var element) ? element : null;
        }

        /// <summary>
        /// Sets the AutomationElement for the specified key.
        /// If the key doesn't exist in RegisteredKeys, it will be added.
        /// </summary>
        /// <param name="key">The element key.</param>
        /// <param name="element">The AutomationElement to store.</param>
        public void SetElement(string key, AutomationElement? element)
        {
            if (string.IsNullOrEmpty(key))
                return;

            Elements[key] = element;

            // Add to registered keys if not already present
            if (!RegisteredKeys.Contains(key))
            {
                RegisteredKeys.Add(key);
            }
        }

        /// <summary>
        /// Registers a key without setting an element value.
        /// Used at design time to define available scope options.
        /// </summary>
        /// <param name="key">The element key to register.</param>
        public void RegisterKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!RegisteredKeys.Contains(key))
            {
                RegisteredKeys.Add(key);
            }
        }

        /// <summary>
        /// Removes a key and its associated element.
        /// </summary>
        /// <param name="key">The element key to remove.</param>
        /// <returns>True if the key was found and removed, false otherwise.</returns>
        public bool RemoveKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            Elements.Remove(key);
            return RegisteredKeys.Remove(key);
        }

        /// <summary>
        /// Checks if a key is registered.
        /// </summary>
        /// <param name="key">The element key.</param>
        /// <returns>True if the key is registered, false otherwise.</returns>
        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return RegisteredKeys.Contains(key);
        }

        /// <summary>
        /// Checks if an element has been set for the specified key.
        /// </summary>
        /// <param name="key">The element key.</param>
        /// <returns>True if an element has been set, false otherwise.</returns>
        public bool HasElement(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return Elements.TryGetValue(key, out var element) && element != null;
        }

        /// <summary>
        /// Clears all runtime element values while preserving registered keys.
        /// Called when resetting the flow for a new execution.
        /// </summary>
        public void ClearRuntimeElements()
        {
            Elements.Clear();
        }

        /// <summary>
        /// Clears everything including registered keys.
        /// </summary>
        public void Clear()
        {
            Elements.Clear();
            RegisteredKeys.Clear();
        }

        /// <summary>
        /// Gets all registered keys.
        /// </summary>
        public IEnumerable<string> Keys => RegisteredKeys;

        /// <summary>
        /// Gets the number of registered keys.
        /// </summary>
        public int Count => RegisteredKeys.Count;
    }
}
