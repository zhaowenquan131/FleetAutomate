using Canvas.TestRunner.Model.Flow;
using Canvas.TestRunner.Model.Project;
using Canvas.TestRunner.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Services
{
    /// <summary>
    /// Response options for save prompts.
    /// </summary>
    public enum SavePromptResponse
    {
        Save,
        DontSave,
        Cancel
    }

    /// <summary>
    /// Response options when save operation fails.
    /// </summary>
    public enum SaveFailedResponse
    {
        Continue,
        Cancel
    }

    /// <summary>
    /// Manages TestProject lifecycle operations including creating, saving, opening, and closing.
    /// </summary>
    public class TestProjectManager
    {
        private TestProject? _currentProject;
        private string? _currentProjectFilePath;
        private bool _hasUnsavedChanges;
        private RecentProjectsManager _recentProjectsManager;

        /// <summary>
        /// Gets the recent projects manager.
        /// </summary>
        public RecentProjectsManager RecentProjectsManager => _recentProjectsManager;

        public TestProjectManager()
        {
            _recentProjectsManager = new RecentProjectsManager();
        }

        /// <summary>
        /// Gets the currently loaded TestProject.
        /// </summary>
        public TestProject? CurrentProject 
        { 
            get => _currentProject;
            private set
            {
                _currentProject = value;
                OnProjectChanged?.Invoke(_currentProject);
            }
        }

        /// <summary>
        /// Gets the file path of the currently loaded project.
        /// </summary>
        public string? CurrentProjectFilePath 
        { 
            get => _currentProjectFilePath;
            private set
            {
                _currentProjectFilePath = value;
                OnProjectPathChanged?.Invoke(_currentProjectFilePath);
            }
        }

        /// <summary>
        /// Gets whether the current project has unsaved changes.
        /// </summary>
        public bool HasUnsavedChanges 
        { 
            get => _hasUnsavedChanges;
            private set
            {
                _hasUnsavedChanges = value;
                OnUnsavedChangesChanged?.Invoke(_hasUnsavedChanges);
            }
        }

        /// <summary>
        /// Gets whether a project is currently loaded.
        /// </summary>
        public bool IsProjectLoaded => CurrentProject != null;

        /// <summary>
        /// Event fired when the current project changes.
        /// </summary>
        public event Action<TestProject?>? OnProjectChanged;

        /// <summary>
        /// Event fired when the project file path changes.
        /// </summary>
        public event Action<string?>? OnProjectPathChanged;

        /// <summary>
        /// Event fired when the unsaved changes status changes.
        /// </summary>
        public event Action<bool>? OnUnsavedChangesChanged;

        /// <summary>
        /// Event fired when a project operation fails.
        /// </summary>
        public event Action<string, Exception>? OnOperationFailed;

        /// <summary>
        /// Event fired when user needs to be prompted to save unsaved changes.
        /// Should return the user's choice: Save, Don't Save, or Cancel.
        /// </summary>
        public event Func<SavePromptResponse>? OnPromptSaveUnsavedChanges;

        /// <summary>
        /// Event fired when user needs to choose a file path for "Save As" operation.
        /// Should return the selected file path, or null if cancelled.
        /// </summary>
        public event Func<string?>? OnPromptSaveAs;

        /// <summary>
        /// Event fired when save operation fails and user needs to decide how to proceed.
        /// Should return whether to continue with the operation or cancel.
        /// </summary>
        public event Func<string, SaveFailedResponse>? OnSaveFailed;

        /// <summary>
        /// Creates a new empty TestProject.
        /// </summary>
        /// <param name="projectName">Optional name for the new project.</param>
        /// <param name="projectFilePath">Optional file path for the new project.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool CreateNewProject(string? projectName = null, string? projectFilePath = null)
        {
            try
            {
                // Check for unsaved changes before creating new project
                if (HasUnsavedChanges)
                {
                    var shouldContinue = PromptSaveUnsavedChanges();
                    if (!shouldContinue)
                        return false;
                }

                var newProject = new TestProject();
                if (!string.IsNullOrEmpty(projectName))
                {
                    newProject.Name = projectName;
                }

                CurrentProject = newProject;
                CurrentProjectFilePath = projectFilePath; // Set the project file path if provided
                HasUnsavedChanges = false;

                return true;
            }
            catch (Exception ex)
            {
                OnOperationFailed?.Invoke("Failed to create new project", ex);
                return false;
            }
        }

        /// <summary>
        /// Opens an existing TestProject from file.
        /// </summary>
        /// <param name="filePath">Path to the project file.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool OpenProject(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Project file not found: {filePath}");

                // Check for unsaved changes before opening new project
                if (HasUnsavedChanges)
                {
                    var shouldContinue = PromptSaveUnsavedChanges();
                    if (!shouldContinue)
                        return false;
                }

                var project = TestProjectXmlExtensions.LoadFromXmlFile(filePath);
                if (project == null)
                    throw new InvalidOperationException("Failed to load project from file");

                CurrentProject = project;
                CurrentProjectFilePath = filePath;
                HasUnsavedChanges = false;

                // Track in recent projects
                _recentProjectsManager.AddRecentProject(filePath, project.Name);

                return true;
            }
            catch (Exception ex)
            {
                OnOperationFailed?.Invoke($"Failed to open project: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Saves the current project to its current file path.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        public bool SaveProject()
        {
            if (CurrentProject == null)
                return false;

            if (string.IsNullOrEmpty(CurrentProjectFilePath))
                return false; // Need to use SaveProjectAs for new projects

            if (SaveProjectToPath(CurrentProjectFilePath))
            {
                // Track in recent projects
                _recentProjectsManager.AddRecentProject(CurrentProjectFilePath, CurrentProject.Name);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Saves the current project to a specified file path.
        /// </summary>
        /// <param name="filePath">Path where to save the project.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool SaveProjectAs(string filePath)
        {
            if (CurrentProject == null)
                return false;

            if (string.IsNullOrEmpty(filePath))
                return false;

            if (SaveProjectToPath(filePath))
            {
                CurrentProjectFilePath = filePath;
                // Track in recent projects
                _recentProjectsManager.AddRecentProject(filePath, CurrentProject.Name);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Closes the current project.
        /// </summary>
        /// <returns>True if successful, false if cancelled by user.</returns>
        public bool CloseProject()
        {
            try
            {
                if (CurrentProject == null)
                    return true; // No project to close

                // Check for unsaved changes
                if (HasUnsavedChanges)
                {
                    var shouldContinue = PromptSaveUnsavedChanges();
                    if (!shouldContinue)
                        return false;
                }

                CurrentProject = null;
                CurrentProjectFilePath = null;
                HasUnsavedChanges = false;

                return true;
            }
            catch (Exception ex)
            {
                OnOperationFailed?.Invoke("Failed to close project", ex);
                return false;
            }
        }

        /// <summary>
        /// Adds a new TestFlow to the current project.
        /// </summary>
        /// <param name="testFlow">The TestFlow to add.</param>
        /// <param name="fileName">Optional custom file name (without extension). If null, uses testFlow.Name.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool AddTestFlow(TestFlow testFlow, string? fileName = null)
        {
            try
            {
                if (CurrentProject == null)
                    throw new InvalidOperationException("No project is currently loaded");

                if (string.IsNullOrEmpty(CurrentProjectFilePath))
                    throw new InvalidOperationException("Current project has no file path");

                ArgumentNullException.ThrowIfNull(testFlow);

                var projectDirectory = Path.GetDirectoryName(CurrentProjectFilePath);
                if (string.IsNullOrEmpty(projectDirectory))
                    throw new InvalidOperationException("Invalid project file path");

                // Use the new AddNewTestFlow method which handles file creation
                CurrentProject.AddNewTestFlow(testFlow, projectDirectory, fileName);
                MarkAsModified();

                return true;
            }
            catch (Exception ex)
            {
                OnOperationFailed?.Invoke("Failed to add TestFlow to project", ex);
                return false;
            }
        }

        /// <summary>
        /// Removes a TestFlow from the current project.
        /// </summary>
        /// <param name="testFlow">The TestFlow to remove.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool RemoveTestFlow(TestFlow testFlow)
        {
            try
            {
                if (CurrentProject == null)
                    throw new InvalidOperationException("No project is currently loaded");

                if (testFlow == null)
                    throw new ArgumentNullException(nameof(testFlow));

                var removed = CurrentProject.TestFlows.Remove(testFlow);
                if (removed)
                {
                    MarkAsModified();
                }

                return removed;
            }
            catch (Exception ex)
            {
                OnOperationFailed?.Invoke("Failed to remove TestFlow from project", ex);
                return false;
            }
        }

        /// <summary>
        /// Creates a new TestFlow and adds it to the current project.
        /// </summary>
        /// <param name="name">Name for the new TestFlow.</param>
        /// <param name="description">Description for the new TestFlow.</param>
        /// <param name="filePath">Optional file path for the TestFlow.</param>
        /// <returns>The created TestFlow if successful, null otherwise.</returns>
        public TestFlow? CreateNewTestFlow(string name, string? description = null, string? filePath = null)
        {
            try
            {
                if (CurrentProject == null)
                    throw new InvalidOperationException("No project is currently loaded");

                // Create the TestFlow object (without FileName - AddNewTestFlow will set it)
                var testFlow = new TestFlow()
                {
                    Name = name,
                    Description = description ?? $"TestFlow: {name}",
                    IsEnabled = true
                };

                // Use AddTestFlow which calls TestProject.AddNewTestFlow
                // This handles file creation and TestFlowFileNames update
                if (AddTestFlow(testFlow, filePath))
                {
                    return testFlow;
                }

                return null;
            }
            catch (Exception ex)
            {
                OnOperationFailed?.Invoke("Failed to create new TestFlow", ex);
                return null;
            }
        }

        /// <summary>
        /// Gets recent project file paths.
        /// </summary>
        /// <returns>List of recent project file paths.</returns>
        public List<string> GetRecentProjects()
        {
            // This could be implemented to read from user settings/registry
            // For now, return empty list
            return new List<string>();
        }

        /// <summary>
        /// Validates the current project for any issues.
        /// </summary>
        /// <returns>List of validation issues found.</returns>
        public List<string> ValidateCurrentProject()
        {
            var issues = new List<string>();

            if (CurrentProject == null)
            {
                issues.Add("No project is currently loaded");
                return issues;
            }

            // Check for TestFlows with missing files
            foreach (var testFlow in CurrentProject.TestFlows)
            {
                if (!string.IsNullOrEmpty(testFlow.FileName) && !File.Exists(testFlow.FileName))
                {
                    issues.Add($"TestFlow '{testFlow.Name}' references missing file: {testFlow.FileName}");
                }
            }

            // Check for duplicate file names
            var duplicateFiles = CurrentProject.TestFlows
                .Where(tf => !string.IsNullOrEmpty(tf.FileName))
                .GroupBy(tf => tf.FileName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var duplicateFile in duplicateFiles)
            {
                issues.Add($"Multiple TestFlows reference the same file: {duplicateFile}");
            }

            return issues;
        }

        /// <summary>
        /// Marks the current project as modified.
        /// </summary>
        public void MarkAsModified()
        {
            HasUnsavedChanges = true;
        }

        /// <summary>
        /// Generates a file path for a TestFlow based on its name and the current project location.
        /// </summary>
        /// <param name="testFlowName">The name of the TestFlow.</param>
        /// <returns>A file path for the TestFlow file.</returns>
        private string GenerateTestFlowFilePath(string testFlowName)
        {
            // Clean the test flow name to make it suitable for a filename
            var fileName = SanitizeFileName(testFlowName) + ".testflow";
            
            // If we have a current project file path, create TestFlow directory relative to it
            if (!string.IsNullOrEmpty(CurrentProjectFilePath))
            {
                var projectDirectory = Path.GetDirectoryName(CurrentProjectFilePath);
                if (!string.IsNullOrEmpty(projectDirectory))
                {
                    var testFlowsDirectory = Path.Combine(projectDirectory, "TestFlows");
                    return Path.Combine(testFlowsDirectory, fileName);
                }
            }
            
            // Fallback: create in a TestFlows subdirectory of current working directory
            var fallbackDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TestFlows");
            return Path.Combine(fallbackDirectory, fileName);
        }

        /// <summary>
        /// Creates a TestFlow file immediately when a TestFlow is added.
        /// </summary>
        /// <param name="testFlow">The TestFlow to create a file for.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private bool CreateTestFlowFile(TestFlow testFlow)
        {
            try
            {
                if (testFlow == null || string.IsNullOrEmpty(testFlow.FileName))
                    return false;

                // Ensure the directory exists
                var directory = Path.GetDirectoryName(testFlow.FileName);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create the TestFlow file
                testFlow.SaveToXmlFile(testFlow.FileName);
                
                return true;
            }
            catch (Exception ex)
            {
                OnOperationFailed?.Invoke($"Failed to create TestFlow file: {testFlow?.FileName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Ensures that directories for all TestFlow files exist.
        /// </summary>
        private void EnsureTestFlowDirectoriesExist()
        {
            if (CurrentProject?.TestFlows == null)
                return;
                
            foreach (var testFlow in CurrentProject.TestFlows)
            {
                if (!string.IsNullOrEmpty(testFlow.FileName))
                {
                    var directory = Path.GetDirectoryName(testFlow.FileName);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        try
                        {
                            Directory.CreateDirectory(directory);
                        }
                        catch (Exception ex)
                        {
                            OnOperationFailed?.Invoke($"Failed to create directory for TestFlow: {directory}", ex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sanitizes a string to be safe for use as a filename.
        /// </summary>
        /// <param name="fileName">The original filename.</param>
        /// <returns>A sanitized filename safe for the filesystem.</returns>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "TestFlow";
            
            // Remove or replace invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new StringBuilder();
            
            foreach (char c in fileName)
            {
                if (invalidChars.Contains(c))
                {
                    sanitized.Append('_');
                }
                else
                {
                    sanitized.Append(c);
                }
            }
            
            var result = sanitized.ToString().Trim();
            
            // Ensure we don't have an empty result
            if (string.IsNullOrEmpty(result))
                return "TestFlow";
                
            // Limit length to prevent filesystem issues
            if (result.Length > 100)
                result = result.Substring(0, 100);
                
            return result;
        }

        /// <summary>
        /// Internal method to save project to a specific path.
        /// </summary>
        private bool SaveProjectToPath(string filePath)
        {
            try
            {
                if (CurrentProject == null)
                    return false;

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save the project file itself using XML serialization
                CurrentProject.SaveToXmlFile(filePath);

                // Ensure TestFlow directories exist and save all TestFlow files
                var projectDirectory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(projectDirectory))
                {
                    EnsureTestFlowDirectoriesExist();
                    CurrentProject.SaveTestFlowsToFiles(projectDirectory);
                }
                HasUnsavedChanges = false;

                return true;
            }
            catch (Exception ex)
            {
                OnOperationFailed?.Invoke($"Failed to save project to: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Prompts to save unsaved changes. Override this method to provide custom UI.
        /// </summary>
        /// <returns>True to continue with operation, false to cancel.</returns>
        protected virtual bool PromptSaveUnsavedChanges()
        {
            var response = OnPromptSaveUnsavedChanges?.Invoke();
            
            switch (response)
            {
                case SavePromptResponse.Save:
                    return HandleSaveRequest();
                    
                case SavePromptResponse.DontSave:
                    return true; // Continue without saving
                    
                case SavePromptResponse.Cancel:
                    return false; // Cancel the operation
                    
                default:
                    // Fallback behavior when no event handler is attached
                    return HandleDefaultSavePrompt();
            }
        }

        /// <summary>
        /// Handles the save request when user chooses to save.
        /// </summary>
        /// <returns>True if save was successful or user chose to continue anyway, false to cancel.</returns>
        private bool HandleSaveRequest()
        {
            // If we have a file path, try to save
            if (!string.IsNullOrEmpty(CurrentProjectFilePath))
            {
                if (SaveProject())
                {
                    return true; // Successfully saved
                }
                else
                {
                    // Save failed, ask user what to do
                    var saveFailedResponse = OnSaveFailed?.Invoke("Failed to save project. Do you want to continue anyway?");
                    return saveFailedResponse != SaveFailedResponse.Cancel;
                }
            }
            else
            {
                // No file path, need to prompt for "Save As"
                var saveAsPath = OnPromptSaveAs?.Invoke();
                if (!string.IsNullOrEmpty(saveAsPath))
                {
                    if (SaveProjectAs(saveAsPath))
                    {
                        return true; // Successfully saved
                    }
                    else
                    {
                        // Save As failed
                        var saveFailedResponse = OnSaveFailed?.Invoke($"Failed to save project to '{saveAsPath}'. Do you want to continue anyway?");
                        return saveFailedResponse != SaveFailedResponse.Cancel;
                    }
                }
                else
                {
                    // User cancelled Save As dialog
                    return false;
                }
            }
        }

        /// <summary>
        /// Default behavior when no event handlers are attached.
        /// </summary>
        /// <returns>True to continue, false to cancel.</returns>
        private bool HandleDefaultSavePrompt()
        {
            // Automatically save if possible
            if (!string.IsNullOrEmpty(CurrentProjectFilePath))
            {
                var saveResult = SaveProject();
                if (!saveResult)
                {
                    // Save failed, but continue anyway in default mode
                    OnOperationFailed?.Invoke("Auto-save failed, but continuing with operation", new InvalidOperationException("Save failed"));
                }
                return true;
            }

            // No file path and no UI - continue without saving
            // Log this as a warning
            OnOperationFailed?.Invoke("Project has unsaved changes but no file path available for auto-save", new InvalidOperationException("Cannot auto-save"));
            return true;
        }
    }
}