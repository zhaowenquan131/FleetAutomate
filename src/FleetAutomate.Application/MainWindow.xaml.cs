using FleetAutomate.Dialogs;
using FleetAutomate.ViewModel;

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
        public MainWindow()
        {
            InitializeComponent();
            ViewModel = (MainViewModel)DataContext;

            // Set up UI event handlers
            SetupUIEventHandlers();

            // Set up Open Recent menu
            Loaded += (s, e) => SetupOpenRecentMenu();
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
                    return (dialog.ElementIdentifier, dialog.IdentifierType, dialog.IsDoubleClick, dialog.UseInvoke);
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

        private void ActionsToolBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // TreeView implementation - differentiate between category and action
            if (sender is System.Windows.Controls.TreeView treeView)
            {
                var selectedItem = treeView.SelectedItem;

                // Check if it's an ActionTemplate (leaf node)
                if (selectedItem is FleetAutomate.Model.ActionTemplate actionTemplate)
                {
                    ViewModel.AddActionFromTemplate(actionTemplate);
                    e.Handled = true;
                }
                // If it's an ActionCategory, toggle expansion
                else if (selectedItem is FleetAutomate.Model.ActionCategory category)
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
            var hitTestResult = VisualTreeHelper.HitTest(TestFlowActionsTreeView, e.GetPosition(TestFlowActionsTreeView));

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

        private void TestFlowActionsTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Check if we double-clicked on a TreeViewItem
            var hitTestResult = VisualTreeHelper.HitTest(TestFlowActionsTreeView, e.GetPosition(TestFlowActionsTreeView));
            if (hitTestResult?.VisualHit == null) return;

            // Walk up the visual tree to find the TreeViewItem
            var dependencyObject = hitTestResult.VisualHit as DependencyObject;
            while (dependencyObject != null)
            {
                if (dependencyObject is System.Windows.Controls.TreeViewItem treeViewItem)
                {
                    // Get the action from the TreeViewItem's DataContext
                    if (treeViewItem.DataContext is FleetAutomate.Model.IAction action)
                    {
                        // Special handling for IfAction - use IfActionDialog for editing
                        if (action is FleetAutomate.Model.Actions.Logic.IfAction ifAction)
                        {
                            EditIfAction(ifAction);
                        }
                        // Special handling for ClickElementAction - use ClickElementDialog for editing
                        else if (action is FleetAutomate.Model.Actions.UIAutomation.ClickElementAction clickAction)
                        {
                            EditClickElementAction(clickAction);
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
                                // Properties were saved, force refresh by clearing and re-setting the selected test flow
                                var currentTestFlow = ViewModel.SelectedTestFlow;
                                ViewModel.SelectedTestFlow = null;
                                ViewModel.SelectedTestFlow = currentTestFlow;
                            }
                        }

                        e.Handled = true;
                        return;
                    }
                }
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }
        }

        private void EditIfAction(FleetAutomate.Model.Actions.Logic.IfAction ifAction)
        {
            // Determine the condition type and extract values
            string conditionType;
            string conditionExpression = string.Empty;
            string elementIdentifier = string.Empty;
            string identifierType = "XPath";
            int retryTimes = 1;

            if (ifAction.Condition is FleetAutomate.Model.Actions.Logic.Expression.UIElementExistsExpression uiExpr)
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
            var dialog = new FleetAutomate.Dialogs.IfActionDialog(conditionType, conditionExpression, elementIdentifier, identifierType, retryTimes)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the IfAction with new values
                if (dialog.ConditionType == "Expression")
                {
                    var parsedCondition = FleetAutomate.Model.Actions.Logic.Expression.BooleanExpressionParser.Parse(dialog.ConditionExpression);
                    if (parsedCondition != null)
                    {
                        ifAction.Condition = parsedCondition;
                        ifAction.Description = $"If {dialog.ConditionExpression}";
                    }
                }
                else if (dialog.ConditionType == "UIElementExists")
                {
                    ifAction.Condition = new FleetAutomate.Model.Actions.Logic.Expression.UIElementExistsExpression(
                        dialog.ElementIdentifier,
                        dialog.IdentifierType,
                        1000,
                        dialog.RetryTimes
                    );
                    ifAction.Description = $"If element '{dialog.ElementIdentifier}' exists (retry {dialog.RetryTimes}x)";
                }

                // Force refresh
                var currentTestFlow = ViewModel.SelectedTestFlow;
                ViewModel.SelectedTestFlow = null;
                ViewModel.SelectedTestFlow = currentTestFlow;
            }
        }

        private void EditClickElementAction(FleetAutomate.Model.Actions.UIAutomation.ClickElementAction clickAction)
        {
            // Extract current values from the ClickElementAction
            string elementIdentifier = clickAction.ElementIdentifier;
            string identifierType = clickAction.IdentifierType;
            bool isDoubleClick = clickAction.IsDoubleClick;
            bool useInvoke = clickAction.UseInvoke;

            // Create and show the ClickElementDialog with pre-populated values
            var dialog = new FleetAutomate.Dialogs.ClickElementDialog(elementIdentifier, identifierType, isDoubleClick, useInvoke)
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
                clickAction.Description = $"Click {(dialog.IsDoubleClick ? "(double) " : "")}{(dialog.UseInvoke ? "(invoke) " : "")}element: {dialog.IdentifierType}={dialog.ElementIdentifier}";

                // Force refresh
                var currentTestFlow = ViewModel.SelectedTestFlow;
                ViewModel.SelectedTestFlow = null;
                ViewModel.SelectedTestFlow = currentTestFlow;
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
    }
}