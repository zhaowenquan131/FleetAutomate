using FleetAutomate.View.Dialog;
using FleetAutomate.ViewModel;

using System.Collections.Generic;
using System.Linq;
using System.Collections.Specialized;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Wpf.Ui.Controls;

using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace FleetAutomate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public MainViewModel ViewModel { get; set; }

        // Dictionary to map LayoutDocuments to their corresponding ObservableFlows
        private readonly Dictionary<AvalonDock.Layout.LayoutDocument, ObservableFlow> _documentFlowMap = new();

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = (MainViewModel)DataContext;

            // Set up UI event handlers
            SetupUIEventHandlers();

            // Set up Open Recent menu
            Loaded += (s, e) => SetupOpenRecentMenu();

            // Set up AvalonDock document management
            Loaded += (s, e) => SetupDocumentManagement();
        }

        private void SetupUIEventHandlers()
        {
            // Handle project name prompt
            ViewModel.OnPromptProjectName += () =>
            {
                var dialog = new ProjectNameDialog
                {
                    Owner = this
                };

                var result = dialog.ShowDialog();

                if (result == true)
                {
                    // Create the project file path using the selected folder and project name
                    var projectFilePath = System.IO.Path.Combine(dialog.ProjectFolder, dialog.ProjectName + ".testproj");
                    return Task.FromResult<(string? projectName, string? projectFolder, string? projectFilePath)>((dialog.ProjectName, dialog.ProjectFolder, projectFilePath));
                }

                return Task.FromResult<(string? projectName, string? projectFolder, string? projectFilePath)>((null, null, null)); // User cancelled
            };

            // Handle set variable prompt
            ViewModel.OnPromptSetVariable += () =>
            {
                var dialog = new SetVariableDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.VariableName, dialog.VariableType, dialog.VariableValue);
                }

                return null; // User cancelled
            };

            // Handle if action prompt
            ViewModel.OnPromptIfAction += () =>
            {
                var dialog = new IfActionDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.ConditionType, dialog.ConditionExpression, dialog.ElementIdentifier, dialog.IdentifierType, dialog.RetryTimes);
                }

                return null; // User cancelled
            };

            // Handle launch application prompt
            ViewModel.OnPromptLaunchApplication += () =>
            {
                var dialog = new LaunchApplicationDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.ExecutablePath, dialog.Arguments, dialog.WorkingDirectory, dialog.WaitForCompletion, dialog.TimeoutMilliseconds);
                }

                return null; // User cancelled
            };

            // Handle wait for element prompt
            ViewModel.OnPromptWaitForElement += () =>
            {
                var dialog = new WaitForElementDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.ElementIdentifier, dialog.IdentifierType, dialog.TimeoutMilliseconds, dialog.PollingIntervalMilliseconds);
                }

                return null; // User cancelled
            };

            // Handle click element prompt
            ViewModel.OnPromptClickElement += () =>
            {
                var dialog = new ClickElementDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.ElementIdentifier, dialog.IdentifierType, dialog.IsDoubleClick, dialog.UseInvoke, dialog.RetryTimes, dialog.RetryDelayMilliseconds);
                }

                return null; // User cancelled
            };

            // Handle set text prompt
            ViewModel.OnPromptSetText += () =>
            {
                var dialog = new SetTextDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.ElementIdentifier, dialog.IdentifierType, dialog.TextToSet, dialog.ClearExistingText, dialog.RetryTimes, dialog.RetryDelayMilliseconds);
                }

                return null; // User cancelled
            };

            // Handle if window contains text prompt
            ViewModel.OnPromptIfWindowContainsText += () =>
            {
                var dialog = new IfWindowContainsTextDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.WindowIdentifier, dialog.IdentifierType, dialog.SearchText, dialog.CaseSensitive, dialog.DeepSearch);
                }

                return null; // User cancelled
            };

            // Handle error messages
            ViewModel.OnShowError += async (title, message) =>
            {
                var messageBox = new MessageBox
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = "OK"
                };
                await messageBox.ShowDialogAsync();
            };

            // Open Recent menu is set up after window is loaded

            // Handle "Save As" file dialog
            ViewModel.OnPromptSaveAsDialog += () =>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save Project As",
                    Filter = "Test Project files (*.testproj)|*.testproj|All files (*.*)|*.*",
                    DefaultExt = "testproj",
                    AddExtension = true
                };

                if (dialog.ShowDialog(this) == true)
                {
                    return dialog.FileName;
                }

                return null; // User cancelled
            };

            // Handle "Open Project" file dialog
            ViewModel.OnPromptOpenDialog += () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Open Project",
                    Filter = "Test Project files (*.testproj)|*.testproj|All files (*.*)|*.*",
                    DefaultExt = "testproj",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog(this) == true)
                {
                    return dialog.FileName;
                }

                return null; // User cancelled
            };

            // Handle "Open TestFlow" file dialog
            ViewModel.OnPromptOpenTestFlowDialog += () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Add Existing TestFlow",
                    Filter = "TestFlow files (*.testfl)|*.testfl|All files (*.*)|*.*",
                    DefaultExt = "testfl",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog(this) == true)
                {
                    return dialog.FileName;
                }

                return null; // User cancelled
            };
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddTestFlowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TestFlowNameDialog()
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                ViewModel.AddTestFlow(dialog.TestFlowName, dialog.TestFlowDescription);
            }
        }

        private void CaptureElementMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ElementCaptureDialog()
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Copy the captured XPath to clipboard for convenience
                try
                {
                    System.Windows.Forms.Clipboard.SetText(dialog.CapturedXPath);
                }
                catch { }

                // Optionally, show a message with the captured XPath
                var msgBox = new MessageBox
                {
                    Title = "Element Captured",
                    Content = $"XPath captured and copied to clipboard:\n\n{dialog.CapturedXPath}",
                    PrimaryButtonText = "OK"
                };
                msgBox.ShowDialog();
            }
        }

        private void TestFlowsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Open the double-clicked TestFlow in a tab
            if (sender is System.Windows.Controls.ListView listView && listView.SelectedItem is ObservableFlow flow)
            {
                ViewModel.OpenTestFlowInTabCommand.Execute(flow);
                e.Handled = true;
            }
        }

        private void ActionsToolBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // TreeView implementation - differentiate between category and action
            if (sender is System.Windows.Controls.TreeView treeView)
            {
                var selectedItem = treeView.SelectedItem;

                // Check if it's an ActionTemplate (leaf node)
                if (selectedItem is Model.ActionTemplate actionTemplate)
                {
                    ViewModel.AddActionFromTemplate(actionTemplate);
                    e.Handled = true;
                }
                // If it's an ActionCategory, toggle expansion
                else if (selectedItem is Model.ActionCategory category)
                {
                    // Toggle expansion on category double-click
                    var item = treeView.ItemContainerGenerator.ContainerFromItem(category) as System.Windows.Controls.TreeViewItem;
                    if (item != null)
                    {
                        item.IsExpanded = !item.IsExpanded;
                    }
                    e.Handled = true;
                }
            }
        }

        private void TestFlowActionsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Update the SelectedAction in the ViewModel
            if (e.NewValue is Model.IAction action)
            {
                ViewModel.SelectedAction = action;
            }
            else
            {
                ViewModel.SelectedAction = null;
            }
        }

        private void TestFlowActionsTreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Handle Delete key press
            if (e.Key == Key.Delete && ViewModel.SelectedAction != null)
            {
                if (ViewModel.DeleteActionCommand.CanExecute(null))
                {
                    ViewModel.DeleteActionCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void TestFlowsListView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // When user clicks on a TestFlow in the list, clear the selected action
            // so that new actions added from toolbox go to the root of the test flow
            ViewModel.SelectedAction = null;
        }

        private void TestFlowActionsTreeView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check if we clicked on empty space in the TreeView
            var treeView = sender as System.Windows.Controls.TreeView;
            if (treeView == null) return;

            var hitTestResult = VisualTreeHelper.HitTest(treeView, e.GetPosition(treeView));

            // If the hit test returns no visual, or the visual is the TreeView itself (not a TreeViewItem),
            // then empty space was clicked
            if (hitTestResult?.VisualHit == null)
            {
                ViewModel.SelectedAction = null;
            }
            else
            {
                // Walk up the visual tree to check if we clicked on a TreeViewItem
                var dependencyObject = hitTestResult.VisualHit as DependencyObject;
                while (dependencyObject != null)
                {
                    if (dependencyObject is System.Windows.Controls.TreeViewItem)
                    {
                        // Clicked on a TreeViewItem, let the normal selection logic handle it
                        return;
                    }
                    dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
                }

                // Clicked on empty space (no TreeViewItem in the visual hierarchy)
                ViewModel.SelectedAction = null;
            }
        }

        private void TestFlowActionsTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Find the TreeViewItem that was right-clicked
            var treeView = sender as System.Windows.Controls.TreeView;
            if (treeView == null) return;

            var hitTestResult = VisualTreeHelper.HitTest(treeView, e.GetPosition(treeView));
            if (hitTestResult?.VisualHit == null) return;

            // Walk up the visual tree to find the TreeViewItem
            var dependencyObject = hitTestResult.VisualHit as DependencyObject;
            while (dependencyObject != null)
            {
                if (dependencyObject is System.Windows.Controls.TreeViewItem treeViewItem)
                {
                    // Select the TreeViewItem
                    treeViewItem.IsSelected = true;
                    treeViewItem.Focus();
                    e.Handled = true;
                    return;
                }
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }
        }

        private void TestFlowActionsTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Check if we double-clicked on a TreeViewItem
            var treeView = sender as System.Windows.Controls.TreeView;
            if (treeView == null) return;

            var hitTestResult = VisualTreeHelper.HitTest(treeView, e.GetPosition(treeView));
            if (hitTestResult?.VisualHit == null) return;

            // Walk up the visual tree to find the TreeViewItem
            var dependencyObject = hitTestResult.VisualHit as DependencyObject;
            while (dependencyObject != null)
            {
                if (dependencyObject is System.Windows.Controls.TreeViewItem treeViewItem)
                {
                    // Get the action from the TreeViewItem's DataContext
                    if (treeViewItem.DataContext is Model.IAction action)
                    {
                        // Special handling for IfAction - use IfActionDialog for editing
                        if (action is Model.Actions.Logic.IfAction ifAction)
                        {
                            EditIfAction(ifAction);
                        }
                        // Special handling for ClickElementAction - use ClickElementDialog for editing
                        else if (action is Model.Actions.UIAutomation.ClickElementAction clickAction)
                        {
                            EditClickElementAction(clickAction);
                        }
                        // Special handling for SetTextAction - use SetTextDialog for editing
                        else if (action is Model.Actions.UIAutomation.SetTextAction setTextAction)
                        {
                            EditSetTextAction(setTextAction);
                        }
                        // Special handling for IfWindowContainsTextAction - use IfWindowContainsTextDialog for editing
                        else if (action is Model.Actions.UIAutomation.IfWindowContainsTextAction windowTextAction)
                        {
                            EditIfWindowContainsTextAction(windowTextAction);
                        }
                        // Special handling for LaunchApplicationAction - use LaunchApplicationDialog for editing
                        else if (action is Model.Actions.System.LaunchApplicationAction launchAction)
                        {
                            EditLaunchApplicationAction(launchAction);
                        }
                        else
                        {
                            // Open the standard properties dialog
                            var dialog = new ActionPropertiesDialog(action)
                            {
                                Owner = this
                            };

                            if (dialog.ShowDialog() == true || dialog.DialogResultOk)
                            {
                                // Properties were saved, force refresh by clearing and re-setting the active test flow
                                var currentTestFlow = ViewModel.ActiveTestFlow;
                                ViewModel.ActiveTestFlow = null;
                                ViewModel.ActiveTestFlow = currentTestFlow;
                            }
                        }

                        e.Handled = true;
                        return;
                    }
                }
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }
        }

        /// <summary>
        /// Extracts a concise description from element identifier.
        /// For XPath, extracts the last property (e.g., "@Name='OK'" becomes "Name: OK").
        /// For other types, returns "Type: Value" format.
        /// </summary>
        private string FormatElementDescription(string elementIdentifier, string identifierType)
        {
            if (identifierType == "XPath")
            {
                // Extract the LAST property from XPath (leaf node, not root)
                // Pattern: [@PropertyName='Value'] or [@PropertyName="Value"]
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    elementIdentifier,
                    @"\[@(\w+)=['""]([^'""]+)['""]");

                if (matches.Count > 0)
                {
                    // Get the LAST match (leaf node)
                    var lastMatch = matches[matches.Count - 1];
                    string propertyName = lastMatch.Groups[1].Value;
                    string propertyValue = lastMatch.Groups[2].Value;
                    return $"{propertyName}: {propertyValue}";
                }

                // If we can't parse it, return the full XPath
                return $"XPath: {elementIdentifier}";
            }
            else
            {
                // For Name, AutomationId, ClassName, etc.
                return $"{identifierType}: {elementIdentifier}";
            }
        }

        private void EditIfAction(Model.Actions.Logic.IfAction ifAction)
        {
            // Determine the condition type and extract values
            string conditionType;
            string conditionExpression = string.Empty;
            string elementIdentifier = string.Empty;
            string identifierType = "XPath";
            int retryTimes = 1;

            if (ifAction.Condition is Model.Actions.Logic.Expression.UIElementExistsExpression uiExpr)
            {
                conditionType = "UIElementExists";
                elementIdentifier = uiExpr.ElementIdentifier;
                identifierType = uiExpr.IdentifierType;
                retryTimes = uiExpr.RetryTimes;
            }
            else
            {
                conditionType = "Expression";
                conditionExpression = ifAction.Condition?.ToString() ?? string.Empty;
            }

            // Create and show the IfActionDialog with pre-populated values
            var dialog = new IfActionDialog(conditionType, conditionExpression, elementIdentifier, identifierType, retryTimes)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the IfAction with new values
                if (dialog.ConditionType == "Expression")
                {
                    var parsedCondition = Model.Actions.Logic.Expression.BooleanExpressionParser.Parse(dialog.ConditionExpression);
                    if (parsedCondition != null)
                    {
                        ifAction.Condition = parsedCondition;
                        ifAction.Description = $"If {dialog.ConditionExpression}";
                    }
                }
                else if (dialog.ConditionType == "UIElementExists")
                {
                    ifAction.Condition = new Model.Actions.Logic.Expression.UIElementExistsExpression(
                        dialog.ElementIdentifier,
                        dialog.IdentifierType,
                        1000,
                        dialog.RetryTimes
                    );
                    ifAction.Description = $"{FormatElementDescription(dialog.ElementIdentifier, dialog.IdentifierType)} (retry:{dialog.RetryTimes}x)";
                }

                // Force refresh
                var currentTestFlow = ViewModel.ActiveTestFlow;
                ViewModel.ActiveTestFlow = null;
                ViewModel.ActiveTestFlow = currentTestFlow;
            }
        }

        private void EditClickElementAction(Model.Actions.UIAutomation.ClickElementAction clickAction)
        {
            // Extract current values from the ClickElementAction
            string elementIdentifier = clickAction.ElementIdentifier;
            string identifierType = clickAction.IdentifierType;
            bool isDoubleClick = clickAction.IsDoubleClick;
            bool useInvoke = clickAction.UseInvoke;
            int retryTimes = clickAction.RetryTimes;
            int retryDelayMilliseconds = clickAction.RetryDelayMilliseconds;

            // Create and show the ClickElementDialog with pre-populated values
            var dialog = new ClickElementDialog(elementIdentifier, identifierType, isDoubleClick, useInvoke, retryTimes, retryDelayMilliseconds)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the ClickElementAction with new values
                clickAction.ElementIdentifier = dialog.ElementIdentifier;
                clickAction.IdentifierType = dialog.IdentifierType;
                clickAction.IsDoubleClick = dialog.IsDoubleClick;
                clickAction.UseInvoke = dialog.UseInvoke;
                clickAction.RetryTimes = dialog.RetryTimes;
                clickAction.RetryDelayMilliseconds = dialog.RetryDelayMilliseconds;
                clickAction.Description = $"{FormatElementDescription(dialog.ElementIdentifier, dialog.IdentifierType)}{(dialog.IsDoubleClick ? " (double)" : "")}{(dialog.UseInvoke ? " (invoke)" : "")} (retry:{dialog.RetryTimes}x)";

                // Force refresh
                var currentTestFlow = ViewModel.ActiveTestFlow;
                ViewModel.ActiveTestFlow = null;
                ViewModel.ActiveTestFlow = currentTestFlow;
            }
        }

        private void EditSetTextAction(Model.Actions.UIAutomation.SetTextAction setTextAction)
        {
            // Extract current values from the SetTextAction
            string elementIdentifier = setTextAction.ElementIdentifier;
            string identifierType = setTextAction.IdentifierType;
            string textToSet = setTextAction.TextToSet;
            bool clearExistingText = setTextAction.ClearExistingText;
            int retryTimes = setTextAction.RetryTimes;
            int retryDelayMilliseconds = setTextAction.RetryDelayMilliseconds;

            // Create and show the SetTextDialog with pre-populated values
            var dialog = new SetTextDialog(elementIdentifier, identifierType, textToSet, clearExistingText, retryTimes, retryDelayMilliseconds)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the SetTextAction with new values
                setTextAction.ElementIdentifier = dialog.ElementIdentifier;
                setTextAction.IdentifierType = dialog.IdentifierType;
                setTextAction.TextToSet = dialog.TextToSet;
                setTextAction.ClearExistingText = dialog.ClearExistingText;
                setTextAction.RetryTimes = dialog.RetryTimes;
                setTextAction.RetryDelayMilliseconds = dialog.RetryDelayMilliseconds;
                setTextAction.Description = $"{FormatElementDescription(dialog.ElementIdentifier, dialog.IdentifierType)} = '{dialog.TextToSet}'{(dialog.ClearExistingText ? " (clear first)" : "")} (retry:{dialog.RetryTimes}x)";

                // Force refresh
                var currentTestFlow = ViewModel.ActiveTestFlow;
                ViewModel.ActiveTestFlow = null;
                ViewModel.ActiveTestFlow = currentTestFlow;
            }
        }

        private void EditIfWindowContainsTextAction(Model.Actions.UIAutomation.IfWindowContainsTextAction windowTextAction)
        {
            // Extract current values from the IfWindowContainsTextAction
            string windowIdentifier = windowTextAction.WindowIdentifier;
            string identifierType = windowTextAction.IdentifierType;
            string searchText = windowTextAction.SearchText;
            bool caseSensitive = windowTextAction.CaseSensitive;
            bool deepSearch = windowTextAction.DeepSearch;

            // Create and show the IfWindowContainsTextDialog with pre-populated values
            var dialog = new IfWindowContainsTextDialog(windowIdentifier, identifierType, searchText, caseSensitive, deepSearch)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the IfWindowContainsTextAction with new values
                windowTextAction.WindowIdentifier = dialog.WindowIdentifier;
                windowTextAction.IdentifierType = dialog.IdentifierType;
                windowTextAction.SearchText = dialog.SearchText;
                windowTextAction.CaseSensitive = dialog.CaseSensitive;
                windowTextAction.DeepSearch = dialog.DeepSearch;
                windowTextAction.Description = $"If window '{dialog.WindowIdentifier}' contains '{dialog.SearchText}'{(dialog.CaseSensitive ? " (case-sensitive)" : "")}";

                // Force refresh
                var currentTestFlow = ViewModel.ActiveTestFlow;
                ViewModel.ActiveTestFlow = null;
                ViewModel.ActiveTestFlow = currentTestFlow;
            }
        }

        private void EditLaunchApplicationAction(Model.Actions.System.LaunchApplicationAction launchAction)
        {
            // Extract current values from the LaunchApplicationAction
            string executablePath = launchAction.ExecutablePath;
            string arguments = launchAction.Arguments;
            string workingDirectory = launchAction.WorkingDirectory;
            bool waitForCompletion = launchAction.WaitForCompletion;
            int timeoutMilliseconds = launchAction.TimeoutMilliseconds;

            // Create and show the LaunchApplicationDialog with pre-populated values
            var dialog = new LaunchApplicationDialog(executablePath, arguments, workingDirectory, waitForCompletion, timeoutMilliseconds)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the LaunchApplicationAction with new values
                launchAction.ExecutablePath = dialog.ExecutablePath;
                launchAction.Arguments = dialog.Arguments;
                launchAction.WorkingDirectory = dialog.WorkingDirectory;
                launchAction.WaitForCompletion = dialog.WaitForCompletion;
                launchAction.TimeoutMilliseconds = dialog.TimeoutMilliseconds;

                // Update description with relevant details
                string description = $"Launch: {dialog.ExecutablePath}";
                if (!string.IsNullOrWhiteSpace(dialog.Arguments))
                {
                    description += $" with args: {dialog.Arguments}";
                }
                if (dialog.WaitForCompletion)
                {
                    description += $" (wait {dialog.TimeoutMilliseconds}ms)";
                }
                launchAction.Description = description;

                // Force refresh
                var currentTestFlow = ViewModel.ActiveTestFlow;
                ViewModel.ActiveTestFlow = null;
                ViewModel.ActiveTestFlow = currentTestFlow;
            }
        }

        private void SetupOpenRecentMenu()
        {
            var openRecentMenu = OpenRecentMenu;
            if (openRecentMenu == null) return;

            // Populate the menu when it's submenu opens
            openRecentMenu.SubmenuOpened += (s, e) => PopulateRecentProjectsMenu();
        }

        private void PopulateRecentProjectsMenu()
        {
            var openRecentMenu = OpenRecentMenu;
            if (openRecentMenu == null) return;

            // Clear existing items (keep the placeholder)
            openRecentMenu.Items.Clear();

            var recentProjects = ViewModel.ProjectManager.RecentProjectsManager.GetValidRecentProjects();

            if (recentProjects.Count == 0)
            {
                var noProjectsItem = new System.Windows.Controls.MenuItem
                {
                    Header = "No Recent Projects",
                    IsEnabled = false
                };
                openRecentMenu.Items.Add(noProjectsItem);
            }
            else
            {
                // Add each recent project as a menu item
                foreach (var project in recentProjects)
                {
                    var menuItem = new System.Windows.Controls.MenuItem
                    {
                        Header = project.ProjectName,
                        ToolTip = project.FilePath
                    };

                    // Capture the filePath in a closure variable
                    var filePath = project.FilePath;
                    menuItem.Click += (s, e) => ViewModel.OpenProjectCommand.Execute(filePath);

                    openRecentMenu.Items.Add(menuItem);
                }

                // Add separator and "Clear Recent" option
                openRecentMenu.Items.Add(new Separator());

                var clearRecentItem = new System.Windows.Controls.MenuItem
                {
                    Header = "Clear Recent Projects"
                };
                clearRecentItem.Click += (s, e) =>
                {
                    ViewModel.ProjectManager.RecentProjectsManager.ClearAll();
                    PopulateRecentProjectsMenu();
                };
                openRecentMenu.Items.Add(clearRecentItem);
            }
        }

        /// <summary>
        /// Sets up AvalonDock document management - dynamically creates/removes LayoutDocument instances
        /// </summary>
        private void SetupDocumentManagement()
        {
            // Subscribe to OpenTestFlows collection changes
            ViewModel.OpenTestFlows.CollectionChanged += OpenTestFlows_CollectionChanged;

            // Subscribe to ActiveTestFlow changes to sync with AvalonDock
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.ActiveTestFlow))
                {
                    SyncActiveDocumentWithViewModel();
                }
            };

            // Handle document closing
            var dockingManager = FindDockingManager();
            if (dockingManager != null)
            {
                dockingManager.DocumentClosing += DockingManager_DocumentClosing;
            }
        }

        /// <summary>
        /// Handles changes to the OpenTestFlows collection
        /// </summary>
        private void OpenTestFlows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                // TestFlow added - create a LayoutDocument for it
                foreach (ObservableFlow flow in e.NewItems)
                {
                    CreateDocumentForTestFlow(flow);
                }

                // Hide placeholder if this is the first document
                if (ViewModel.OpenTestFlows.Count > 0 && PlaceholderDocument != null)
                {
                    DocumentPane.Children.Remove(PlaceholderDocument);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                // TestFlow removed - remove its LayoutDocument
                foreach (ObservableFlow flow in e.OldItems)
                {
                    RemoveDocumentForTestFlow(flow);
                }

                // Show placeholder if no documents left
                if (ViewModel.OpenTestFlows.Count == 0 && PlaceholderDocument != null && !DocumentPane.Children.Contains(PlaceholderDocument))
                {
                    DocumentPane.Children.Add(PlaceholderDocument);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                // Clear all documents
                var docsToRemove = DocumentPane.Children.OfType<AvalonDock.Layout.LayoutDocument>()
                    .Where(d => d != PlaceholderDocument)
                    .ToList();
                foreach (var doc in docsToRemove)
                {
                    DocumentPane.Children.Remove(doc);
                }

                // Show placeholder
                if (PlaceholderDocument != null && !DocumentPane.Children.Contains(PlaceholderDocument))
                {
                    DocumentPane.Children.Add(PlaceholderDocument);
                }
            }
        }

        /// <summary>
        /// Creates a LayoutDocument for the given TestFlow
        /// </summary>
        private void CreateDocumentForTestFlow(ObservableFlow flow)
        {
            // Create the content (Grid with toolbar + TreeView)
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Toolbar
            var toolbar = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245)),
                Padding = new Thickness(5),
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var toolbarStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var deleteButton = new Wpf.Ui.Controls.Button
            {
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete20 },
                Appearance = Wpf.Ui.Controls.ControlAppearance.Danger,
                Margin = new Thickness(0, 0, 5, 0),
                ToolTip = "Delete selected action (Delete key)",
                Command = ViewModel.DeleteActionCommand
            };
            toolbarStack.Children.Add(deleteButton);
            toolbar.Child = toolbarStack;
            Grid.SetRow(toolbar, 0);
            grid.Children.Add(toolbar);

            // TreeView
            var treeView = new System.Windows.Controls.TreeView
            {
                DataContext = flow
            };
            treeView.SetBinding(System.Windows.Controls.TreeView.ItemsSourceProperty, new System.Windows.Data.Binding("Actions") { Mode = System.Windows.Data.BindingMode.OneWay });
            treeView.SelectedItemChanged += TestFlowActionsTreeView_SelectedItemChanged;
            treeView.KeyDown += TestFlowActionsTreeView_KeyDown;
            treeView.PreviewMouseDown += TestFlowActionsTreeView_PreviewMouseDown;
            treeView.PreviewMouseRightButtonDown += TestFlowActionsTreeView_PreviewMouseRightButtonDown;
            treeView.MouseDoubleClick += TestFlowActionsTreeView_MouseDoubleClick;

            // Context menu
            var contextMenu = new System.Windows.Controls.ContextMenu();
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "Execute Step",
                Command = ViewModel.ExecuteStepCommand,
                InputGestureText = "F9"
            });
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "Execute from this step",
                Command = ViewModel.ExecuteFromThisStepCommand
            });
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "Add Else Block",
                Command = ViewModel.ToggleElseBlockCommand,
                InputGestureText = "Ctrl+E"
            });
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "Delete Action",
                Command = ViewModel.DeleteActionCommand,
                InputGestureText = "Del"
            });
            treeView.ContextMenu = contextMenu;

            // Set ItemTemplate - create hierarchical template for actions
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);
            factory.SetValue(StackPanel.MarginProperty, new Thickness(2));

            // Status icon
            var statusIcon = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            statusIcon.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("State") { Converter = (IValueConverter)this.FindResource("ActionStateToIconConverter") });
            statusIcon.SetBinding(System.Windows.Controls.TextBlock.ForegroundProperty, new System.Windows.Data.Binding("State") { Converter = (IValueConverter)this.FindResource("ActionStateToColorConverter") });
            statusIcon.SetValue(System.Windows.Controls.TextBlock.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe UI Symbol"));
            statusIcon.SetValue(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold);
            statusIcon.SetValue(System.Windows.Controls.TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
            statusIcon.SetValue(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            statusIcon.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 16.0);
            factory.AppendChild(statusIcon);

            // Action type icon
            var typeIcon = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            typeIcon.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding { Converter = (IValueConverter)this.FindResource("ActionTypeToIconConverter") });
            typeIcon.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 14.0);
            typeIcon.SetValue(System.Windows.Controls.TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            typeIcon.SetValue(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(typeIcon);

            // Action info stack
            var infoStack = new FrameworkElementFactory(typeof(StackPanel));

            // Use ContentPresenter with converter to display formatted name
            var nameContent = new FrameworkElementFactory(typeof(ContentPresenter));
            nameContent.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding
            {
                Converter = (IValueConverter)this.FindResource("ActionToFormattedNameConverter")
            });
            infoStack.AppendChild(nameContent);

            var descText = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            descText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("Description"));
            descText.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 10.0);
            descText.SetValue(System.Windows.Controls.TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray);
            descText.SetValue(System.Windows.Controls.TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            descText.SetValue(System.Windows.Controls.TextBlock.MaxWidthProperty, 300.0);
            infoStack.AppendChild(descText);

            var stateStack = new FrameworkElementFactory(typeof(StackPanel));
            stateStack.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);
            stateStack.SetValue(StackPanel.MarginProperty, new Thickness(0, 2, 0, 0));

            var stateLabel = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            stateLabel.SetValue(System.Windows.Controls.TextBlock.TextProperty, "State: ");
            stateLabel.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 9.0);
            stateLabel.SetValue(System.Windows.Controls.TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray);
            stateStack.AppendChild(stateLabel);

            var stateValue = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            stateValue.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("State"));
            stateValue.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 9.0);
            stateValue.SetValue(System.Windows.Controls.TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Blue);
            stateStack.AppendChild(stateValue);

            var enabledLabel = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            enabledLabel.SetValue(System.Windows.Controls.TextBlock.TextProperty, " | Enabled: ");
            enabledLabel.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 9.0);
            enabledLabel.SetValue(System.Windows.Controls.TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray);
            enabledLabel.SetValue(System.Windows.Controls.TextBlock.MarginProperty, new Thickness(10, 0, 0, 0));
            stateStack.AppendChild(enabledLabel);

            var enabledValue = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            enabledValue.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("IsEnabled"));
            enabledValue.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 9.0);
            enabledValue.SetValue(System.Windows.Controls.TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Green);
            stateStack.AppendChild(enabledValue);

            infoStack.AppendChild(stateStack);
            factory.AppendChild(infoStack);

            var template = new HierarchicalDataTemplate
            {
                VisualTree = factory
            };
            template.ItemsSource = new System.Windows.Data.Binding("ChildActions") { Mode = System.Windows.Data.BindingMode.OneWay };
            treeView.ItemTemplate = template;

            Grid.SetRow(treeView, 1);
            grid.Children.Add(treeView);

            // Create LayoutDocument
            var document = new AvalonDock.Layout.LayoutDocument
            {
                Title = GetDocumentTitle(flow),
                Content = grid,
                CanClose = true
            };

            // Store mapping from document to flow
            _documentFlowMap[document] = flow;

            // Subscribe to flow property changes to update title when Name or HasUnsavedChanges changes
            flow.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ObservableFlow.Name) || e.PropertyName == nameof(ObservableFlow.HasUnsavedChanges))
                {
                    document.Title = GetDocumentTitle(flow);
                }
            };

            DocumentPane.Children.Add(document);

            // Make it active
            document.IsActive = true;
        }

        /// <summary>
        /// Gets the document title for a TestFlow, appending "*" if it has unsaved changes.
        /// </summary>
        private string GetDocumentTitle(ObservableFlow flow)
        {
            return flow.HasUnsavedChanges ? $"{flow.Name}*" : flow.Name;
        }

        /// <summary>
        /// Removes the LayoutDocument for the given TestFlow
        /// </summary>
        private void RemoveDocumentForTestFlow(ObservableFlow flow)
        {
            var docToRemove = _documentFlowMap.FirstOrDefault(kvp => kvp.Value == flow).Key;

            if (docToRemove != null)
            {
                DocumentPane.Children.Remove(docToRemove);
                _documentFlowMap.Remove(docToRemove);
            }
        }

        /// <summary>
        /// Syncs the active document in AvalonDock with the ViewModel's ActiveTestFlow
        /// </summary>
        private void SyncActiveDocumentWithViewModel()
        {
            if (ViewModel.ActiveTestFlow == null) return;

            var doc = _documentFlowMap.FirstOrDefault(kvp => kvp.Value == ViewModel.ActiveTestFlow).Key;

            if (doc != null && !doc.IsActive)
            {
                doc.IsActive = true;
            }
        }

        /// <summary>
        /// Handles document closing in AvalonDock
        /// </summary>
        private void DockingManager_DocumentClosing(object? sender, AvalonDock.DocumentClosingEventArgs e)
        {
            if (_documentFlowMap.TryGetValue(e.Document, out var flow))
            {
                // Close the TestFlow via ViewModel command
                ViewModel.CloseTestFlowTabCommand.Execute(flow);
            }
        }

        /// <summary>
        /// Finds the DockingManager in the visual tree
        /// </summary>
        private AvalonDock.DockingManager? FindDockingManager()
        {
            return FindVisualChild<AvalonDock.DockingManager>(this);
        }

        /// <summary>
        /// Helper to find a child of a specific type in the visual tree
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    return typedChild;
                }

                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}