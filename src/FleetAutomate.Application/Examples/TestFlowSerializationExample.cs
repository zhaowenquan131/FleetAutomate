using Canvas.TestRunner.Model.Actions.Logic;
using Canvas.TestRunner.Model.Flow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Examples
{
    /// <summary>
    /// Example demonstrating TestFlow XML serialization capabilities.
    /// </summary>
    public static class TestFlowXmlSerializationExample
    {
        /// <summary>
        /// Creates a sample TestFlow for serialization demonstration.
        /// </summary>
        public static TestFlow CreateSampleTestFlow()
        {
            var testFlow = new TestFlow()
            {
                Name = "Sample Test Flow",
                Description = "A demonstration of TestFlow serialization",
                IsEnabled = true
            };

            // Add some sample variables to environment
            testFlow.Environment.Variables.Add(new Variable("counter", 0, typeof(int)));
            testFlow.Environment.Variables.Add(new Variable("message", "Hello World", typeof(string)));

            return testFlow;
        }

        /// <summary>
        /// Demonstrates XML serialization of a TestFlow.
        /// </summary>
        public static string SerializeTestFlowExample()
        {
            var testFlow = CreateSampleTestFlow();
            
            // Serialize using the extension method
            return testFlow.ToXml();
        }

        /// <summary>
        /// Demonstrates XML deserialization of a TestFlow.
        /// </summary>
        public static TestFlow? DeserializeTestFlowExample(string xml)
        {
            // Deserialize using the extension method
            var testFlow = TestFlowXmlExtensions.FromXml(xml);
            
            // The InitializeAfterDeserialization is called automatically
            // by the FromXml method to ensure runtime objects are properly initialized
            
            return testFlow;
        }

        /// <summary>
        /// Demonstrates round-trip serialization (serialize then deserialize).
        /// </summary>
        public static (TestFlow original, TestFlow roundtrip, string xml) RoundTripExample()
        {
            // Create original
            var original = CreateSampleTestFlow();
            
            // Serialize
            var xml = original.ToXml();
            
            // Deserialize
            var roundtrip = TestFlowXmlExtensions.FromXml(xml);
            
            return (original, roundtrip!, xml);
        }

        /// <summary>
        /// Demonstrates file-based XML serialization.
        /// </summary>
        public static void FileSerializationExample(string filePath)
        {
            var testFlow = CreateSampleTestFlow();
            
            // Save to XML file
            testFlow.SaveToXmlFile(filePath);
            
            // Load from XML file
            var loadedFlow = TestFlowXmlExtensions.LoadFromXmlFile(filePath);
            
            Console.WriteLine($"Saved and loaded TestFlow: {loadedFlow?.Name}");
        }

        /// <summary>
        /// Example XML output for documentation purposes.
        /// </summary>
        public static void PrintExampleXml()
        {
            var xml = SerializeTestFlowExample();
            Console.WriteLine("TestFlow XML Example:");
            Console.WriteLine(xml);
        }

        /// <summary>
        /// Demonstrates XML validation.
        /// </summary>
        public static bool ValidateXmlExample(string xml)
        {
            return TestFlowXmlSerializer.ValidateXml(xml);
        }
    }
}