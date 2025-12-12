using Canvas.TestRunner.Model.Actions.Logic;
using Canvas.TestRunner.Model.Actions.Logic.Loops;
using Canvas.TestRunner.Model.Actions.System;
using Canvas.TestRunner.Model.Actions.UIAutomation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Canvas.TestRunner.Model.Flow
{
    /// <summary>
    /// Custom DataContract serialization utilities for TestFlow to handle interface serialization.
    /// </summary>
    public static class TestFlowXmlSerializer
    {
        /// <summary>
        /// Gets all known action types that can appear in IAction arrays.
        /// </summary>
        /// <returns>Array of all known action types.</returns>
        private static Type[] GetKnownActionTypes()
        {
            return
            [
                typeof(SetVariableAction<object>),
                typeof(WhileLoopAction),
                typeof(ForLoopAction),
                typeof(IfAction),
                typeof(TestFlow),
                typeof(LaunchApplicationAction),
                typeof(WaitForElementAction),
                typeof(ClickElementAction)
            ];
        }

        /// <summary>
        /// Creates a configured DataContractSerializer for TestFlow.
        /// </summary>
        /// <returns>Configured DataContractSerializer instance.</returns>
        public static DataContractSerializer CreateSerializer()
        {
            try
            {
                var knownTypes = GetKnownActionTypes();
                var settings = new DataContractSerializerSettings
                {
                    KnownTypes = knownTypes,
                    PreserveObjectReferences = true,
                    SerializeReadOnlyTypes = true
                };
                return new DataContractSerializer(typeof(TestFlow), settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataContractSerializer creation failed: {ex.Message}");
                throw new InvalidOperationException($"Failed to create DataContractSerializer: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates XmlWriterSettings configured for TestFlow serialization.
        /// </summary>
        /// <returns>Configured XmlWriterSettings.</returns>
        public static XmlWriterSettings CreateWriterSettings()
        {
            return new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false,
                NewLineOnAttributes = false
            };
        }

        /// <summary>
        /// Creates XmlReaderSettings configured for TestFlow deserialization.
        /// </summary>
        /// <returns>Configured XmlReaderSettings.</returns>
        public static XmlReaderSettings CreateReaderSettings()
        {
            return new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                CloseInput = false
            };
        }

        /// <summary>
        /// Serializes a TestFlow to XML string.
        /// </summary>
        /// <param name="testFlow">The TestFlow to serialize.</param>
        /// <returns>XML string representation of the TestFlow.</returns>
        public static string SerializeToXml(TestFlow testFlow)
        {
            if (testFlow == null)
                throw new ArgumentNullException(nameof(testFlow));

            try
            {
                var serializer = CreateSerializer();
                var settings = CreateWriterSettings();

                using var stringWriter = new StringWriter();
                using var xmlWriter = XmlWriter.Create(stringWriter, settings);

                serializer.WriteObject(xmlWriter, testFlow);
                xmlWriter.Flush(); // CRITICAL: Flush the XmlWriter before reading from StringWriter

                return stringWriter.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TestFlow serialization failed: {ex.Message}");
                throw new InvalidOperationException($"Failed to serialize TestFlow: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deserializes a TestFlow from XML string.
        /// </summary>
        /// <param name="xml">The XML string to deserialize.</param>
        /// <returns>Deserialized TestFlow instance.</returns>
        public static TestFlow? DeserializeFromXml(string xml)
        {
            if (string.IsNullOrEmpty(xml))
                throw new ArgumentException("XML cannot be null or empty", nameof(xml));

            var serializer = CreateSerializer();
            var settings = CreateReaderSettings();

            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            
            var testFlow = (TestFlow?)serializer.ReadObject(xmlReader);
            testFlow?.InitializeAfterDeserialization();
            return testFlow;
        }

        /// <summary>
        /// Serializes a TestFlow to XML file.
        /// </summary>
        /// <param name="testFlow">The TestFlow to serialize.</param>
        /// <param name="filePath">The file path to write to.</param>
        public static void SerializeToFile(TestFlow testFlow, string filePath)
        {
            if (testFlow == null)
                throw new ArgumentNullException(nameof(testFlow));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            var xml = SerializeToXml(testFlow);
            File.WriteAllText(filePath, xml, Encoding.UTF8);
        }

        /// <summary>
        /// Deserializes a TestFlow from XML file.
        /// </summary>
        /// <param name="filePath">The file path to read from.</param>
        /// <returns>Deserialized TestFlow instance.</returns>
        public static TestFlow? DeserializeFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var xml = File.ReadAllText(filePath, Encoding.UTF8);
            return DeserializeFromXml(xml);
        }

        /// <summary>
        /// Validates XML against TestFlow schema (basic validation).
        /// </summary>
        /// <param name="xml">The XML string to validate.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public static bool ValidateXml(string xml)
        {
            try
            {
                var testFlow = DeserializeFromXml(xml);
                return testFlow != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Extension methods for TestFlow XML serialization.
    /// </summary>
    public static class TestFlowXmlExtensions
    {
        /// <summary>
        /// Serializes a TestFlow to XML string.
        /// </summary>
        /// <param name="testFlow">The TestFlow to serialize.</param>
        /// <returns>XML string representation of the TestFlow.</returns>
        public static string ToXml(this TestFlow testFlow)
        {
            return TestFlowXmlSerializer.SerializeToXml(testFlow);
        }

        /// <summary>
        /// Deserializes a TestFlow from XML string.
        /// </summary>
        /// <param name="xml">The XML string to deserialize.</param>
        /// <returns>Deserialized TestFlow instance.</returns>
        public static TestFlow? FromXml(string xml)
        {
            return TestFlowXmlSerializer.DeserializeFromXml(xml);
        }

        /// <summary>
        /// Saves a TestFlow to XML file.
        /// </summary>
        /// <param name="testFlow">The TestFlow to save.</param>
        /// <param name="filePath">The file path to save to.</param>
        public static void SaveToXmlFile(this TestFlow testFlow, string filePath)
        {
            TestFlowXmlSerializer.SerializeToFile(testFlow, filePath);
        }

        /// <summary>
        /// Loads a TestFlow from XML file.
        /// </summary>
        /// <param name="filePath">The file path to load from.</param>
        /// <returns>Loaded TestFlow instance.</returns>
        public static TestFlow? LoadFromXmlFile(string filePath)
        {
            return TestFlowXmlSerializer.DeserializeFromFile(filePath);
        }
    }
}