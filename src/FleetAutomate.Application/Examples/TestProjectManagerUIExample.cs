using Canvas.TestRunner.Model.Flow;
using Canvas.TestRunner.Model.Project;
using Canvas.TestRunner.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Examples
{
    /// <summary>
    /// Example demonstrating how to handle UI events for TestProjectManager.
    /// Shows proper implementation of save prompts and user interaction.
    /// </summary>
    public static class TestProjectManagerUIExample
    {
        /// <summary>
        /// Demonstrates how to set up a TestProjectManager with proper UI event handlers.
        /// </summary>
        public static void SetupProjectManagerWithUI()
        {
            Console.WriteLine("=== Setting up TestProjectManager with UI Events ===");

            var projectManager = new TestProjectManager();

            // Set up event handlers for UI interactions
            SetupUIEventHandlers(projectManager);

            // Set up status change event handlers
            SetupStatusEventHandlers(projectManager);

            // Demonstrate the functionality
            DemonstrateProjectLifecycle(projectManager);
        }

        /// <summary>
        /// Sets up UI event handlers for save prompts and file dialogs.
        /// </summary>
        private static void SetupUIEventHandlers(TestProjectManager projectManager)
        {
            // Handle save unsaved changes prompt
            projectManager.OnPromptSaveUnsavedChanges += () =>
            {
                Console.WriteLine("\n⚠️  You have unsaved changes. What would you like to do?");
                Console.WriteLine("1. Save changes");
                Console.WriteLine("2. Don't save changes");
                Console.WriteLine("3. Cancel operation");
                Console.Write("Enter your choice (1-3): ");

                var input = Console.ReadLine();
                return input switch
                {
                    "1" => SavePromptResponse.Save,
                    "2" => SavePromptResponse.DontSave,
                    "3" => SavePromptResponse.Cancel,
                    _ => SavePromptResponse.Cancel // Default to cancel for invalid input
                };
            };

            // Handle "Save As" dialog
            projectManager.OnPromptSaveAs += () =>
            {
                Console.WriteLine("\n💾 Choose a location to save your project:");
                Console.Write("Enter file path (or press Enter to cancel): ");
                
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    return null; // User cancelled
                }

                // Ensure .testproj extension
                if (!input.EndsWith(".testproj", StringComparison.OrdinalIgnoreCase))
                {
                    input += ".testproj";
                }

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(input);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch
                    {
                        Console.WriteLine($"❌ Cannot create directory: {directory}");
                        return null;
                    }
                }

                return input;
            };

            // Handle save failed scenario
            projectManager.OnSaveFailed += (errorMessage) =>
            {
                Console.WriteLine($"\n❌ Save Failed: {errorMessage}");
                Console.WriteLine("1. Continue anyway");
                Console.WriteLine("2. Cancel operation");
                Console.Write("Enter your choice (1-2): ");

                var input = Console.ReadLine();
                return input == "1" ? SaveFailedResponse.Continue : SaveFailedResponse.Cancel;
            };
        }

        /// <summary>
        /// Sets up event handlers for project status changes.
        /// </summary>
        private static void SetupStatusEventHandlers(TestProjectManager projectManager)
        {
            projectManager.OnProjectChanged += (project) =>
            {
                if (project != null)
                {
                    Console.WriteLine($"📁 Project loaded: {project.TestFlows.Count} TestFlows");
                }
                else
                {
                    Console.WriteLine("📁 No project loaded");
                }
            };

            projectManager.OnProjectPathChanged += (path) =>
            {
                Console.WriteLine($"📂 Project path: {path ?? "Not saved"}");
            };

            projectManager.OnUnsavedChangesChanged += (hasChanges) =>
            {
                Console.WriteLine($"💾 Unsaved changes: {(hasChanges ? "Yes" : "No")}");
            };

            projectManager.OnOperationFailed += (message, exception) =>
            {
                Console.WriteLine($"❌ Operation failed: {message}");
                Console.WriteLine($"   Error: {exception.Message}");
            };
        }

        /// <summary>
        /// Demonstrates the complete project lifecycle with user prompts.
        /// </summary>
        private static void DemonstrateProjectLifecycle(TestProjectManager projectManager)
        {
            Console.WriteLine("\n=== Project Lifecycle Demonstration ===");

            // Step 1: Create a new project
            Console.WriteLine("\n1. Creating new project...");
            projectManager.CreateNewProject("Demo Project");

            // Step 2: Add some content to make it have unsaved changes
            Console.WriteLine("\n2. Adding TestFlows to create unsaved changes...");
            projectManager.CreateNewTestFlow("Login Test", "Test user login functionality");
            projectManager.CreateNewTestFlow("Data Validation Test", "Test data validation");

            // Step 3: Try to create another project (should trigger save prompt)
            Console.WriteLine("\n3. Attempting to create another project (should trigger save prompt)...");
            var success = projectManager.CreateNewProject("Second Project");
            
            if (success)
            {
                Console.WriteLine("✅ Successfully created second project");
            }
            else
            {
                Console.WriteLine("❌ Second project creation was cancelled");
            }

            // Step 4: Demonstrate opening a project with unsaved changes
            if (projectManager.IsProjectLoaded && projectManager.HasUnsavedChanges)
            {
                Console.WriteLine("\n4. Attempting to open another project (should trigger save prompt)...");
                
                // Try to open a non-existent project (will fail, but shows the prompt flow)
                projectManager.OpenProject("NonExistent.testproj");
            }

            // Step 5: Demonstrate closing with unsaved changes
            if (projectManager.IsProjectLoaded)
            {
                Console.WriteLine("\n5. Attempting to close project (may trigger save prompt)...");
                var closed = projectManager.CloseProject();
                
                if (closed)
                {
                    Console.WriteLine("✅ Project closed successfully");
                }
                else
                {
                    Console.WriteLine("❌ Project close was cancelled");
                }
            }
        }

        /// <summary>
        /// Demonstrates advanced save scenarios.
        /// </summary>
        public static void DemonstrateAdvancedSaveScenarios()
        {
            Console.WriteLine("\n=== Advanced Save Scenarios ===");

            var projectManager = new TestProjectManager();
            SetupUIEventHandlers(projectManager);
            SetupStatusEventHandlers(projectManager);

            // Create a project without a file path
            projectManager.CreateNewProject("Unsaved Project");
            projectManager.CreateNewTestFlow("Test Flow", "A test flow");

            Console.WriteLine("\n1. Project created without file path - save prompt should offer 'Save As'");
            
            // Try to close (should trigger Save As dialog)
            var closed = projectManager.CloseProject();
            Console.WriteLine($"Close result: {(closed ? "Success" : "Cancelled")}");

            // Demonstrate save failure handling
            if (projectManager.IsProjectLoaded)
            {
                Console.WriteLine("\n2. Simulating save failure...");
                
                // Set up a handler that will cause save to fail
                projectManager.OnPromptSaveAs += () => "/invalid/path/that/will/fail.testproj";
                
                // Try to close again
                var closedAfterFailure = projectManager.CloseProject();
                Console.WriteLine($"Close after save failure: {(closedAfterFailure ? "Success" : "Cancelled")}");
            }
        }

        /// <summary>
        /// Example of a custom TestProjectManager subclass with built-in UI.
        /// </summary>
        public class ConsoleTestProjectManager : TestProjectManager
        {
            public ConsoleTestProjectManager()
            {
                // Set up console-based UI handlers automatically
                SetupConsoleHandlers();
            }

            private void SetupConsoleHandlers()
            {
                OnPromptSaveUnsavedChanges += () =>
                {
                    Console.WriteLine("Unsaved changes detected. Save? (y/n/c): ");
                    var key = Console.ReadKey().KeyChar;
                    Console.WriteLine();
                    
                    return char.ToLower(key) switch
                    {
                        'y' => SavePromptResponse.Save,
                        'n' => SavePromptResponse.DontSave,
                        _ => SavePromptResponse.Cancel
                    };
                };

                OnPromptSaveAs += () =>
                {
                    Console.Write("Save as (filename): ");
                    var input = Console.ReadLine();
                    return string.IsNullOrWhiteSpace(input) ? null : input;
                };

                OnSaveFailed += (message) =>
                {
                    Console.WriteLine($"Save failed: {message}. Continue? (y/n): ");
                    var key = Console.ReadKey().KeyChar;
                    Console.WriteLine();
                    
                    return char.ToLower(key) == 'y' ? SaveFailedResponse.Continue : SaveFailedResponse.Cancel;
                };
            }
        }

        /// <summary>
        /// Demonstrates the custom console manager.
        /// </summary>
        public static void DemonstrateConsoleManager()
        {
            Console.WriteLine("\n=== Console Manager Demonstration ===");

            var consoleManager = new ConsoleTestProjectManager();
            
            // The console manager has built-in UI handlers
            consoleManager.CreateNewProject("Console Project");
            consoleManager.CreateNewTestFlow("Console Test", "A test for console manager");
            
            // This will use the built-in console prompts
            consoleManager.CloseProject();
        }
    }
}