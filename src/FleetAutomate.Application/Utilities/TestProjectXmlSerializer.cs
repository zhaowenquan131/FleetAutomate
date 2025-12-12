using FleetAutomate.Model.Project;

using FleetAutomate.Model.Flow;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FleetAutomate.Utilities
{
    /// <summary>
    /// Custom DataContract serialization utilities for TestProject to handle project file operations.
    /// </summary>
    public static class TestProjectXmlSerializer
    {
        /// <summary>
        /// Gets all known types that can appear in TestProject serialization.
        /// </summary>
        /// <returns>Array of all known types.</returns>
        private static Type[] GetKnownTypes() => [
                typeof(TestFlow),
                typeof(List<TestFlow>),
                // Action types from TestFlow's previous KnownType attributes
                typeof(Model.Actions.Logic.SetVariableAction<object>),
                typeof(Model.Actions.Logic.Loops.WhileLoopAction),
                typeof(Model.Actions.Logic.Loops.ForLoopAction),
                typeof(Model.Actions.Logic.IfAction),
                // UI Automation and System actions
                typeof(Model.Actions.System.LaunchApplicationAction),
                typeof(Model.Actions.UIAutomation.WaitForElementAction),
                typeof(Model.Actions.UIAutomation.ClickElementAction)
            ];

        /// <summary>
        /// Creates a configured DataContractSerializer for TestProject.
        /// </summary>
        /// <returns>Configured DataContractSerializer instance.</returns>
        public static DataContractSerializer CreateSerializer()
        {
            try
            {
                var knownTypes = GetKnownTypes();
                var settings = new DataContractSerializerSettings
                {
                    KnownTypes = knownTypes,
                    PreserveObjectReferences = false, // Set to false for simpler serialization
                    SerializeReadOnlyTypes = true
                };
                return new DataContractSerializer(typeof(TestProject), settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataContractSerializer creation failed: {ex.Message}");
                throw new InvalidOperationException($"Failed to create DataContractSerializer: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates XmlWriterSettings configured for TestProject serialization.
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
        /// Creates XmlReaderSettings configured for TestProject deserialization.
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
        /// Serializes a TestProject to XML string.
        /// </summary>
        /// <param name="testProject">The TestProject to serialize.</param>
        /// <returns>XML string representation of the TestProject.</returns>
        public static string SerializeToXml(TestProject testProject)
        {
            if (testProject == null)
                throw new ArgumentNullException(nameof(testProject));

            try
            {
                var serializer = CreateSerializer();
                var settings = CreateWriterSettings();

                using var stringWriter = new StringWriter();
                using var xmlWriter = XmlWriter.Create(stringWriter, settings);
                
                serializer.WriteObject(xmlWriter, testProject);
                xmlWriter.Flush();
                
                string xml = stringWriter.ToString();
                return xml;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TestProject serialization failed: {ex.Message}");
                throw new InvalidOperationException($"Failed to serialize TestProject: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deserializes a TestProject from XML string.
        /// </summary>
        /// <param name="xml">The XML string to deserialize.</param>
        /// <returns>Deserialized TestProject instance.</returns>
        public static TestProject? DeserializeFromXml(string xml)
        {
            if (string.IsNullOrEmpty(xml))
                throw new ArgumentException("XML cannot be null or empty", nameof(xml));

            var serializer = CreateSerializer();
            var settings = CreateReaderSettings();

            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            
            var testProject = (TestProject?)serializer.ReadObject(xmlReader);
            return testProject;
        }

        /// <summary>
        /// Serializes a TestProject to XML file.
        /// </summary>
        /// <param name="testProject">The TestProject to serialize.</param>
        /// <param name="filePath">The file path to write to.</param>
        public static void SerializeToFile(TestProject testProject, string filePath)
        {
            if (testProject == null)
                throw new ArgumentNullException(nameof(testProject));
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            var xml = SerializeToXml(testProject);
            File.WriteAllText(filePath, xml, Encoding.UTF8);
        }

        /// <summary>
        /// Deserializes a TestProject from XML file.
        /// </summary>
        /// <param name="filePath">The file path to read from.</param>
        /// <returns>Deserialized TestProject instance.</returns>
        public static TestProject? DeserializeFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var xml = File.ReadAllText(filePath, Encoding.UTF8);
            return DeserializeFromXml(xml);
        }

        /// <summary>
        /// Validates XML against TestProject schema (basic validation).
        /// </summary>
        /// <param name="xml">The XML string to validate.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public static bool ValidateXml(string xml)
        {
            try
            {
                var testProject = DeserializeFromXml(xml);
                return testProject != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Extension methods for TestProject XML serialization.
    /// </summary>
    public static class TestProjectXmlExtensions
    {
        /// <summary>
        /// Serializes a TestProject to XML string.
        /// </summary>
        /// <param name="testProject">The TestProject to serialize.</param>
        /// <returns>XML string representation of the TestProject.</returns>
        public static string ToXml(this TestProject testProject)
        {
            return TestProjectXmlSerializer.SerializeToXml(testProject);
        }

        /// <summary>
        /// Deserializes a TestProject from XML string.
        /// </summary>
        /// <param name="xml">The XML string to deserialize.</param>
        /// <returns>Deserialized TestProject instance.</returns>
        public static TestProject? FromXml(string xml)
        {
            return TestProjectXmlSerializer.DeserializeFromXml(xml);
        }

        /// <summary>
        /// Saves a TestProject to XML file.
        /// </summary>
        /// <param name="testProject">The TestProject to save.</param>
        /// <param name="filePath">The file path to save to.</param>
        public static void SaveToXmlFile(this TestProject testProject, string filePath)
        {
            TestProjectXmlSerializer.SerializeToFile(testProject, filePath);
        }

        /// <summary>
        /// Loads a TestProject from XML file.
        /// </summary>
        /// <param name="filePath">The file path to load from.</param>
        /// <returns>Loaded TestProject instance.</returns>
        public static TestProject? LoadFromXmlFile(string filePath)
        {
            var project = TestProjectXmlSerializer.DeserializeFromFile(filePath);
            if (project != null)
            {
                // Get project directory and load TestFlows from their .testfl files
                var projectDirectory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(projectDirectory))
                {
                    project.LoadTestFlowsFromFiles(projectDirectory);
                }
            }
            return project;
        }

        /// <summary>
        /// Saves a TestProject and all its TestFlows to their respective files.
        /// Project is saved to .testproj file, TestFlows are saved to individual .testfl files.
        /// </summary>
        /// <param name="testProject">The TestProject to save.</param>
        /// <param name="projectFilePath">The project file path to save to (.testproj).</param>
        public static void SaveProjectAndTestFlows(this TestProject testProject, string projectFilePath)
        {
            if (string.IsNullOrEmpty(projectFilePath))
                throw new ArgumentException("Project file path cannot be null or empty", nameof(projectFilePath));

            // Get project directory
            var projectDirectory = Path.GetDirectoryName(projectFilePath);
            if (string.IsNullOrEmpty(projectDirectory))
                throw new ArgumentException("Invalid project file path", nameof(projectFilePath));

            // Ensure project directory exists
            Directory.CreateDirectory(projectDirectory);

            // Save all TestFlows to their individual .testfl files first
            testProject.SaveTestFlowsToFiles(projectDirectory);

            // Then save the project file with just the file references
            testProject.SaveToXmlFile(projectFilePath);
        }
    }
}