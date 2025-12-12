using FleetAutomate.Model.Flow;
using FleetAutomate.Utilities;

using FleetAutomate.Model.Flow;

using System.IO;
using System.Runtime.Serialization;

namespace FleetAutomate.Model.Project
{
    [DataContract]
    public class TestProject
    {

        /// <summary>
        /// Parameterless constructor required for DataContract serialization.
        /// </summary>
        public TestProject()
        {
            Name = string.Empty;
            TestFlows = [];
        }

        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// TestFlows collection - NOT serialized to project file.
        /// TestFlows are loaded from individual .testfl files referenced in TestFlowFileNames.
        /// </summary>
        [IgnoreDataMember]
        public List<TestFlow>? TestFlows { get; private set; } = [];

        /// <summary>
        /// Array of .testfl file paths relative to project file.
        /// This is the ONLY TestFlow-related data saved in the .testproj file.
        /// </summary>
        [DataMember]
        public string[] TestFlowFileNames { get; set; } = [];


        /// <summary>
        /// Loads all TestFlow objects from their .testfl file paths.
        /// File paths in TestFlowFileNames are relative to the project file directory.
        /// </summary>
        /// <param name="projectDirectory">The directory containing the .testproj file.</param>
        public void LoadTestFlowsFromFiles(string projectDirectory)
        {
            TestFlows ??= [];
            TestFlows.Clear();

            if (TestFlowFileNames == null || TestFlowFileNames.Length == 0)
                return;

            foreach (var relativeFilePath in TestFlowFileNames)
            {
                if (string.IsNullOrWhiteSpace(relativeFilePath))
                    continue;

                // Convert relative path to absolute
                var absolutePath = Path.IsPathRooted(relativeFilePath)
                    ? relativeFilePath
                    : Path.Combine(projectDirectory, relativeFilePath);

                if (File.Exists(absolutePath))
                {
                    try
                    {
                        var loadedFlow = TestFlowXmlExtensions.LoadFromXmlFile(absolutePath);
                        if (loadedFlow != null)
                        {
                            loadedFlow.FileName = absolutePath;
                            TestFlows.Add(loadedFlow);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load TestFlow from {absolutePath}: {ex.Message}");
                        // Create placeholder for missing/corrupted file
                        var placeholder = new TestFlow
                        {
                            Name = $"[Error loading: {Path.GetFileNameWithoutExtension(relativeFilePath)}]",
                            FileName = absolutePath,
                            IsEnabled = false
                        };
                        TestFlows.Add(placeholder);
                    }
                }
                else
                {
                    // Create placeholder for missing file
                    var placeholder = new TestFlow
                    {
                        Name = $"[Missing: {Path.GetFileNameWithoutExtension(relativeFilePath)}]",
                        FileName = absolutePath,
                        IsEnabled = false
                    };
                    TestFlows.Add(placeholder);
                }
            }
        }

        /// <summary>
        /// Saves all TestFlow objects to their respective .testfl files.
        /// </summary>
        /// <param name="projectDirectory">The directory containing the .testproj file.</param>
        public void SaveTestFlowsToFiles(string projectDirectory)
        {
            if (TestFlows == null)
                return;

            foreach (var testFlow in TestFlows)
            {
                if (!string.IsNullOrEmpty(testFlow.FileName))
                {
                    try
                    {
                        // DEBUG: Log action count before saving
                        System.Diagnostics.Debug.WriteLine($"[SAVE] Saving TestFlow '{testFlow.Name}' with {testFlow.Actions.Count} actions to {testFlow.FileName}");

                        // Ensure directory exists
                        var directory = Path.GetDirectoryName(testFlow.FileName);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        testFlow.SaveToXmlFile(testFlow.FileName);

                        // DEBUG: Verify file was written
                        if (File.Exists(testFlow.FileName))
                        {
                            var fileInfo = new FileInfo(testFlow.FileName);
                            System.Diagnostics.Debug.WriteLine($"[SAVE] File written successfully: {fileInfo.Length} bytes");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to save TestFlow to {testFlow.FileName}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Creates and adds a new TestFlow to the project with a .testfl file.
        /// </summary>
        /// <param name="testFlow">The TestFlow to add.</param>
        /// <param name="projectDirectory">The directory containing the .testproj file.</param>
        /// <param name="fileName">Optional custom file name (without extension). If null, uses testFlow.Name.</param>
        public void AddNewTestFlow(TestFlow testFlow, string projectDirectory, string? fileName = null)
        {
            ArgumentNullException.ThrowIfNull(testFlow);

            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("Project directory cannot be null or empty", nameof(projectDirectory));

            // Determine file name from either parameter or testFlow name
            fileName = string.IsNullOrWhiteSpace(fileName) ? testFlow.Name : fileName;
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "NewTestFlow";

            // Sanitize file name
            fileName = SanitizeFileName(fileName);

            // Create TestFlows directory if it doesn't exist
            var testFlowsDir = Path.Combine(projectDirectory, "TestFlows");
            Directory.CreateDirectory(testFlowsDir);

            // Generate full file path with .testfl extension
            var testFlowFilePath = Path.Combine(testFlowsDir, $"{fileName}.testfl");

            // Ensure unique file name
            int counter = 1;
            while (File.Exists(testFlowFilePath))
            {
                testFlowFilePath = Path.Combine(testFlowsDir, $"{fileName}_{counter}.testfl");
                counter++;
            }

            // Set the file name on the TestFlow
            testFlow.FileName = testFlowFilePath;

            // Add to collection
            TestFlows?.Add(testFlow);

            // Add relative path to TestFlowFileNames
            var relativePath = GetRelativePath(projectDirectory, testFlowFilePath);
            TestFlowFileNames = [.. TestFlowFileNames ?? [], relativePath];

            // Save the TestFlow file immediately
            testFlow.SaveToXmlFile(testFlowFilePath);
        }

        /// <summary>
        /// Adds an existing .testfl file to the project.
        /// </summary>
        /// <param name="testFlowFilePath">Absolute path to the .testfl file.</param>
        /// <param name="projectDirectory">The directory containing the .testproj file.</param>
        /// <param name="copyToProject">If true, copies the file to project's TestFlows directory. If false, references it in-place.</param>
        public void AddExistingTestFlow(string testFlowFilePath, string projectDirectory, bool copyToProject = true)
        {
            if (!File.Exists(testFlowFilePath))
                throw new FileNotFoundException($"TestFlow file not found: {testFlowFilePath}");

            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("Project directory cannot be null or empty", nameof(projectDirectory));

            string targetPath = testFlowFilePath;

            if (copyToProject)
            {
                // Copy to project's TestFlows directory
                var testFlowsDir = Path.Combine(projectDirectory, "TestFlows");
                Directory.CreateDirectory(testFlowsDir);

                var fileName = Path.GetFileName(testFlowFilePath);
                targetPath = Path.Combine(testFlowsDir, fileName);

                // Ensure unique file name in target directory
                int counter = 1;
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                while (File.Exists(targetPath))
                {
                    targetPath = Path.Combine(testFlowsDir, $"{baseName}_{counter}{extension}");
                    counter++;
                }

                File.Copy(testFlowFilePath, targetPath, overwrite: false);
            }

            // Load the TestFlow from file
            var testFlow = TestFlowXmlExtensions.LoadFromXmlFile(targetPath);
            if (testFlow == null)
                throw new InvalidOperationException($"Failed to load TestFlow from {targetPath}");

            testFlow.FileName = targetPath;

            // Add to collection
            TestFlows?.Add(testFlow);

            // Add relative path to TestFlowFileNames
            var relativePath = GetRelativePath(projectDirectory, targetPath);
            if (!TestFlowFileNames.Contains(relativePath))
            {
                TestFlowFileNames = [.. TestFlowFileNames, relativePath];
            }
        }

        /// <summary>
        /// Removes a TestFlow from the project.
        /// </summary>
        /// <param name="testFlow">The TestFlow to remove.</param>
        /// <param name="deleteFile">If true, deletes the .testfl file from disk.</param>
        /// <returns>True if the TestFlow was removed, false if it wasn't found.</returns>
        public bool RemoveTestFlow(TestFlow testFlow, bool deleteFile = false)
        {
            ArgumentNullException.ThrowIfNull(testFlow);

            bool removed = TestFlows?.Remove(testFlow) ?? false;

            if (removed && !string.IsNullOrEmpty(testFlow.FileName))
            {
                // Remove from file names array
                var fileName = testFlow.FileName;
                TestFlowFileNames = [.. TestFlowFileNames.Where(fn =>
                {
                    // Compare absolute paths
                    var absoluteFn = Path.IsPathRooted(fn) ? fn : fn;
                    return !Path.GetFullPath(absoluteFn).Equals(Path.GetFullPath(fileName), StringComparison.OrdinalIgnoreCase);
                })];

                // Optionally delete the file
                if (deleteFile && File.Exists(testFlow.FileName))
                {
                    try
                    {
                        File.Delete(testFlow.FileName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete TestFlow file {testFlow.FileName}: {ex.Message}");
                    }
                }
            }

            return removed;
        }

        /// <summary>
        /// Renames a TestFlow and its associated .testfl file.
        /// </summary>
        /// <param name="testFlow">The TestFlow to rename.</param>
        /// <param name="newName">The new name for the TestFlow.</param>
        /// <param name="projectDirectory">The directory containing the .testproj file.</param>
        /// <returns>True if renamed successfully, false otherwise.</returns>
        public bool RenameTestFlow(TestFlow testFlow, string newName, string projectDirectory)
        {
            ArgumentNullException.ThrowIfNull(testFlow);

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name cannot be null or empty", nameof(newName));

            if (string.IsNullOrEmpty(testFlow.FileName))
                return false;

            // Sanitize the new name
            newName = SanitizeFileName(newName);

            // Get the directory of the current file
            var directory = Path.GetDirectoryName(testFlow.FileName);
            if (string.IsNullOrEmpty(directory))
                return false;

            // Create new file path with .testfl extension
            var newFilePath = Path.Combine(directory, $"{newName}.testfl");

            // Check if file already exists
            if (File.Exists(newFilePath) && !Path.GetFullPath(newFilePath).Equals(Path.GetFullPath(testFlow.FileName), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"A file named {newName}.testfl already exists in the directory");
            }

            var oldFilePath = testFlow.FileName;
            var oldRelativePath = GetRelativePath(projectDirectory, oldFilePath);

            try
            {
                // Update TestFlow name
                testFlow.Name = newName;

                // Save with new name (this creates the new file)
                testFlow.SaveToXmlFile(newFilePath);

                // Update FileName property
                testFlow.FileName = newFilePath;

                // Update TestFlowFileNames array
                var newRelativePath = GetRelativePath(projectDirectory, newFilePath);
                TestFlowFileNames = TestFlowFileNames.Select(fn =>
                    fn.Equals(oldRelativePath, StringComparison.OrdinalIgnoreCase) ? newRelativePath : fn
                ).ToArray();

                // Delete old file if it's different from new file
                if (!Path.GetFullPath(oldFilePath).Equals(Path.GetFullPath(newFilePath), StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(oldFilePath))
                    {
                        File.Delete(oldFilePath);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to rename TestFlow: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Helper method to get relative path from project directory to target file.
        /// </summary>
        private static string GetRelativePath(string projectDirectory, string targetPath)
        {
            var projectUri = new Uri(Path.GetFullPath(projectDirectory) + Path.DirectorySeparatorChar);
            var targetUri = new Uri(Path.GetFullPath(targetPath));
            var relativeUri = projectUri.MakeRelativeUri(targetUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Helper method to sanitize file names by removing invalid characters.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(sanitized) ? "TestFlow" : sanitized;
        }

        /// <summary>
        /// Internal method: Adds a TestFlow to the collection without file operations.
        /// Used by ViewModel layer for in-memory operations.
        /// Note: This does NOT update TestFlowFileNames or save files.
        /// </summary>
        internal void AddTestFlowToCollection(TestFlow testFlow)
        {
            ArgumentNullException.ThrowIfNull(testFlow);
            TestFlows?.Add(testFlow);
        }

        /// <summary>
        /// Finds a TestFlow by name.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <returns>The TestFlow if found, null otherwise.</returns>
        public TestFlow? FindTestFlowByName(string name)
        {
            return TestFlows.FirstOrDefault(tf => tf.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Finds a TestFlow by file name.
        /// </summary>
        /// <param name="fileName">The file name to search for.</param>
        /// <returns>The TestFlow if found, null otherwise.</returns>
        public TestFlow? FindTestFlowByFileName(string fileName)
        {
            return TestFlows.FirstOrDefault(tf =>
                !string.IsNullOrEmpty(tf.FileName) &&
                Path.GetFileName(tf.FileName).Equals(Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets all enabled TestFlows in the project.
        /// </summary>
        /// <returns>Collection of enabled TestFlows.</returns>
        public IEnumerable<TestFlow> GetEnabledTestFlows()
        {
            return TestFlows.Where(tf => tf.IsEnabled);
        }

        /// <summary>
        /// Gets all disabled TestFlows in the project.
        /// </summary>
        /// <returns>Collection of disabled TestFlows.</returns>
        public IEnumerable<TestFlow> GetDisabledTestFlows()
        {
            return TestFlows.Where(tf => !tf.IsEnabled);
        }

        /// <summary>
        /// Gets TestFlows that have missing file references.
        /// </summary>
        /// <returns>Collection of TestFlows with missing files.</returns>
        public IEnumerable<TestFlow> GetTestFlowsWithMissingFiles()
        {
            return TestFlows.Where(tf =>
                !string.IsNullOrEmpty(tf.FileName) &&
                !System.IO.File.Exists(tf.FileName));
        }

        /// <summary>
        /// Enables or disables all TestFlows in the project.
        /// </summary>
        /// <param name="enabled">True to enable all, false to disable all.</param>
        public void SetAllTestFlowsEnabled(bool enabled)
        {
            foreach (var testFlow in TestFlows)
            {
                testFlow.IsEnabled = enabled;
            }
        }

        /// <summary>
        /// Clears all TestFlows from the project.
        /// </summary>
        public void ClearTestFlows()
        {
            TestFlows.Clear();
        }
    }
}
