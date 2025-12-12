using Canvas.TestRunner.Model.Flow;
using Canvas.TestRunner.Model.Project;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Utilities
{
    /// <summary>
    /// Static utility class for common project operations.
    /// </summary>
    public static class ProjectOperations
    {
        /// <summary>
        /// Creates a new project with default structure.
        /// </summary>
        /// <param name="projectName">Name of the project.</param>
        /// <param name="projectDirectory">Directory where to create the project.</param>
        /// <returns>The created TestProject and its file path.</returns>
        public static (TestProject project, string projectPath) CreateProjectWithStructure(string projectName, string projectDirectory)
        {
            if (string.IsNullOrEmpty(projectName))
                throw new ArgumentException("Project name cannot be null or empty", nameof(projectName));

            if (string.IsNullOrEmpty(projectDirectory))
                throw new ArgumentException("Project directory cannot be null or empty", nameof(projectDirectory));

            // Ensure project directory exists
            Directory.CreateDirectory(projectDirectory);

            // Create TestFlows subdirectory
            var testFlowsDirectory = Path.Combine(projectDirectory, "TestFlows");
            Directory.CreateDirectory(testFlowsDirectory);

            // Create project file path
            var projectFileName = $"{projectName}.testproj";
            var projectPath = Path.Combine(projectDirectory, projectFileName);

            // Create new project
            var project = new TestProject();

            return (project, projectPath);
        }

        /// <summary>
        /// Imports TestFlow files into a project.
        /// </summary>
        /// <param name="project">The target project.</param>
        /// <param name="testFlowFilePaths">Paths to .testfl files to import.</param>
        /// <param name="copyToProjectDirectory">Whether to copy files to project directory.</param>
        /// <param name="projectDirectory">Project directory for copying files.</param>
        /// <returns>Number of TestFlows successfully imported.</returns>
        public static int ImportTestFlows(TestProject project, IEnumerable<string> testFlowFilePaths, bool copyToProjectDirectory = false, string? projectDirectory = null)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("Project directory is required", nameof(projectDirectory));

            int importedCount = 0;

            foreach (var filePath in testFlowFilePaths)
            {
                try
                {
                    if (!File.Exists(filePath))
                        continue;

                    // Use the new AddExistingTestFlow method
                    project.AddExistingTestFlow(filePath, projectDirectory, copyToProjectDirectory);
                    importedCount++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to import {filePath}: {ex.Message}");
                    // Skip files that can't be imported
                    continue;
                }
            }

            return importedCount;
        }

        /// <summary>
        /// Exports TestFlows from a project to a specified directory.
        /// </summary>
        /// <param name="project">The source project.</param>
        /// <param name="exportDirectory">Directory where to export TestFlows.</param>
        /// <param name="overwriteExisting">Whether to overwrite existing files.</param>
        /// <returns>Number of TestFlows successfully exported.</returns>
        public static int ExportTestFlows(TestProject project, string exportDirectory, bool overwriteExisting = false)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (string.IsNullOrEmpty(exportDirectory))
                throw new ArgumentException("Export directory cannot be null or empty", nameof(exportDirectory));

            Directory.CreateDirectory(exportDirectory);

            int exportedCount = 0;

            foreach (var testFlow in project.TestFlows ?? Enumerable.Empty<TestFlow>())
            {
                try
                {
                    var fileName = !string.IsNullOrEmpty(testFlow.FileName)
                        ? Path.GetFileName(testFlow.FileName)
                        : $"{testFlow.Name ?? "TestFlow"}.testfl";

                    // Ensure .testfl extension
                    if (!fileName.EndsWith(".testfl", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = Path.ChangeExtension(fileName, ".testfl");
                    }

                    var exportPath = Path.Combine(exportDirectory, fileName);

                    if (!overwriteExisting && File.Exists(exportPath))
                        continue;

                    testFlow.SaveToXmlFile(exportPath);
                    exportedCount++;
                }
                catch
                {
                    // Skip TestFlows that can't be exported
                    continue;
                }
            }

            return exportedCount;
        }

        /// <summary>
        /// Creates a backup of a project and all its TestFlows.
        /// </summary>
        /// <param name="projectPath">Path to the project file.</param>
        /// <param name="backupDirectory">Directory where to create the backup.</param>
        /// <returns>Path to the backup project file.</returns>
        public static string CreateProjectBackup(string projectPath, string? backupDirectory = null)
        {
            if (!File.Exists(projectPath))
                throw new FileNotFoundException($"Project file not found: {projectPath}");

            // Use default backup directory if not specified
            if (string.IsNullOrEmpty(backupDirectory))
            {
                var projectDir = Path.GetDirectoryName(projectPath);
                backupDirectory = Path.Combine(projectDir!, "Backups");
            }

            Directory.CreateDirectory(backupDirectory);

            // Create timestamped backup directory
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupName = $"{Path.GetFileNameWithoutExtension(projectPath)}_Backup_{timestamp}";
            var specificBackupDir = Path.Combine(backupDirectory, backupName);
            Directory.CreateDirectory(specificBackupDir);

            // Load project to get TestFlow file paths
            var project = TestProjectXmlExtensions.LoadFromXmlFile(projectPath);
            if (project != null)
            {
                // Create TestFlows subdirectory in backup
                var backupTestFlowsDir = Path.Combine(specificBackupDir, "TestFlows");
                Directory.CreateDirectory(backupTestFlowsDir);

                // Copy all TestFlow files
                foreach (var testFlow in project.TestFlows)
                {
                    if (!string.IsNullOrEmpty(testFlow.FileName) && File.Exists(testFlow.FileName))
                    {
                        var fileName = Path.GetFileName(testFlow.FileName);
                        var backupTestFlowPath = Path.Combine(backupTestFlowsDir, fileName);
                        File.Copy(testFlow.FileName, backupTestFlowPath, overwrite: true);

                        // Update the file path in the project for the backup
                        testFlow.FileName = backupTestFlowPath;
                    }
                }

                // Save the project file to backup directory
                var backupProjectPath = Path.Combine(specificBackupDir, Path.GetFileName(projectPath));
                project.SaveProjectAndTestFlows(backupProjectPath);

                return backupProjectPath;
            }
            else
            {
                // Just copy the project file if it can't be loaded
                var backupProjectPath = Path.Combine(specificBackupDir, Path.GetFileName(projectPath));
                File.Copy(projectPath, backupProjectPath);
                return backupProjectPath;
            }
        }

        /// <summary>
        /// Repairs broken file references in a project.
        /// </summary>
        /// <param name="project">The project to repair.</param>
        /// <param name="searchDirectories">Directories to search for missing files.</param>
        /// <returns>Number of references repaired.</returns>
        public static int RepairFileReferences(TestProject project, IEnumerable<string> searchDirectories)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            int repairedCount = 0;

            foreach (var testFlow in project.TestFlows)
            {
                if (string.IsNullOrEmpty(testFlow.FileName) || File.Exists(testFlow.FileName))
                    continue;

                var fileName = Path.GetFileName(testFlow.FileName);

                foreach (var searchDir in searchDirectories)
                {
                    if (!Directory.Exists(searchDir))
                        continue;

                    var foundFiles = Directory.GetFiles(searchDir, fileName, SearchOption.AllDirectories);
                    if (foundFiles.Length > 0)
                    {
                        testFlow.FileName = foundFiles[0];
                        repairedCount++;
                        break;
                    }
                }
            }

            return repairedCount;
        }

        /// <summary>
        /// Gets project statistics.
        /// </summary>
        /// <param name="project">The project to analyze.</param>
        /// <returns>Project statistics.</returns>
        public static ProjectStatistics GetProjectStatistics(TestProject project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            var stats = new ProjectStatistics
            {
                TotalTestFlows = project.TestFlows.Count,
                EnabledTestFlows = project.TestFlows.Count(tf => tf.IsEnabled),
                DisabledTestFlows = project.TestFlows.Count(tf => !tf.IsEnabled),
                TestFlowsWithFiles = project.TestFlows.Count(tf => !string.IsNullOrEmpty(tf.FileName)),
                TestFlowsWithMissingFiles = project.TestFlows.Count(tf => !string.IsNullOrEmpty(tf.FileName) && !File.Exists(tf.FileName)),
                TotalActions = 0,
                TotalVariables = 0
            };

            foreach (var testFlow in project.TestFlows)
            {
                stats.TotalActions += testFlow.Actions?.Count ?? 0;
                stats.TotalVariables += testFlow.Environment?.Variables?.Count ?? 0;
            }

            return stats;
        }

        /// <summary>
        /// Cleans up unused TestFlow files in a project directory.
        /// </summary>
        /// <param name="project">The project to check against.</param>
        /// <param name="projectDirectory">The project directory to clean.</param>
        /// <param name="dryRun">If true, only returns files that would be deleted without actually deleting them.</param>
        /// <returns>List of files that were deleted or would be deleted.</returns>
        public static List<string> CleanupUnusedFiles(TestProject project, string projectDirectory, bool dryRun = true)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            if (!Directory.Exists(projectDirectory))
                return new List<string>();

            var referencedFiles = (project.TestFlows ?? Enumerable.Empty<TestFlow>())
                .Where(tf => !string.IsNullOrEmpty(tf.FileName))
                .Select(tf => Path.GetFullPath(tf.FileName!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var testFlowsDir = Path.Combine(projectDirectory, "TestFlows");
            var filesToDelete = new List<string>();

            if (Directory.Exists(testFlowsDir))
            {
                // Look for .testfl files (new format) and .xml files (legacy format)
                var allTestFlowFiles = Directory.GetFiles(testFlowsDir, "*.testfl", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(testFlowsDir, "*.xml", SearchOption.AllDirectories));

                foreach (var file in allTestFlowFiles)
                {
                    var fullPath = Path.GetFullPath(file);
                    if (!referencedFiles.Contains(fullPath))
                    {
                        filesToDelete.Add(file);
                        if (!dryRun)
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch
                            {
                                // Skip files that can't be deleted
                            }
                        }
                    }
                }
            }

            return filesToDelete;
        }
    }

    /// <summary>
    /// Contains statistical information about a project.
    /// </summary>
    public class ProjectStatistics
    {
        public int TotalTestFlows { get; set; }
        public int EnabledTestFlows { get; set; }
        public int DisabledTestFlows { get; set; }
        public int TestFlowsWithFiles { get; set; }
        public int TestFlowsWithMissingFiles { get; set; }
        public int TotalActions { get; set; }
        public int TotalVariables { get; set; }

        public override string ToString()
        {
            return $"TestFlows: {TotalTestFlows} ({EnabledTestFlows} enabled, {DisabledTestFlows} disabled), " +
                   $"Actions: {TotalActions}, Variables: {TotalVariables}, " +
                   $"Missing Files: {TestFlowsWithMissingFiles}";
        }
    }
}