using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Actions.Logic.Loops;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;
using FleetAutomate.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FleetAutomate.Model;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FleetAutomate.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {
        private TestProjectManager _projectManager;

        /// <summary>
        /// Gets the project manager instance.
        /// </summary>
        public TestProjectManager ProjectManager
        {
            get => _projectManager;
            private set => SetProperty(ref _projectManager, value);
        }

        /// <summary>
        /// Gets the currently active project.
        /// </summary>
        [ObservableProperty]
        private ObservableProject? _activeProject;

        /// <summary>
        /// Gets whether a project is currently loaded.
        /// </summary>
        public bool IsProjectLoaded => ProjectManager?.IsProjectLoaded ?? false;

        /// <summary>
        /// Gets whether the current project has unsaved changes.
        /// </summary>
        public bool HasUnsavedChanges => ProjectManager?.HasUnsavedChanges ?? false;

        /// <summary>
        /// Gets the current project file path.
        /// </summary>
        public string? CurrentProjectPath => ProjectManager?.CurrentProjectFilePath;

        private ObservableFlow? _selectedTestFlow;
        /// <summary>
        /// Gets or sets the currently selected TestFlow.
        /// </summary>
        public ObservableFlow? SelectedTestFlow
        {
            get => _selectedTestFlow;
            set
            {
                if (SetProperty(ref _selectedTestFlow, value))
                {
                    // Update command states
                    ((RelayCommand)RunTestFlowCommand).NotifyCanExecuteChanged();
                }
            }
        }

        private IAction? _selectedAction;
        /// <summary>
        /// Gets or sets the currently selected Action in the TreeView.
        /// </summary>
        public IAction? SelectedAction
        {
            get => _selectedAction;
            set
            {
                if (SetProperty(ref _selectedAction, value))
                {
                    // Update command states
                    ((RelayCommand)DeleteActionCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)ToggleElseBlockCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)ExecuteStepCommand).NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets the available action templates for the ToolBox.
        /// </summary>
        public ObservableCollection<ActionTemplate> AvailableActions { get; } = new();


        /// <summary>
        /// Event fired when UI needs to prompt user for project name and folder.
        /// Should return the project name, folder, and file path, or nulls if cancelled.
        /// </summary>
        public event Func<Task<(string? projectName, string? projectFolder, string? projectFilePath)>>? OnPromptProjectName;

        /// <summary>
        /// Event fired when UI needs to display an error message.
        /// </summary>
        public event Action<string, string>? OnShowError;

        /// <summary>
        /// Event fired when UI needs to display an information message.
        /// </summary>
        public event Action<string, string>? OnShowInfo;

        /// <summary>
        /// Event fired when UI needs to show a "Save As" file dialog.
        /// Should return the selected file path, or null if cancelled.
        /// </summary>
        public event Func<string?>? OnPromptSaveAsDialog;

        /// <summary>
        /// Event fired when UI needs to show an "Open Project" file dialog.
        /// Should return the selected file path, or null if cancelled.
        /// </summary>
        public event Func<string?>? OnPromptOpenDialog;

        /// <summary>
        /// Event fired when UI needs to show an "Open TestFlow" file dialog.
        /// Should return the selected .testfl file path, or null if cancelled.
        /// </summary>
        public event Func<string?>? OnPromptOpenTestFlowDialog;

        /// <summary>
        /// Event fired when UI needs to show a "Set Variable" dialog.
        /// Should return the variable name, type, and value, or null if cancelled.
        /// </summary>
        public event Func<(string variableName, string variableType, string variableValue)?>? OnPromptSetVariable;

        /// <summary>
        /// Event fired when UI needs to show an "If Action" dialog.
        /// Returns (conditionType, conditionExpression, elementIdentifier, identifierType), or null if cancelled.
        /// conditionType: "Expression" or "UIElementExists"
        /// </summary>
        public event Func<(string conditionType, string conditionExpression, string elementIdentifier, string identifierType, int retryTimes)?>? OnPromptIfAction;

        /// <summary>
        /// Event fired when UI needs to show a "Launch Application" dialog.
        /// Should return the launch application parameters, or null if cancelled.
        /// </summary>
        public event Func<(string executablePath, string arguments, string workingDirectory, bool waitForCompletion, int timeoutMs)?>? OnPromptLaunchApplication;

        /// <summary>
        /// Event fired when UI needs to show a "Wait for Element" dialog.
        /// Should return the element search parameters, or null if cancelled.
        /// </summary>
        public event Func<(string elementIdentifier, string identifierType, int timeoutMs, int pollingIntervalMs)?>? OnPromptWaitForElement;

        /// <summary>
        /// Event fired when UI needs to show a "Click Element" dialog.
        /// Should return the element click parameters, or null if cancelled.
        /// </summary>
        public event Func<(string elementIdentifier, string identifierType, bool isDoubleClick, bool useInvoke)?>? OnPromptClickElement;

        /// <summary>
        /// Command to create a new project.
        /// </summary>
        public ICommand CreateNewProjectCommand { get; }

        /// <summary>
        /// Command to open an existing project.
        /// </summary>
        public ICommand OpenProjectCommand { get; }

        /// <summary>
        /// Command to save the current project.
        /// </summary>
        public ICommand SaveProjectCommand { get; }

        /// <summary>
        /// Command to save the current project with a new name.
        /// </summary>
        public ICommand SaveProjectAsCommand { get; }

        /// <summary>
        /// Command to close the current project.
        /// </summary>
        public ICommand CloseProjectCommand { get; }

        /// <summary>
        /// Command to delete the selected action.
        /// </summary>
        public ICommand DeleteActionCommand { get; }

        /// <summary>
        /// Command to toggle the ElseBlock visibility on the selected IfAction.
        /// </summary>
        public ICommand ToggleElseBlockCommand { get; }

        /// <summary>
        /// Command to execute the currently selected TestFlow.
        /// </summary>
        public ICommand RunTestFlowCommand { get; }

        /// <summary>
        /// Command to execute only the selected action.
        /// </summary>
        public ICommand ExecuteStepCommand { get; }

        /// <summary>
        /// Command to add an existing .testfl file to the current project.
        /// </summary>
        public ICommand AddExistingTestFlowCommand { get; }

        public MainViewModel()
        {
            _projectManager = new TestProjectManager();

            // Set up project manager event handlers
            SetupProjectManagerEvents();

            // Initialize commands
            CreateNewProjectCommand = new RelayCommand(CreateNewProject);
            OpenProjectCommand = new RelayCommand<string>(OpenProject);
            SaveProjectCommand = new RelayCommand(SaveProject, () => IsProjectLoaded);
            SaveProjectAsCommand = new RelayCommand(SaveProjectAs, () => IsProjectLoaded);
            CloseProjectCommand = new RelayCommand(CloseProject, () => IsProjectLoaded);
            DeleteActionCommand = new RelayCommand(DeleteSelectedAction, () => SelectedAction != null && SelectedTestFlow != null);
            ToggleElseBlockCommand = new RelayCommand(ToggleElseBlock, CanToggleElseBlock);
            RunTestFlowCommand = new RelayCommand(RunTestFlow, () => SelectedTestFlow != null);
            ExecuteStepCommand = new RelayCommand(ExecuteStep, () => SelectedAction != null);
            AddExistingTestFlowCommand = new RelayCommand(AddExistingTestFlow, () => IsProjectLoaded);

            // Initialize available actions
            InitializeAvailableActions();

            // Note: OnPromptProjectName event should be handled by the UI layer (MainWindow)
            // This default implementation is for testing purposes only
        }

        /// <summary>
        /// Sets up event handlers for the project manager.
        /// </summary>
        private void SetupProjectManagerEvents()
        {
            ProjectManager.OnProjectChanged += (project) =>
            {
                OnPropertyChanged(nameof(ActiveProject));
                OnPropertyChanged(nameof(IsProjectLoaded));

                // Update command states
                ((RelayCommand)SaveProjectCommand).NotifyCanExecuteChanged();
                ((RelayCommand)SaveProjectAsCommand).NotifyCanExecuteChanged();
                ((RelayCommand)CloseProjectCommand).NotifyCanExecuteChanged();
                ((RelayCommand)AddExistingTestFlowCommand).NotifyCanExecuteChanged();
                ActiveProject = new ObservableProject(project);
            };

            ProjectManager.OnProjectPathChanged += (path) =>
            {
                OnPropertyChanged(nameof(CurrentProjectPath));
            };

            ProjectManager.OnUnsavedChangesChanged += (hasChanges) =>
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
            };

            ProjectManager.OnOperationFailed += (message, exception) =>
            {
                OnShowError?.Invoke("Operation Failed", $"{message}\n\nDetails: {exception.Message}");
            };

            // Set up save prompt handlers
            ProjectManager.OnPromptSaveUnsavedChanges += () =>
            {
                // This would typically show a message box with Save/Don't Save/Cancel options
                // For now, return a default response - this should be handled by the UI layer
                return SavePromptResponse.Save;
            };

            ProjectManager.OnPromptSaveAs += () =>
            {
                return OnPromptSaveAsDialog?.Invoke();
            };

            ProjectManager.OnSaveFailed += (errorMessage) =>
            {
                OnShowError?.Invoke("Save Failed", errorMessage);
                return SaveFailedResponse.Cancel;
            };
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
        }

        /// <summary>
        /// Creates a new project with user input for project name.
        /// </summary>
        private async void CreateNewProject()
        {
            try
            {
                // Prompt user for project name and folder
                var projectInfo = await (OnPromptProjectName?.Invoke() ?? Task.FromResult<(string?, string?, string?)>((null, null, null)));

                if (string.IsNullOrWhiteSpace(projectInfo.Item1))
                {
                    // User cancelled or provided empty name
                    return;
                }

                var projectName = projectInfo.Item1;
                var projectFolder = projectInfo.Item2;
                var projectFilePath = projectInfo.Item3;

                // Validate project name
                if (!IsValidProjectName(projectName))
                {
                    OnShowError?.Invoke("Invalid Project Name",
                        "Project name cannot contain invalid characters: \\ / : * ? \" < > |");
                    return;
                }

                // Create the project
                var success = ProjectManager.CreateNewProject(projectName, projectFilePath);

                if (success)
                {
                    // Success - no message shown
                }
                else
                {
                    OnShowError?.Invoke("Project Creation Failed", "Failed to create the new project.");
                }
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"An error occurred while creating the project: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens an existing project from the specified file path.
        /// </summary>
        /// <param name="filePath">Path to the project file to open.</param>
        private void OpenProject(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                // Prompt user for file path
                filePath = OnPromptOpenDialog?.Invoke();

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    // User cancelled
                    return;
                }
            }

            try
            {
                var success = ProjectManager.OpenProject(filePath);

                if (success)
                {
                    // Success - no message shown
                }
                else
                {
                    OnShowError?.Invoke("Open Failed", $"Failed to open project '{filePath}'.");
                }
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"An error occurred while opening the project: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the current project.
        /// </summary>
        private void SaveProject()
        {
            try
            {
                // Sync all observable flows to their models before saving
                if (ActiveProject != null)
                {
                    foreach (var flow in ActiveProject.TestFlows)
                    {
                        flow.SyncToModel();
                    }
                }

                var success = ProjectManager.SaveProject();

                if (success)
                {
                    // Success - no message shown
                }
                else
                {
                    OnShowError?.Invoke("Save Failed", "Failed to save the project. You may need to use 'Save As' for new projects.");
                }
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"An error occurred while saving the project: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the current project with a new name/location.
        /// </summary>
        private void SaveProjectAs()
        {
            try
            {
                if (!IsProjectLoaded)
                {
                    OnShowError?.Invoke("No Project", "Please create or open a project first.");
                    return;
                }

                // Prompt user for file path
                var filePath = OnPromptSaveAsDialog?.Invoke();

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    // User cancelled
                    return;
                }

                // Sync all observable flows to their models before saving
                if (ActiveProject != null)
                {
                    foreach (var flow in ActiveProject.TestFlows)
                    {
                        flow.SyncToModel();
                    }
                }

                // Save using the project manager
                var success = ProjectManager.SaveProjectAs(filePath);

                if (success)
                {
                    // Success - no message shown
                }
                else
                {
                    OnShowError?.Invoke("Save Failed", "Failed to save the project to the specified location.");
                }
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"An error occurred while saving the project: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes the current project.
        /// </summary>
        private void CloseProject()
        {
            try
            {
                var success = ProjectManager.CloseProject();

                if (success)
                {
                    // Success - no message shown
                }
                // If not successful, it was cancelled by user (handled by project manager events)
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"An error occurred while closing the project: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates whether a project name is valid.
        /// </summary>
        /// <param name="projectName">The project name to validate.</param>
        /// <returns>True if valid, false otherwise.</returns>
        private static bool IsValidProjectName(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return false;

            // Check for invalid file name characters
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            return !projectName.Any(c => invalidChars.Contains(c));
        }

        /// <summary>
        /// Adds a new TestFlow to the current project.
        /// </summary>
        /// <param name="name">Name of the TestFlow.</param>
        /// <param name="description">Description of the TestFlow.</param>
        /// <returns>The created ObservableFlow if successful, null otherwise.</returns>
        public ObservableFlow? AddTestFlow(string name, string? description = null)
        {
            if (!IsProjectLoaded || ActiveProject == null)
            {
                OnShowError?.Invoke("No Project", "Please create or open a project first.");
                return null;
            }

            try
            {
                // Use TestProjectManager to create the TestFlow properly
                // This ensures file creation and TestFlowFileNames update
                var testFlow = _projectManager.CreateNewTestFlow(name, description);

                if (testFlow == null)
                {
                    OnShowError?.Invoke("Error", "Failed to create TestFlow.");
                    return null;
                }

                // Refresh the ObservableProject to sync with the model
                // This creates the ObservableFlow wrapper for the newly added TestFlow
                ActiveProject.RefreshTestFlows();

                // Find and return the newly created ObservableFlow
                var observableFlow = ActiveProject.TestFlows
                    .FirstOrDefault(of => of.Model == testFlow);

                return observableFlow;
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to add TestFlow: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Adds an existing .testfl file to the current project.
        /// </summary>
        private void AddExistingTestFlow()
        {
            if (!IsProjectLoaded || ActiveProject == null)
            {
                OnShowError?.Invoke("No Project", "Please create or open a project first.");
                return;
            }

            try
            {
                // Prompt user to select a .testfl file
                var filePath = OnPromptOpenTestFlowDialog?.Invoke();

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    // User cancelled
                    return;
                }

                // Verify the file exists
                if (!System.IO.File.Exists(filePath))
                {
                    OnShowError?.Invoke("File Not Found", $"The file '{filePath}' does not exist.");
                    return;
                }

                // Verify it's a .testfl file
                if (!filePath.EndsWith(".testfl", StringComparison.OrdinalIgnoreCase))
                {
                    OnShowError?.Invoke("Invalid File", "Please select a .testfl file.");
                    return;
                }

                // Get project directory
                if (string.IsNullOrEmpty(_projectManager.CurrentProjectFilePath))
                {
                    OnShowError?.Invoke("Error", "Current project has no file path. Please save the project first.");
                    return;
                }

                var projectDirectory = System.IO.Path.GetDirectoryName(_projectManager.CurrentProjectFilePath);
                if (string.IsNullOrEmpty(projectDirectory))
                {
                    OnShowError?.Invoke("Error", "Invalid project file path.");
                    return;
                }

                // Add the existing TestFlow to the project (with copy to project directory)
                _projectManager.CurrentProject?.AddExistingTestFlow(filePath, projectDirectory, copyToProject: true);

                // Mark project as modified
                _projectManager.MarkAsModified();

                // Refresh the ObservableProject to show the newly added TestFlow
                ActiveProject.RefreshTestFlows();

                OnShowInfo?.Invoke("Success", $"TestFlow added successfully from '{System.IO.Path.GetFileName(filePath)}'.");
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to add existing TestFlow: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes a TestFlow from the current project.
        /// </summary>
        /// <param name="observableFlow">The ObservableFlow to remove.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool RemoveTestFlow(ObservableFlow observableFlow)
        {
            if (!IsProjectLoaded || ActiveProject == null)
            {
                OnShowError?.Invoke("No Project", "No project is currently loaded.");
                return false;
            }

            try
            {
                // Remove from the ObservableProject's TestFlows collection
                // This will automatically sync to the underlying model via CollectionChanged event
                var success = ActiveProject.TestFlows.Remove(observableFlow);
                return success;
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to remove TestFlow: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initializes the available actions for the ToolBox.
        /// </summary>
        private void InitializeAvailableActions()
        {
            AvailableActions.Clear();

            // Logic Actions
            AvailableActions.Add(new ActionTemplate("If Statement", "Logic", "🔀", typeof(IfAction), "Conditional execution based on a boolean expression"));
            AvailableActions.Add(new ActionTemplate("Set Variable", "Logic", "📝", typeof(FleetAutomate.Model.Actions.Logic.SetVariableAction<object>), "Assign a value to a variable"));

            // Loop Actions
            AvailableActions.Add(new ActionTemplate("While Loop", "Loops", "🔄", typeof(WhileLoopAction), "Execute actions while a condition is true"));
            AvailableActions.Add(new ActionTemplate("For Loop", "Loops", "🔁", typeof(ForLoopAction), "Execute actions with initialization, condition, and increment"));

            // System Actions
            AvailableActions.Add(new ActionTemplate("Launch Application", "System", "🚀", typeof(Model.Actions.System.LaunchApplicationAction), "Launch an application or execute a command"));

            // UI Automation Actions
            AvailableActions.Add(new ActionTemplate("Wait for Element", "UI Automation", "⏱️", typeof(Model.Actions.UIAutomation.WaitForElementAction), "Wait until a UI element exists"));
            AvailableActions.Add(new ActionTemplate("Click Element", "UI Automation", "👆", typeof(Model.Actions.UIAutomation.ClickElementAction), "Click on a UI element"));

            // Future categories can be added here
            // AvailableActions.Add(new ActionTemplate("Click Element", "UI Automation", "👆", typeof(ClickAction), "Click on a UI element"));
            // AvailableActions.Add(new ActionTemplate("Type Text", "UI Automation", "⌨️", typeof(TypeAction), "Type text into an input field"));

            // File System Actions
            // AvailableActions.Add(new ActionTemplate("Read File", "File System", "📄", typeof(ReadFileAction), "Read content from a file"));
            // AvailableActions.Add(new ActionTemplate("Write File", "File System", "💾", typeof(WriteFileAction), "Write content to a file"));
        }

        /// <summary>
        /// Adds an action to the selected TestFlow or under the selected action if it's a composite action.
        /// </summary>
        /// <param name="actionTemplate">The action template to create an instance from.</param>
        public void AddActionFromTemplate(ActionTemplate actionTemplate)
        {
            if (SelectedTestFlow == null)
            {
                OnShowError?.Invoke("No TestFlow Selected", "Please select a TestFlow before adding actions.");
                return;
            }

            try
            {
                IAction? action = null;

                // Special handling for IfAction - prompt user for condition
                if (actionTemplate.ActionType == typeof(IfAction))
                {
                    var result = OnPromptIfAction?.Invoke();
                    if (result == null)
                    {
                        // User cancelled
                        return;
                    }

                    var (conditionType, conditionExpression, elementIdentifier, identifierType, retryTimes) = result.Value;
                    action = CreateIfAction(conditionType, conditionExpression, elementIdentifier, identifierType, retryTimes);
                }
                // Special handling for SetVariableAction - prompt user for details
                else if (actionTemplate.ActionType == typeof(SetVariableAction<object>))
                {
                    var result = OnPromptSetVariable?.Invoke();
                    if (result == null)
                    {
                        // User cancelled
                        return;
                    }

                    action = CreateSetVariableAction(result.Value.variableName, result.Value.variableType, result.Value.variableValue);
                }
                // Special handling for LaunchApplicationAction - prompt user for application details
                else if (actionTemplate.ActionType == typeof(Model.Actions.System.LaunchApplicationAction))
                {
                    var result = OnPromptLaunchApplication?.Invoke();
                    if (result == null)
                    {
                        // User cancelled
                        return;
                    }

                    action = CreateLaunchApplicationAction(result.Value.executablePath, result.Value.arguments, result.Value.workingDirectory, result.Value.waitForCompletion, result.Value.timeoutMs);
                }
                // Special handling for WaitForElementAction - prompt user for element search parameters
                else if (actionTemplate.ActionType == typeof(Model.Actions.UIAutomation.WaitForElementAction))
                {
                    var result = OnPromptWaitForElement?.Invoke();
                    if (result == null)
                    {
                        // User cancelled
                        return;
                    }

                    action = CreateWaitForElementAction(result.Value.elementIdentifier, result.Value.identifierType, result.Value.timeoutMs, result.Value.pollingIntervalMs);
                }
                // Special handling for ClickElementAction - prompt user for element click parameters
                else if (actionTemplate.ActionType == typeof(Model.Actions.UIAutomation.ClickElementAction))
                {
                    var result = OnPromptClickElement?.Invoke();
                    if (result == null)
                    {
                        // User cancelled
                        return;
                    }

                    action = CreateClickElementAction(result.Value.elementIdentifier, result.Value.identifierType, result.Value.isDoubleClick, result.Value.useInvoke);
                }
                else
                {
                    // Create instance of the action normally
                    action = CreateActionInstance(actionTemplate);
                }

                if (action != null)
                {
                    // Check if an ActionBlock is selected (pseudo-node for action blocks)
                    if (SelectedAction is ActionBlock actionBlock)
                    {
                        // Add to the managed collection of the action block
                        actionBlock.ManagedCollection.Add(action);
                    }
                    // Check if a composite action is selected
                    else if (SelectedAction is ICompositeAction compositeAction)
                    {
                        // Add to the child actions of the selected composite action
                        compositeAction.GetChildActions().Add(action);
                    }
                    else
                    {
                        // Add to the root TestFlow Actions collection
                        SelectedTestFlow.Actions.Add(action);
                    }

                    // Select the newly added action
                    SelectedAction = action;
                }
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to add action: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates an instance of an action from a template.
        /// </summary>
        /// <param name="template">The action template.</param>
        /// <returns>An instance of the action, or null if creation failed.</returns>
        private IAction? CreateActionInstance(ActionTemplate template)
        {
            try
            {
                if (template.ActionType == typeof(WhileLoopAction))
                {
                    return new WhileLoopAction();
                }
                else if (template.ActionType == typeof(ForLoopAction))
                {
                    return new ForLoopAction();
                }

                // For other types, use Activator.CreateInstance
                return (IAction?)Activator.CreateInstance(template.ActionType);
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Creation Failed", $"Failed to create {template.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a SetVariableAction with the given parameters.
        /// </summary>
        private IAction? CreateSetVariableAction(string variableName, string variableType, string variableValue)
        {
            try
            {
                var parsedValue = Model.Actions.Logic.Expression.LiteralExpressionFactory.CreateLiteral(variableValue, variableType);

                if (parsedValue == null)
                {
                    OnShowError?.Invoke("Invalid Value", $"Could not parse '{variableValue}' as {variableType}.");
                    return null;
                }

                // Create Variable object
                var variable = new Model.Actions.Logic.Variable
                {
                    Name = variableName,
                    Value = parsedValue,
                    Type = GetTypeFromString(variableType)
                };

                // Create and return SetVariableAction<object>
                var action = new SetVariableAction<object>(variableName, parsedValue)
                {
                    Environment = SelectedTestFlow?.Model.Environment ?? new Model.Actions.Logic.Environment(),
                    Variable = variable
                };

                return action;
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to create variable: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates an IfAction with the given condition expression.
        /// </summary>
        private IAction? CreateIfAction(string conditionType, string conditionExpression, string elementIdentifier, string identifierType, int retryTimes)
        {
            try
            {
                object condition;

                if (conditionType == "Expression")
                {
                    // Parse the boolean expression
                    var parsedCondition = Model.Actions.Logic.Expression.BooleanExpressionParser.Parse(conditionExpression);

                    if (parsedCondition == null)
                    {
                        OnShowError?.Invoke("Invalid Expression", "Could not parse the condition as a boolean expression.");
                        return null;
                    }

                    condition = parsedCondition;
                }
                else if (conditionType == "UIElementExists")
                {
                    // Create UIElementExistsExpression with retry times
                    condition = new Model.Actions.Logic.Expression.UIElementExistsExpression(
                        elementIdentifier,
                        identifierType,
                        1000,  // 1 second timeout for existence check
                        retryTimes  // Use the retry times from the dialog
                    );
                }
                else
                {
                    OnShowError?.Invoke("Invalid Condition Type", $"Unknown condition type: {conditionType}");
                    return null;
                }

                // Create and return IfAction
                var description = conditionType == "UIElementExists"
                    ? $"If element '{elementIdentifier}' exists (retry {retryTimes}x)"
                    : $"If {conditionExpression}";

                var action = new IfAction
                {
                    Condition = condition,
                    Environment = SelectedTestFlow?.Model.Environment ?? new Model.Actions.Logic.Environment(),
                    Description = description
                };

                return action;
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to create if action: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a LaunchApplicationAction with the given parameters.
        /// </summary>
        private IAction? CreateLaunchApplicationAction(string executablePath, string arguments, string workingDirectory, bool waitForCompletion, int timeoutMs)
        {
            try
            {
                var action = new Model.Actions.System.LaunchApplicationAction
                {
                    ExecutablePath = executablePath,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    WaitForCompletion = waitForCompletion,
                    TimeoutMilliseconds = timeoutMs,
                    Description = $"Launch: {executablePath}"
                };

                return action;
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to create launch application action: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a WaitForElementAction with the given parameters.
        /// </summary>
        private IAction? CreateWaitForElementAction(string elementIdentifier, string identifierType, int timeoutMs, int pollingIntervalMs)
        {
            try
            {
                var action = new Model.Actions.UIAutomation.WaitForElementAction
                {
                    ElementIdentifier = elementIdentifier,
                    IdentifierType = identifierType,
                    TimeoutMilliseconds = timeoutMs,
                    PollingIntervalMilliseconds = pollingIntervalMs,
                    Description = $"Wait for {identifierType}: {elementIdentifier}"
                };

                return action;
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to create wait for element action: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a ClickElementAction with the given parameters.
        /// </summary>
        private IAction? CreateClickElementAction(string elementIdentifier, string identifierType, bool isDoubleClick, bool useInvoke)
        {
            try
            {
                var action = new Model.Actions.UIAutomation.ClickElementAction
                {
                    ElementIdentifier = elementIdentifier,
                    IdentifierType = identifierType,
                    IsDoubleClick = isDoubleClick,
                    UseInvoke = useInvoke,
                    Description = $"Click {(isDoubleClick ? "(double) " : "")}{(useInvoke ? "(invoke) " : "")}element: {identifierType}={elementIdentifier}"
                };

                return action;
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"Failed to create click element action: {ex.Message}");
                return null;
            }
        }

        private static Type GetTypeFromString(string typeStr)
        {
            return typeStr switch
            {
                "int" => typeof(int),
                "double" => typeof(double),
                "bool" => typeof(bool),
                "string" => typeof(string),
                _ => typeof(object)
            };
        }

        /// <summary>
        /// Deletes the currently selected action from the selected TestFlow.
        /// </summary>
        private void DeleteSelectedAction()
        {
            if (SelectedAction == null || SelectedTestFlow == null)
            {
                OnShowError?.Invoke("No Action Selected", "Please select an action to delete.");
                return;
            }

            try
            {
                // Try to remove from root first
                var removed = SelectedTestFlow.RemoveAction(SelectedAction);

                if (!removed)
                {
                    // If not found in root, search in nested collections (composite actions)
                    removed = RemoveActionFromNested(SelectedTestFlow.Actions, SelectedAction);
                }

                if (removed)
                {
                    // Clear the selection
                    SelectedAction = null;
                }
                else
                {
                    OnShowError?.Invoke("Delete Failed", "Failed to remove the action from the TestFlow.");
                }
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Error", $"An error occurred while deleting the action: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively searches and removes an action from nested composite actions.
        /// </summary>
        private bool RemoveActionFromNested(System.Collections.ObjectModel.ObservableCollection<IAction> actions, IAction targetAction)
        {
            foreach (var action in actions.ToList())
            {
                // Check if this is a composite action with child actions
                if (action is ICompositeAction compositeAction)
                {
                    var childActions = compositeAction.GetChildActions();

                    // Try to remove from this composite's children
                    if (childActions.Remove(targetAction))
                    {
                        return true;
                    }

                    // Recursively search in nested composite actions
                    if (RemoveActionFromNested(childActions, targetAction))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if the ToggleElseBlock command can be executed.
        /// </summary>
        private bool CanToggleElseBlock()
        {
            return SelectedAction is IfAction;
        }

        /// <summary>
        /// Toggles the visibility of the ElseBlock for the selected IfAction.
        /// </summary>
        private void ToggleElseBlock()
        {
            if (SelectedAction is IfAction ifAction)
            {
                ifAction.ShowElseBlock = !ifAction.ShowElseBlock;
            }
        }

        /// <summary>
        /// Executes the currently selected TestFlow.
        /// </summary>
        private async void RunTestFlow()
        {
            if (SelectedTestFlow == null)
            {
                OnShowError?.Invoke("No TestFlow Selected", "Please select a TestFlow to execute.");
                return;
            }

            try
            {
                // Sync the observable flow to the model before execution
                SelectedTestFlow.SyncToModel();

                // Execute the test flow
                var result = await SelectedTestFlow.Model.ExecuteAsync(CancellationToken.None);

                if (!result)
                {
                    OnShowError?.Invoke("Execution Failed", $"TestFlow '{SelectedTestFlow.Name}' execution failed or was cancelled.");
                }
                // Success - no message shown
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Execution Error", $"An error occurred while executing the TestFlow: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes only the currently selected action.
        /// </summary>
        private async void ExecuteStep()
        {
            if (SelectedAction == null)
            {
                OnShowError?.Invoke("No Action Selected", "Please select an action to execute.");
                return;
            }

            try
            {
                // For ILogicAction, ensure it has an environment
                if (SelectedAction is ILogicAction logicAction && SelectedTestFlow != null)
                {
                    logicAction.Environment = SelectedTestFlow.Model.Environment;
                }

                // Execute the action
                var result = await SelectedAction.ExecuteAsync(CancellationToken.None);

                if (!result)
                {
                    OnShowError?.Invoke("Step Execution Failed", $"Action '{SelectedAction.Name}' execution failed or was cancelled.");
                }
                // Success - no message shown, but state should be visible in the tree
            }
            catch (Exception ex)
            {
                OnShowError?.Invoke("Step Execution Error", $"An error occurred while executing the action: {ex.Message}");
            }
        }
    }
}
