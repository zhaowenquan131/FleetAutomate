using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;
using FleetAutomate.Services;
using FleetAutomate.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FleetAutomate.Examples
{
    /// <summary>
    /// Comprehensive example demonstrating TestProject management operations.
    /// </summary>
    public static class ProjectManagementExample
    {
        /// <summary>
        /// Demonstrates creating a new project with structure.
        /// </summary>
        public static void CreateNewProjectExample()
        {
            Console.WriteLine("=== Creating New Project ===");

            var projectManager = new TestProjectManager();

            // Set up event handlers
            projectManager.OnProjectChanged += (project) =>
                Console.WriteLine($"Project changed: {(project != null ? "Loaded" : "Unloaded")}");

            projectManager.OnProjectPathChanged += (path) =>
                Console.WriteLine($"Project path changed: {path ?? "None"}");

            projectManager.OnUnsavedChangesChanged += (hasChanges) =>
                Console.WriteLine($"Unsaved changes: {hasChanges}");

            projectManager.OnOperationFailed += (message, ex) =>
                Console.WriteLine($"Operation failed: {message} - {ex.Message}");

            // Create new project
            var success = projectManager.CreateNewProject("Sample Project");
            Console.WriteLine($"New project created: {success}");

            // Add some TestFlows
            var testFlow1 = projectManager.CreateNewTestFlow("Login Test", "Tests user login functionality");
            var testFlow2 = projectManager.CreateNewTestFlow("Registration Test", "Tests user registration");

            if (testFlow1 != null && testFlow2 != null)
            {
                // Add some variables to the TestFlows
                testFlow1.Environment.Variables.Add(new Variable("username", "testuser", typeof(string)));
                testFlow1.Environment.Variables.Add(new Variable("password", "password123", typeof(string)));

                testFlow2.Environment.Variables.Add(new Variable("email", "test@example.com", typeof(string)));
                testFlow2.Environment.Variables.Add(new Variable("confirmPassword", "password123", typeof(string)));

                Console.WriteLine($"Added TestFlows: {testFlow1.Name}, {testFlow2.Name}");
            }

            // Set up project structure
            var projectDir = Path.Combine(Path.GetTempPath(), "SampleProject");
            var (project, projectPath) = ProjectOperations.CreateProjectWithStructure("SampleProject", projectDir);

            // Copy the current project's TestFlows to the structured project
            if (projectManager.CurrentProject != null)
            {
                foreach (var testFlow in projectManager.CurrentProject.TestFlows)
                {
                    project.TestFlows.Add(testFlow);
                }

                // Assign file paths to TestFlows
                var testFlowsDir = Path.Combine(projectDir, "TestFlows");
                foreach (var testFlow in project.TestFlows)
                {
                    testFlow.FileName = Path.Combine(testFlowsDir, $"{testFlow.Name?.Replace(" ", "_") ?? "TestFlow"}.xml");
                }
            }

            // Save the project
            project.SaveProjectAndTestFlows(projectPath);
            Console.WriteLine($"Project saved to: {projectPath}");

            // Load the project in the manager
            projectManager.OpenProject(projectPath);
        }

        /// <summary>
        /// Demonstrates opening and working with an existing project.
        /// </summary>
        public static void OpenProjectExample(string projectPath)
        {
            Console.WriteLine("=== Opening Existing Project ===");

            var projectManager = new TestProjectManager();

            var success = projectManager.OpenProject(projectPath);
            Console.WriteLine($"Project opened: {success}");

            if (success && projectManager.CurrentProject != null)
            {
                // Display project information
                var stats = ProjectOperations.GetProjectStatistics(projectManager.CurrentProject);
                Console.WriteLine($"Project Statistics: {stats}");

                // Validate the project
                var issues = projectManager.ValidateCurrentProject();
                if (issues.Any())
                {
                    Console.WriteLine("Project Issues:");
                    foreach (var issue in issues)
                    {
                        Console.WriteLine($"  - {issue}");
                    }
                }
                else
                {
                    Console.WriteLine("Project validation passed - no issues found");
                }

                // List all TestFlows
                Console.WriteLine("\nTestFlows in project:");
                foreach (var testFlow in projectManager.CurrentProject.TestFlows)
                {
                    Console.WriteLine($"  - {testFlow.Name} ({testFlow.Actions?.Count ?? 0} actions, " +
                                      $"{testFlow.Environment?.Variables?.Count ?? 0} variables)");
                    Console.WriteLine($"    File: {testFlow.FileName ?? "No file"}");
                    Console.WriteLine($"    Enabled: {testFlow.IsEnabled}");
                }
            }
        }

        /// <summary>
        /// Demonstrates importing TestFlows into a project.
        /// </summary>
        public static void ImportTestFlowsExample(string projectPath, string[] testFlowFiles)
        {
            Console.WriteLine("=== Importing TestFlows ===");

            var project = TestProjectXmlExtensions.LoadFromXmlFile(projectPath);
            if (project == null)
            {
                Console.WriteLine("Failed to load project");
                return;
            }

            var projectDir = Path.GetDirectoryName(projectPath);
            var importedCount = ProjectOperations.ImportTestFlows(
                project, 
                testFlowFiles, 
                copyToProjectDirectory: true, 
                projectDirectory: projectDir);

            Console.WriteLine($"Imported {importedCount} TestFlows");

            // Save the updated project
            project.SaveProjectAndTestFlows(projectPath);
            Console.WriteLine("Project saved with imported TestFlows");
        }

        /// <summary>
        /// Demonstrates exporting TestFlows from a project.
        /// </summary>
        public static void ExportTestFlowsExample(string projectPath, string exportDirectory)
        {
            Console.WriteLine("=== Exporting TestFlows ===");

            var project = TestProjectXmlExtensions.LoadFromXmlFile(projectPath);
            if (project == null)
            {
                Console.WriteLine("Failed to load project");
                return;
            }

            var exportedCount = ProjectOperations.ExportTestFlows(project, exportDirectory, overwriteExisting: true);
            Console.WriteLine($"Exported {exportedCount} TestFlows to {exportDirectory}");
        }

        /// <summary>
        /// Demonstrates creating a project backup.
        /// </summary>
        public static void CreateBackupExample(string projectPath)
        {
            Console.WriteLine("=== Creating Project Backup ===");

            try
            {
                var backupPath = ProjectOperations.CreateProjectBackup(projectPath);
                Console.WriteLine($"Backup created at: {backupPath}");

                // Verify backup
                var backupProject = TestProjectXmlExtensions.LoadFromXmlFile(backupPath);
                if (backupProject != null)
                {
                    Console.WriteLine($"Backup verified - contains {backupProject.TestFlows.Count} TestFlows");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Backup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Demonstrates repairing broken file references.
        /// </summary>
        public static void RepairFileReferencesExample(string projectPath)
        {
            Console.WriteLine("=== Repairing File References ===");

            var project = TestProjectXmlExtensions.LoadFromXmlFile(projectPath);
            if (project == null)
            {
                Console.WriteLine("Failed to load project");
                return;
            }

            // Show current issues
            var missingFiles = project.TestFlows
                .Where(tf => !string.IsNullOrEmpty(tf.FileName) && !File.Exists(tf.FileName))
                .ToList();

            Console.WriteLine($"Found {missingFiles.Count} TestFlows with missing files");

            if (missingFiles.Any())
            {
                // Try to repair by searching in common directories
                var projectDir = Path.GetDirectoryName(projectPath);
                var searchDirectories = new[]
                {
                    projectDir!,
                    Path.Combine(projectDir!, "TestFlows"),
                    Path.Combine(projectDir!, "Backup"),
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
                };

                var repairedCount = ProjectOperations.RepairFileReferences(project, searchDirectories);
                Console.WriteLine($"Repaired {repairedCount} file references");

                if (repairedCount > 0)
                {
                    project.SaveProjectAndTestFlows(projectPath);
                    Console.WriteLine("Project saved with repaired references");
                }
            }
        }

        /// <summary>
        /// Demonstrates cleaning up unused files.
        /// </summary>
        public static void CleanupUnusedFilesExample(string projectPath)
        {
            Console.WriteLine("=== Cleaning Up Unused Files ===");

            var project = TestProjectXmlExtensions.LoadFromXmlFile(projectPath);
            if (project == null)
            {
                Console.WriteLine("Failed to load project");
                return;
            }

            var projectDir = Path.GetDirectoryName(projectPath);
            if (projectDir == null)
            {
                Console.WriteLine("Could not determine project directory");
                return;
            }

            // First, do a dry run to see what would be deleted
            var filesToDelete = ProjectOperations.CleanupUnusedFiles(project, projectDir, dryRun: true);
            
            Console.WriteLine($"Found {filesToDelete.Count} unused files:");
            foreach (var file in filesToDelete)
            {
                Console.WriteLine($"  - {file}");
            }

            // For this example, we'll only do the dry run
            // In a real application, you might prompt the user before actually deleting
            Console.WriteLine("Dry run completed - no files were actually deleted");
        }

        /// <summary>
        /// Comprehensive project workflow demonstration.
        /// </summary>
        public static void CompleteWorkflowExample()
        {
            Console.WriteLine("=== Complete Project Management Workflow ===");

            var tempDir = Path.Combine(Path.GetTempPath(), $"ProjectExample_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // 1. Create new project
                var projectManager = new TestProjectManager();
                projectManager.CreateNewProject("Complete Workflow Example");

                // 2. Add TestFlows
                var testFlow1 = projectManager.CreateNewTestFlow("UI Test", "Tests user interface");
                var testFlow2 = projectManager.CreateNewTestFlow("API Test", "Tests API endpoints");
                var testFlow3 = projectManager.CreateNewTestFlow("Database Test", "Tests database operations");

                // 3. Add some content to TestFlows
                if (testFlow1 != null)
                {
                    testFlow1.Environment.Variables.Add(new Variable("browserType", "Chrome", typeof(string)));
                    testFlow1.Environment.Variables.Add(new Variable("timeout", 30, typeof(int)));
                }

                if (testFlow2 != null)
                {
                    testFlow2.Environment.Variables.Add(new Variable("baseUrl", "https://api.example.com", typeof(string)));
                    testFlow2.Environment.Variables.Add(new Variable("apiKey", "test-key-123", typeof(string)));
                }

                if (testFlow3 != null)
                {
                    testFlow3.Environment.Variables.Add(new Variable("connectionString", "Server=localhost;Database=Test", typeof(string)));
                    testFlow3.Environment.Variables.Add(new Variable("maxRetries", 3, typeof(int)));
                }

                // 4. Set up project structure and save
                var (project, projectPath) = ProjectOperations.CreateProjectWithStructure("CompleteWorkflow", tempDir);
                
                if (projectManager.CurrentProject != null)
                {
                    foreach (var testFlow in projectManager.CurrentProject.TestFlows)
                {
                    project.TestFlows.Add(testFlow);
                }
                    
                    // Assign file paths
                    var testFlowsDir = Path.Combine(tempDir, "TestFlows");
                    foreach (var testFlow in project.TestFlows)
                    {
                        testFlow.FileName = Path.Combine(testFlowsDir, $"{testFlow.Name?.Replace(" ", "_") ?? "TestFlow"}.xml");
                    }
                }

                project.SaveProjectAndTestFlows(projectPath);
                Console.WriteLine($"Project created and saved to: {projectPath}");

                // 5. Get statistics
                var stats = ProjectOperations.GetProjectStatistics(project);
                Console.WriteLine($"Initial project statistics: {stats}");

                // 6. Create backup
                var backupPath = ProjectOperations.CreateProjectBackup(projectPath);
                Console.WriteLine($"Backup created: {backupPath}");

                // 7. Export TestFlows
                var exportDir = Path.Combine(tempDir, "Exports");
                var exportedCount = ProjectOperations.ExportTestFlows(project, exportDir);
                Console.WriteLine($"Exported {exportedCount} TestFlows to: {exportDir}");

                // 8. Validate project
                projectManager.OpenProject(projectPath);
                var issues = projectManager.ValidateCurrentProject();
                Console.WriteLine($"Project validation: {(issues.Any() ? $"{issues.Count} issues found" : "No issues")}");

                // 9. Final statistics
                var finalStats = ProjectOperations.GetProjectStatistics(project);
                Console.WriteLine($"Final project statistics: {finalStats}");

                Console.WriteLine("Workflow completed successfully!");
            }
            finally
            {
                // Clean up temporary directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                        Console.WriteLine($"Cleaned up temporary directory: {tempDir}");
                    }
                }
                catch
                {
                    Console.WriteLine($"Note: Could not clean up temporary directory: {tempDir}");
                }
            }
        }
    }
}