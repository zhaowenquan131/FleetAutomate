using Canvas.TestRunner.Model.Actions.Logic;
using Canvas.TestRunner.Model.Flow;
using Canvas.TestRunner.Model.Project;
using Canvas.TestRunner.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Examples
{
    /// <summary>
    /// Example demonstrating TestProject XML serialization capabilities.
    /// Shows how TestProject serializes only file names instead of full TestFlow objects.
    /// </summary>
    public static class TestProjectSerializationExample
    {
        /// <summary>
        /// Creates a sample TestProject for serialization demonstration.
        /// </summary>
        public static TestProject CreateSampleTestProject()
        {
            var testProject = new TestProject();

            // Create sample TestFlows with file names
            var testFlow1 = new TestFlow()
            {
                Name = "Login Test Flow",
                Description = "Tests user login functionality",
                IsEnabled = true,
                FileName = @"C:\TestFlows\LoginTest.xml"
            };

            var testFlow2 = new TestFlow()
            {
                Name = "Registration Test Flow", 
                Description = "Tests user registration functionality",
                IsEnabled = true,
                FileName = @"C:\TestFlows\RegistrationTest.xml"
            };

            var testFlow3 = new TestFlow()
            {
                Name = "Shopping Cart Test Flow",
                Description = "Tests shopping cart operations",
                IsEnabled = true,
                FileName = @"C:\TestFlows\ShoppingCartTest.xml"
            };

            // Add some sample variables to environments
            testFlow1.Environment.Variables.Add(new Variable("username", "testuser", typeof(string)));
            testFlow1.Environment.Variables.Add(new Variable("password", "testpass", typeof(string)));

            testFlow2.Environment.Variables.Add(new Variable("email", "test@example.com", typeof(string)));
            testFlow2.Environment.Variables.Add(new Variable("age", 25, typeof(int)));

            testFlow3.Environment.Variables.Add(new Variable("itemCount", 3, typeof(int)));
            testFlow3.Environment.Variables.Add(new Variable("totalPrice", 99.99, typeof(double)));

            testProject.TestFlows.Add(testFlow1);
            testProject.TestFlows.Add(testFlow2);
            testProject.TestFlows.Add(testFlow3);

            return testProject;
        }

        /// <summary>
        /// Demonstrates XML serialization of a TestProject.
        /// The XML will contain only file names, not the full TestFlow objects.
        /// </summary>
        public static string SerializeTestProjectExample()
        {
            var testProject = CreateSampleTestProject();
            
            // Serialize using the extension method
            return testProject.ToXml();
        }

        /// <summary>
        /// Demonstrates XML deserialization of a TestProject.
        /// </summary>
        public static TestProject? DeserializeTestProjectExample(string xml)
        {
            // Deserialize using the extension method
            var testProject = TestProjectXmlExtensions.FromXml(xml);
            
            // Note: At this point, TestFlows contain only placeholder objects with file names
            // Call LoadTestFlowsFromFiles() to load the actual TestFlow content from files
            
            return testProject;
        }

        /// <summary>
        /// Demonstrates round-trip serialization (serialize then deserialize).
        /// </summary>
        public static (TestProject original, TestProject roundtrip, string xml) RoundTripExample()
        {
            // Create original
            var original = CreateSampleTestProject();
            
            // Serialize (only file names will be serialized)
            var xml = original.ToXml();
            
            // Deserialize (creates placeholder TestFlows with file names)
            var roundtrip = TestProjectXmlExtensions.FromXml(xml);
            
            return (original, roundtrip!, xml);
        }

        /// <summary>
        /// Demonstrates complete project and TestFlow file management.
        /// </summary>
        public static void CompleteProjectExample(string projectFilePath, string testFlowDirectory)
        {
            var testProject = CreateSampleTestProject();
            
            // Update file paths to use the specified directory
            foreach (var testFlow in testProject.TestFlows)
            {
                var fileName = Path.GetFileName(testFlow.FileName);
                testFlow.FileName = Path.Combine(testFlowDirectory, fileName);
            }
            
            // Ensure directory exists
            Directory.CreateDirectory(testFlowDirectory);
            
            // Save project and all TestFlows
            testProject.SaveProjectAndTestFlows(projectFilePath);
            
            Console.WriteLine($"Saved project to: {projectFilePath}");
            Console.WriteLine($"Saved TestFlows to: {testFlowDirectory}");
            
            // Load project back
            var loadedProject = TestProjectXmlExtensions.LoadFromXmlFile(projectFilePath);
            
            Console.WriteLine($"Loaded project with {loadedProject?.TestFlows.Count} TestFlows");
        }

        /// <summary>
        /// Example showing the XML structure for documentation purposes.
        /// </summary>
        public static void PrintExampleXml()
        {
            var xml = SerializeTestProjectExample();
            Console.WriteLine("TestProject XML Example (showing only file names):");
            Console.WriteLine(xml);
        }

        /// <summary>
        /// Demonstrates XML validation.
        /// </summary>
        public static bool ValidateXmlExample(string xml)
        {
            return TestProjectXmlSerializer.ValidateXml(xml);
        }

        /// <summary>
        /// Example showing how to work with individual TestFlow files.
        /// </summary>
        public static void WorkWithIndividualTestFlows(TestProject project)
        {
            foreach (var testFlow in project.TestFlows)
            {
                Console.WriteLine($"TestFlow: {testFlow.Name}");
                Console.WriteLine($"File: {testFlow.FileName}");
                
                if (!string.IsNullOrEmpty(testFlow.FileName) && File.Exists(testFlow.FileName))
                {
                    // Load full TestFlow from file
                    var fullTestFlow = TestFlowXmlExtensions.LoadFromXmlFile(testFlow.FileName);
                    if (fullTestFlow != null)
                    {
                        Console.WriteLine($"  Actions: {fullTestFlow.Actions.Count}");
                        Console.WriteLine($"  Variables: {fullTestFlow.Environment.Variables.Count}");
                    }
                }
                else
                {
                    Console.WriteLine("  File not found - placeholder only");
                }
                
                Console.WriteLine();
            }
        }
    }
}