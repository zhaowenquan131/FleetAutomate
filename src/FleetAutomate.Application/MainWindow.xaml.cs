using FleetAutomate.View.Dialog;
using FleetAutomate.ViewModel;

using System.Collections.Generic;
using System.Linq;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
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
        private System.Windows.Point _toolboxDragStartPoint;
        private System.Windows.Point _testFlowDragStartPoint;
        private Model.ActionTemplate? _pendingToolboxDragTemplate;
        private Model.IAction? _pendingTestFlowDragAction;
        private System.Windows.Controls.TreeViewItem? _activeDropHighlightItem;
        private DropPlacement? _activeDropPlacement;
        private const string ToolboxActionTemplateFormat = "FleetAutomate.ActionTemplate";
        private const string TestFlowActionFormat = "FleetAutomate.TestFlowAction";

        private enum DropPlacement
        {
            Before,
            Into,
            After
        }

        private sealed record DropInstruction(
            Model.IAction? InsertionTarget,
            Model.IAction? ValidationTarget,
            DropPlacement Placement,
            System.Windows.Controls.TreeViewItem? HighlightItem);

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
            Loaded += OnMainWindowLoaded;
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Application.Current is FleetAutomate.App app)
            {
                app.EnsureUiSessionHost(this);
            }
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
                var scopeKeys = GetAvailableScopeOptions();
                var dialog = new WaitForElementDialog(scopeKeys)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.ElementIdentifier, dialog.IdentifierType, dialog.TimeoutMilliseconds, dialog.PollingIntervalMilliseconds, dialog.SearchScope, dialog.AddToGlobalDictionary);
                }

                return null; // User cancelled
            };

            // Handle click element prompt
            ViewModel.OnPromptClickElement += () =>
            {
                var scopeKeys = GetAvailableScopeOptions();
                var dialog = new ClickElementDialog(scopeKeys)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.ElementIdentifier, dialog.IdentifierType, dialog.IsDoubleClick, dialog.UseInvoke, dialog.InvokeWithoutWaiting, dialog.RetryTimes, dialog.RetryDelayMilliseconds, dialog.SearchScope, dialog.AddToGlobalDictionary);
                }

                return null; // User cancelled
            };

            // Handle set text prompt
            ViewModel.OnPromptSetText += () =>
            {
                var scopeKeys = GetAvailableScopeOptions();
                var dialog = new SetTextDialog(scopeKeys)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.ElementIdentifier, dialog.IdentifierType, dialog.TextToSet, dialog.ClearExistingText, dialog.RetryTimes, dialog.RetryDelayMilliseconds, dialog.SearchScope, dialog.AddToGlobalDictionary);
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

            // Handle log action prompt
            ViewModel.OnPromptLogAction += () =>
            {
                var dialog = new LogActionDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return (dialog.LogLevel, dialog.Message);
                }

                return null; // User cancelled
            };

            // Handle sub flow prompt
            ViewModel.OnPromptSubFlow += (availableFlows) =>
            {
                var dialog = new SubFlowDialog(availableFlows)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    return dialog.SelectedFlowName;
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

            // Handle info messages
            ViewModel.OnShowInfo += async (title, message) =>
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

        private void ActionsToolBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _toolboxDragStartPoint = e.GetPosition(this);
            _pendingToolboxDragTemplate = FindAncestor<System.Windows.Controls.TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext as Model.ActionTemplate;
        }

        private void ActionsToolBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_pendingToolboxDragTemplate == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPosition = e.GetPosition(this);
            if (Math.Abs(currentPosition.X - _toolboxDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - _toolboxDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var data = new System.Windows.DataObject(ToolboxActionTemplateFormat, _pendingToolboxDragTemplate);
            System.Windows.DragDrop.DoDragDrop(ActionsToolBox, data, System.Windows.DragDropEffects.Copy);
            _pendingToolboxDragTemplate = null;
        }

        private void ActionsToolBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.TreeView treeView)
            {
                return;
            }

            var treeViewItem = FindAncestor<System.Windows.Controls.TreeViewItem>(e.OriginalSource as DependencyObject);
            if (treeViewItem == null)
            {
                return;
            }

            treeViewItem.IsSelected = true;
            e.Handled = false;
        }

        private void ActionsToolBox_ContextMenu_Opening(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ContextMenu contextMenu)
            {
                return;
            }

            var selectedTemplate = ActionsToolBox?.SelectedItem as Model.ActionTemplate;
            foreach (var item in contextMenu.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                item.IsEnabled = selectedTemplate != null;
            }
        }

        private void CopyActionTemplateNameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ActionsToolBox?.SelectedItem is not Model.ActionTemplate actionTemplate)
            {
                return;
            }

            const int maxAttempts = 5;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(actionTemplate.Name);
                    return;
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    if (attempt < maxAttempts)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                }
            }

            try
            {
                System.Windows.MessageBox.Show(
                    "剪贴板当前被其他程序占用，复制名称失败，请稍后重试。",
                    "Copy Name Failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            catch
            {
                // Do not let clipboard/messagebox failures bring down the app.
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = current switch
                {
                    Visual visual => VisualTreeHelper.GetParent(visual),
                    System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                    FrameworkContentElement contentElement => contentElement.Parent,
                    _ => null
                };
            }

            return null;
        }

        private System.Windows.Controls.TreeViewItem? ResolveDropTargetItem(System.Windows.Controls.TreeView treeView, DependencyObject? originalSource, System.Windows.Point fallbackPoint)
        {
            var treeViewItem = FindAncestor<System.Windows.Controls.TreeViewItem>(originalSource);
            if (treeViewItem != null)
            {
                return treeViewItem;
            }

            var hit = VisualTreeHelper.HitTest(treeView, fallbackPoint);
            return FindAncestor<System.Windows.Controls.TreeViewItem>(hit?.VisualHit as DependencyObject);
        }

        private DropInstruction ResolveDropInstruction(System.Windows.Controls.TreeView treeView, DependencyObject? originalSource, System.Windows.Point fallbackPoint)
        {
            var dropTargetItem = ResolveDropTargetItem(treeView, originalSource, fallbackPoint);
            if (dropTargetItem?.DataContext is not Model.IAction dropTarget)
            {
                return new DropInstruction(null, null, DropPlacement.After, null);
            }

            var placement = ResolveDropPlacement(dropTargetItem, fallbackPoint);

            if (dropTarget is Model.ActionBlock actionBlock)
            {
                return new DropInstruction(actionBlock, actionBlock.ParentIfAction, DropPlacement.Into, dropTargetItem);
            }

            if (placement == DropPlacement.Into && dropTarget is Model.Actions.Logic.IfAction ifAction)
            {
                var syntheticIfBlock = new Model.ActionBlock
                {
                    ParentIfAction = ifAction,
                    ManagedCollection = ifAction.IfBlock,
                    Name = "If Block",
                    Description = "If block"
                };

                return new DropInstruction(syntheticIfBlock, ifAction, DropPlacement.Into, dropTargetItem);
            }

            return new DropInstruction(dropTarget, dropTarget, placement, dropTargetItem);
        }

        private static DropPlacement ResolveDropPlacement(System.Windows.Controls.TreeViewItem targetItem, System.Windows.Point treePoint)
        {
            if (targetItem.DataContext is Model.ActionBlock)
            {
                return DropPlacement.Into;
            }

            var localPoint = targetItem.TranslatePoint(treePoint, targetItem);
            var height = Math.Max(1.0, targetItem.ActualHeight);
            var topZone = height * 0.25;
            var bottomZone = height * 0.75;

            if (targetItem.DataContext is Model.Actions.Logic.IfAction)
            {
                if (localPoint.Y < topZone)
                {
                    return DropPlacement.Before;
                }

                if (localPoint.Y > bottomZone)
                {
                    return DropPlacement.After;
                }

                return DropPlacement.Into;
            }

            return localPoint.Y < height * 0.5
                ? DropPlacement.Before
                : DropPlacement.After;
        }

        private static FleetAutomate.ViewModel.ActionInsertionMode GetInsertionMode(DropPlacement placement)
        {
            return placement switch
            {
                DropPlacement.Before => FleetAutomate.ViewModel.ActionInsertionMode.Before,
                DropPlacement.Into => FleetAutomate.ViewModel.ActionInsertionMode.Into,
                _ => FleetAutomate.ViewModel.ActionInsertionMode.After
            };
        }

        private void SetActiveDropHighlight(System.Windows.Controls.TreeViewItem? targetItem, DropPlacement placement)
        {
            if (ReferenceEquals(_activeDropHighlightItem, targetItem) && _activeDropPlacement == placement)
            {
                return;
            }

            if (_activeDropHighlightItem != null)
            {
                _activeDropHighlightItem.ClearValue(FrameworkElement.TagProperty);
            }

            _activeDropHighlightItem = targetItem;
            _activeDropPlacement = placement;

            if (_activeDropHighlightItem != null)
            {
                _activeDropHighlightItem.Tag = placement switch
                {
                    DropPlacement.Before => "DropBefore",
                    DropPlacement.Into => "DropInto",
                    _ => "DropAfter"
                };
            }
        }

        private void ClearActiveDropHighlight()
        {
            if (_activeDropHighlightItem != null)
            {
                _activeDropHighlightItem.ClearValue(FrameworkElement.TagProperty);
            }

            _activeDropHighlightItem = null;
            _activeDropPlacement = null;
        }

        private bool IsDescendantDropTarget(Model.IAction draggedAction, Model.IAction? dropTarget)
        {
            if (dropTarget == null || ReferenceEquals(draggedAction, dropTarget))
            {
                return false;
            }

            if (draggedAction is not Model.ICompositeAction compositeAction)
            {
                return false;
            }

            return ContainsActionRecursive(compositeAction.GetChildActions(), dropTarget);
        }

        private bool ContainsActionRecursive(IEnumerable<Model.IAction> actions, Model.IAction target)
        {
            foreach (var action in actions)
            {
                if (ReferenceEquals(action, target))
                {
                    return true;
                }

                if (action is Model.ICompositeAction compositeAction &&
                    ContainsActionRecursive(compositeAction.GetChildActions(), target))
                {
                    return true;
                }
            }

            return false;
        }

        private void MoveDraggedAction(ObservableFlow flow, Model.IAction draggedAction, Model.IAction? dropTarget, FleetAutomate.ViewModel.ActionInsertionMode insertionMode)
        {
            if (ViewModel.ActiveTestFlow != flow)
            {
                return;
            }

            if (!ViewModel.RemoveAction(draggedAction))
            {
                return;
            }

            ViewModel.InsertAction(draggedAction, dropTarget, insertionMode);
        }

        private void TestFlowActionsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is System.Windows.Controls.TreeView treeView && treeView.DataContext is ObservableFlow flow)
            {
                ViewModel.ActiveTestFlow = flow;
            }

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
            _testFlowDragStartPoint = e.GetPosition(this);
            _pendingTestFlowDragAction = FindAncestor<System.Windows.Controls.TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext as Model.IAction;

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

        private void TestFlowActionsTreeView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_pendingTestFlowDragAction == null || _pendingTestFlowDragAction is Model.ActionBlock || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPosition = e.GetPosition(this);
            if (Math.Abs(currentPosition.X - _testFlowDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - _testFlowDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (sender is not System.Windows.Controls.TreeView treeView || treeView.DataContext is not ObservableFlow)
            {
                return;
            }

            var data = new System.Windows.DataObject(TestFlowActionFormat, _pendingTestFlowDragAction);
            try
            {
                System.Windows.DragDrop.DoDragDrop(treeView, data, System.Windows.DragDropEffects.Move);
            }
            finally
            {
                _pendingTestFlowDragAction = null;
                ClearActiveDropHighlight();
            }
        }

        private void TestFlowActionsTreeView_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            var acceptsToolbox = e.Data.GetDataPresent(ToolboxActionTemplateFormat);
            var acceptsReorder = e.Data.GetDataPresent(TestFlowActionFormat);
            e.Effects = acceptsToolbox
                ? System.Windows.DragDropEffects.Copy
                : acceptsReorder ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;

            if (sender is System.Windows.Controls.TreeView treeView && e.Effects != System.Windows.DragDropEffects.None)
            {
                var instruction = ResolveDropInstruction(treeView, e.OriginalSource as DependencyObject, e.GetPosition(treeView));
                SetActiveDropHighlight(instruction.HighlightItem, instruction.Placement);
            }
            else
            {
                ClearActiveDropHighlight();
            }

            e.Handled = true;
        }

        private void TestFlowActionsTreeView_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is not System.Windows.Controls.TreeView treeView || treeView.DataContext is not ObservableFlow flow)
            {
                return;
            }

            ViewModel.ActiveTestFlow = flow;

            var instruction = ResolveDropInstruction(treeView, e.OriginalSource as DependencyObject, e.GetPosition(treeView));
            var insertionMode = GetInsertionMode(instruction.Placement);

            if (e.Data.GetDataPresent(ToolboxActionTemplateFormat))
            {
                if (e.Data.GetData(ToolboxActionTemplateFormat) is Model.ActionTemplate actionTemplate)
                {
                    ViewModel.AddActionFromTemplate(actionTemplate, instruction.InsertionTarget, insertionMode);
                }
            }
            else if (e.Data.GetDataPresent(TestFlowActionFormat))
            {
                if (e.Data.GetData(TestFlowActionFormat) is Model.IAction draggedAction &&
                    draggedAction != instruction.ValidationTarget &&
                    !IsDescendantDropTarget(draggedAction, instruction.ValidationTarget))
                {
                    MoveDraggedAction(flow, draggedAction, instruction.InsertionTarget, insertionMode);
                }
            }

            ClearActiveDropHighlight();
            e.Handled = true;
        }

        private void TestFlowActionsTreeView_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            ClearActiveDropHighlight();
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
                        // Special handling for WaitForElementAction - use WaitForElementDialog for editing
                        else if (action is Model.Actions.UIAutomation.WaitForElementAction waitAction)
                        {
                            EditWaitForElementAction(waitAction);
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
                        // Special handling for LogAction - use LogActionDialog for editing
                        else if (action is Model.Actions.System.LogAction logAction)
                        {
                            EditLogAction(logAction);
                        }
                        // Special handling for SetVariableAction<T> - use SetVariableDialog for editing
                        else if (IsSetVariableAction(action))
                        {
                            EditSetVariableAction(action);
                        }
                        // Special handling for SubFlowAction - use SubFlowDialog for editing
                        else if (action is Model.Actions.Logic.SubFlowAction subFlowAction)
                        {
                            EditSubFlowAction(subFlowAction);
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
            else if (ifAction.Condition is Model.Actions.Logic.ExpressionBase<bool> boolExpr)
            {
                conditionType = "Expression";
                conditionExpression = boolExpr.RawText ?? string.Empty;
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
            bool invokeWithoutWaiting = clickAction.InvokeWithoutWaiting;
            int retryTimes = clickAction.RetryTimes;
            int retryDelayMilliseconds = clickAction.RetryDelayMilliseconds;
            string? searchScope = clickAction.SearchScope;
            bool addToGlobalDictionary = clickAction.AddToGlobalDictionary;

            // Get available scope keys from actions BEFORE this action
            var scopeKeys = GetAvailableScopeOptions(clickAction);

            // Create and show the ClickElementDialog with pre-populated values
            var dialog = new ClickElementDialog(
                elementIdentifier, identifierType, isDoubleClick, useInvoke, invokeWithoutWaiting,
                retryTimes, retryDelayMilliseconds, searchScope, addToGlobalDictionary, scopeKeys)
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
                clickAction.InvokeWithoutWaiting = dialog.UseInvoke && dialog.InvokeWithoutWaiting;
                clickAction.RetryTimes = dialog.RetryTimes;
                clickAction.RetryDelayMilliseconds = dialog.RetryDelayMilliseconds;
                clickAction.SearchScope = dialog.SearchScope;
                clickAction.AddToGlobalDictionary = dialog.AddToGlobalDictionary;
                clickAction.Description = $"{FormatElementDescription(dialog.ElementIdentifier, dialog.IdentifierType)}{(dialog.IsDoubleClick ? " (double)" : "")}{(dialog.UseInvoke ? (dialog.InvokeWithoutWaiting ? " (invoke no-wait)" : " (invoke)") : "")} (retry:{dialog.RetryTimes}x)";

                // Register key to GlobalElementDictionary if AddToGlobalDictionary is checked
                if (dialog.AddToGlobalDictionary)
                {
                    var activeFlow = ViewModel.ActiveTestFlow?.Model;
                    activeFlow?.GlobalElementDictionary?.RegisterKey(dialog.ElementIdentifier);
                }

                // Force refresh
                var currentTestFlow = ViewModel.ActiveTestFlow;
                ViewModel.ActiveTestFlow = null;
                ViewModel.ActiveTestFlow = currentTestFlow;
            }
        }

        private void EditWaitForElementAction(Model.Actions.UIAutomation.WaitForElementAction waitAction)
        {
            var scopeKeys = GetAvailableScopeOptions(waitAction);
            var dialog = new WaitForElementDialog(
                waitAction.ElementIdentifier,
                waitAction.IdentifierType,
                waitAction.TimeoutMilliseconds,
                waitAction.PollingIntervalMilliseconds,
                waitAction.SearchScope,
                waitAction.AddToGlobalDictionary,
                scopeKeys)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                waitAction.ElementIdentifier = dialog.ElementIdentifier;
                waitAction.IdentifierType = dialog.IdentifierType;
                waitAction.TimeoutMilliseconds = dialog.TimeoutMilliseconds;
                waitAction.PollingIntervalMilliseconds = dialog.PollingIntervalMilliseconds;
                waitAction.SearchScope = dialog.SearchScope;
                waitAction.AddToGlobalDictionary = dialog.AddToGlobalDictionary;
                waitAction.Description = $"{FormatElementDescription(dialog.ElementIdentifier, dialog.IdentifierType)} (timeout:{dialog.TimeoutMilliseconds}ms)";

                if (dialog.AddToGlobalDictionary)
                {
                    var activeFlow = ViewModel.ActiveTestFlow?.Model;
                    activeFlow?.GlobalElementDictionary?.RegisterKey(dialog.ElementIdentifier);
                }

                RefreshActiveTestFlow();
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
            string? searchScope = setTextAction.SearchScope;
            bool addToGlobalDictionary = setTextAction.AddToGlobalDictionary;

            // Get available scope keys from actions BEFORE this action
            var scopeKeys = GetAvailableScopeOptions(setTextAction);

            // Create and show the SetTextDialog with pre-populated values
            var dialog = new SetTextDialog(
                elementIdentifier, identifierType, textToSet, clearExistingText,
                retryTimes, retryDelayMilliseconds, searchScope, addToGlobalDictionary, scopeKeys)
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
                setTextAction.SearchScope = dialog.SearchScope;
                setTextAction.AddToGlobalDictionary = dialog.AddToGlobalDictionary;
                setTextAction.Description = $"{FormatElementDescription(dialog.ElementIdentifier, dialog.IdentifierType)} = '{dialog.TextToSet}'{(dialog.ClearExistingText ? " (clear first)" : "")} (retry:{dialog.RetryTimes}x)";

                // Register key to GlobalElementDictionary if AddToGlobalDictionary is checked
                if (dialog.AddToGlobalDictionary)
                {
                    var activeFlow = ViewModel.ActiveTestFlow?.Model;
                    activeFlow?.GlobalElementDictionary?.RegisterKey(dialog.ElementIdentifier);
                }

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

        private void EditLogAction(Model.Actions.System.LogAction logAction)
        {
            // Extract current values from the LogAction
            var logLevel = logAction.LogLevel;
            string message = logAction.Message;

            // Create and show the LogActionDialog with pre-populated values
            var dialog = new LogActionDialog(logLevel, message)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the LogAction with new values
                logAction.LogLevel = dialog.LogLevel;
                logAction.Message = dialog.Message;
                logAction.Description = $"Log [{dialog.LogLevel}]: {(dialog.Message.Length > 30 ? dialog.Message.Substring(0, 27) + "..." : dialog.Message)}";

                // Force refresh
                var currentTestFlow = ViewModel.ActiveTestFlow;
                ViewModel.ActiveTestFlow = null;
                ViewModel.ActiveTestFlow = currentTestFlow;
            }
        }

        private void EditSetVariableAction(Model.IAction action)
        {
            var variableProperty = action.GetType().GetProperty("Variable");
            var variable = variableProperty?.GetValue(action) as Model.Actions.Logic.Variable;
            if (variable == null)
            {
                return;
            }

            var dialog = new SetVariableDialog(
                variable.Name,
                GetVariableTypeKey(variable.Type),
                variable.Value?.ToString() ?? string.Empty)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                var parsedValue = Model.Actions.Logic.Expression.LiteralExpressionFactory.CreateLiteral(dialog.VariableValue, dialog.VariableType);
                if (parsedValue == null)
                {
                    return;
                }

                variable.Name = dialog.VariableName;
                variable.Value = parsedValue;
                variable.Type = GetTypeFromVariableKey(dialog.VariableType);

                var descriptionProperty = action.GetType().GetProperty("Description");
                descriptionProperty?.SetValue(action, $"Set {variable.ShortTypeName} {variable.Name} = {variable.Value}");

                RefreshActiveTestFlow();
            }
        }

        private void EditSubFlowAction(Model.Actions.Logic.SubFlowAction subFlowAction)
        {
            var availableFlows = ViewModel.ActiveProject?.Model?.TestFlows?
                .Where(flow => flow.IsEnabled
                    && !string.IsNullOrWhiteSpace(flow.Name)
                    && !string.Equals(flow.Name, ViewModel.ActiveTestFlow?.Name, StringComparison.Ordinal))
                .Select(flow => flow.Name)
                .ToList() ?? [];

            var dialog = new SubFlowDialog(availableFlows, subFlowAction.TargetFlowName)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedFlowName))
            {
                subFlowAction.TargetFlowName = dialog.SelectedFlowName;
                subFlowAction.Description = $"Execute sub-flow: {dialog.SelectedFlowName}";
                subFlowAction.TestProject = ViewModel.ActiveProject?.Model;

                RefreshActiveTestFlow();
            }
        }

        private static string GetVariableTypeKey(Type? type)
        {
            if (type == typeof(int))
                return "int";
            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
                return "double";
            if (type == typeof(bool))
                return "bool";

            return "string";
        }

        private static Type GetTypeFromVariableKey(string variableType)
        {
            return variableType switch
            {
                "int" => typeof(int),
                "double" => typeof(double),
                "bool" => typeof(bool),
                _ => typeof(string)
            };
        }

        private void RefreshActiveTestFlow()
        {
            var currentTestFlow = ViewModel.ActiveTestFlow;
            ViewModel.ActiveTestFlow = null;
            ViewModel.ActiveTestFlow = currentTestFlow;
        }

        /// <summary>
        /// Gets the available scope keys from actions that appear BEFORE the current action.
        /// Only actions with AddToGlobalDictionary=true are included as potential search scopes.
        /// </summary>
        /// <param name="currentAction">The current action being edited/created. If null, all existing actions are considered (for new actions added at end).</param>
        /// <returns>Collection of scope keys from preceding actions.</returns>
        private IEnumerable<Helpers.SearchScopeOption> GetAvailableScopeOptions(Model.IAction? currentAction = null)
        {
            var activeFlow = ViewModel.ActiveTestFlow?.Model;
            if (activeFlow == null)
                return [];

            var scopeOptions = new List<Helpers.SearchScopeOption>();
            var addedKeys = new HashSet<string>();
            var actionSequence = 0;
            CollectScopeOptionsBeforeAction(activeFlow.Actions, currentAction, scopeOptions, addedKeys, ref actionSequence, out _);
            return scopeOptions;
        }

        /// <summary>
        /// Recursively collects scope keys from IUIElementAction instances that have AddToGlobalDictionary=true,
        /// stopping when the target action is found.
        /// </summary>
        /// <param name="actions">The collection of actions to traverse.</param>
        /// <param name="targetAction">The action to stop at (not included). If null, traverse all actions.</param>
        /// <param name="scopeKeys">The list to collect scope keys into.</param>
        /// <param name="foundTarget">Output: true if the target action was found.</param>
        private void CollectScopeOptionsBeforeAction(
            IEnumerable<Model.IAction> actions,
            Model.IAction? targetAction,
            List<Helpers.SearchScopeOption> scopeOptions,
            HashSet<string> addedKeys,
            ref int actionSequence,
            out bool foundTarget)
        {
            foundTarget = false;

            foreach (var action in actions)
            {
                actionSequence++;

                // If this is the target action, stop here (don't include its key)
                if (targetAction != null && ReferenceEquals(action, targetAction))
                {
                    foundTarget = true;
                    return;
                }

                // If this action is an IUIElementAction with AddToGlobalDictionary=true, collect its key
                if (action is Model.Actions.UIAutomation.IUIElementAction uiAction && uiAction.AddToGlobalDictionary)
                {
                    // Use GlobalDictionaryKey if specified, otherwise use ElementIdentifier
                    var key = string.IsNullOrEmpty(uiAction.GlobalDictionaryKey)
                        ? uiAction.ElementIdentifier
                        : uiAction.GlobalDictionaryKey;

                    if (!string.IsNullOrEmpty(key) && addedKeys.Add(key))
                    {
                        var elementSummary = Helpers.ElementDescriptionHelper.ExtractElementDescription(
                            uiAction.ElementIdentifier,
                            uiAction.IdentifierType);
                        var displayText = string.Equals(key, uiAction.ElementIdentifier, StringComparison.Ordinal)
                            ? $"{actionSequence}. {action.Name} - {elementSummary}"
                            : $"{actionSequence}. {action.Name} - {elementSummary} [{key}]";
                        scopeOptions.Add(new Helpers.SearchScopeOption(key, displayText));
                    }
                }

                // Recurse through composite children via the shared interface so loop implementations
                // do not need bespoke UI handling when their internal collection name changes.
                if (action is Model.ICompositeAction compositeAction)
                {
                    CollectScopeOptionsBeforeAction(compositeAction.GetChildActions(), targetAction, scopeOptions, addedKeys, ref actionSequence, out foundTarget);
                    if (foundTarget)
                        return;
                }

                // IfAction also exposes an else branch outside GetChildActions().
                if (action is Model.Actions.Logic.IfAction ifAction)
                {
                    CollectScopeOptionsBeforeAction(ifAction.ElseBlock, targetAction, scopeOptions, addedKeys, ref actionSequence, out foundTarget);
                    if (foundTarget)
                        return;
                }
            }
        }

        private static bool IsSetVariableAction(Model.IAction action)
        {
            var actionType = action.GetType();
            return actionType.IsGenericType
                && actionType.GetGenericTypeDefinition() == typeof(Model.Actions.Logic.SetVariableAction<>);
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
            var visibilityBinding = new System.Windows.Data.Binding(nameof(MainViewModel.HasSelectedAction))
            {
                Source = ViewModel,
                Converter = new BooleanToVisibilityConverter()
            };
            var moveUpButton = new Wpf.Ui.Controls.Button
            {
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowUp20 },
                Margin = new Thickness(0, 0, 5, 0),
                ToolTip = "Move selected action up",
                Command = ViewModel.MoveSelectedActionUpCommand
            };
            moveUpButton.SetBinding(VisibilityProperty, visibilityBinding);
            toolbarStack.Children.Add(moveUpButton);

            var moveDownButton = new Wpf.Ui.Controls.Button
            {
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowDown20 },
                Margin = new Thickness(0, 0, 5, 0),
                ToolTip = "Move selected action down",
                Command = ViewModel.MoveSelectedActionDownCommand
            };
            moveDownButton.SetBinding(VisibilityProperty, visibilityBinding);
            toolbarStack.Children.Add(moveDownButton);

            var stepButton = new Wpf.Ui.Controls.Button
            {
                Content = "Step",
                Margin = new Thickness(0, 0, 5, 0),
                ToolTip = "Execute selected action",
                Command = ViewModel.ExecuteStepCommand
            };
            stepButton.SetBinding(VisibilityProperty, visibilityBinding);
            toolbarStack.Children.Add(stepButton);

            var runFromButton = new Wpf.Ui.Controls.Button
            {
                Content = "Run From",
                Margin = new Thickness(0, 0, 5, 0),
                ToolTip = "Execute TestFlow starting from selected action",
                Command = ViewModel.ExecuteFromThisStepCommand
            };
            runFromButton.SetBinding(VisibilityProperty, visibilityBinding);
            toolbarStack.Children.Add(runFromButton);

            var skipFailedButton = new Wpf.Ui.Controls.Button
            {
                Content = "Skip Failed",
                Margin = new Thickness(0, 0, 5, 0),
                ToolTip = "Skip current failed action and continue",
                Command = ViewModel.SkipFailedActionCommand
            };
            var skipFailedVisibilityBinding = new System.Windows.Data.Binding(nameof(MainViewModel.CanSkipFailedAction))
            {
                Source = ViewModel,
                Converter = new BooleanToVisibilityConverter()
            };
            skipFailedButton.SetBinding(VisibilityProperty, skipFailedVisibilityBinding);
            toolbarStack.Children.Add(skipFailedButton);

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
            treeView.AllowDrop = true;
            var selectedBackgroundBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(221, 238, 255));
            treeView.Resources[System.Windows.SystemColors.HighlightBrushKey] = selectedBackgroundBrush;
            treeView.Resources[System.Windows.SystemColors.InactiveSelectionHighlightBrushKey] = selectedBackgroundBrush;
            treeView.Resources[System.Windows.SystemColors.HighlightTextBrushKey] = System.Windows.Media.Brushes.Black;
            treeView.Resources[System.Windows.SystemColors.InactiveSelectionHighlightTextBrushKey] = System.Windows.Media.Brushes.Black;
            treeView.SetBinding(System.Windows.Controls.TreeView.ItemsSourceProperty, new System.Windows.Data.Binding("Actions") { Mode = System.Windows.Data.BindingMode.OneWay });
            treeView.SelectedItemChanged += TestFlowActionsTreeView_SelectedItemChanged;
            treeView.KeyDown += TestFlowActionsTreeView_KeyDown;
            treeView.PreviewMouseDown += TestFlowActionsTreeView_PreviewMouseDown;
            treeView.PreviewMouseMove += TestFlowActionsTreeView_PreviewMouseMove;
            treeView.PreviewMouseRightButtonDown += TestFlowActionsTreeView_PreviewMouseRightButtonDown;
            treeView.MouseDoubleClick += TestFlowActionsTreeView_MouseDoubleClick;
            treeView.DragOver += TestFlowActionsTreeView_DragOver;
            treeView.Drop += TestFlowActionsTreeView_Drop;
            treeView.DragLeave += TestFlowActionsTreeView_DragLeave;

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
            var statusIconBinding = new MultiBinding
            {
                Converter = (IMultiValueConverter)this.FindResource("ActionStateToIconConverter")
            };
            statusIconBinding.Bindings.Add(new System.Windows.Data.Binding("."));
            statusIconBinding.Bindings.Add(new System.Windows.Data.Binding("State"));
            statusIcon.SetBinding(System.Windows.Controls.TextBlock.TextProperty, statusIconBinding);
            statusIcon.SetBinding(System.Windows.Controls.TextBlock.ForegroundProperty, new System.Windows.Data.Binding("State") { Converter = (IValueConverter)this.FindResource("ActionStateToColorConverter") });
            statusIcon.SetValue(System.Windows.Controls.TextBlock.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe UI Symbol"));
            statusIcon.SetValue(System.Windows.Controls.TextBlock.FontWeightProperty, FontWeights.Bold);
            statusIcon.SetValue(System.Windows.Controls.TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
            statusIcon.SetValue(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            statusIcon.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 16.0);
            factory.AppendChild(statusIcon);

            // Action type icon
            var typeIconHost = new FrameworkElementFactory(typeof(Grid));
            typeIconHost.SetValue(FrameworkElement.WidthProperty, 18.0);
            typeIconHost.SetValue(FrameworkElement.HeightProperty, 18.0);
            typeIconHost.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 5, 0));

            var typeIcon = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            typeIcon.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding
            {
                Converter = (IValueConverter)this.FindResource("ActionTypeToIconConverter")
            });
            typeIcon.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding(".")
            {
                Converter = (IValueConverter)this.FindResource("ActionTypeToIconVisibilityConverter")
            });
            typeIcon.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, 14.0);
            typeIcon.SetValue(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            typeIcon.SetValue(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            typeIconHost.AppendChild(typeIcon);

            var symbolIcon = new FrameworkElementFactory(typeof(Wpf.Ui.Controls.SymbolIcon));
            symbolIcon.SetBinding(Wpf.Ui.Controls.SymbolIcon.SymbolProperty, new System.Windows.Data.Binding(".")
            {
                Converter = (IValueConverter)this.FindResource("ActionTypeToSymbolConverter")
            });
            symbolIcon.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding(".")
            {
                Converter = (IValueConverter)this.FindResource("ActionTypeToIconVisibilityConverter"),
                ConverterParameter = "Symbol"
            });
            symbolIcon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            symbolIcon.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            symbolIcon.SetValue(Wpf.Ui.Controls.SymbolIcon.FontSizeProperty, 14.0);
            typeIconHost.AppendChild(symbolIcon);

            factory.AppendChild(typeIconHost);

            // Action info stack
            var infoStack = new FrameworkElementFactory(typeof(StackPanel));

            // Use ContentPresenter with converter to display formatted name
            var nameContent = new FrameworkElementFactory(typeof(ContentPresenter));
            nameContent.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding(".")
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

            // Create ItemContainerStyle to highlight executing actions
            var itemContainerStyle = new Style(typeof(System.Windows.Controls.TreeViewItem));
            itemContainerStyle.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BorderBrushProperty, Brushes.Transparent));
            itemContainerStyle.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BorderThicknessProperty, new Thickness(1)));

            // Bind Background to State with ActionStateToBackgroundConverter
            var backgroundBinding = new System.Windows.Data.Binding("State")
            {
                Converter = (IValueConverter)this.FindResource("ActionStateToBackgroundConverter")
            };
            itemContainerStyle.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BackgroundProperty, backgroundBinding));
            itemContainerStyle.Triggers.Add(new Trigger
            {
                Property = System.Windows.Controls.TreeViewItem.IsSelectedProperty,
                Value = true,
                Setters =
                {
                    new Setter(System.Windows.Controls.TreeViewItem.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(221, 238, 255))),
                    new Setter(System.Windows.Controls.TreeViewItem.ForegroundProperty, System.Windows.Media.Brushes.Black)
                }
            });
            var dropIntoTrigger = new MultiTrigger();
            dropIntoTrigger.Conditions.Add(new Condition(FrameworkElement.TagProperty, "DropInto"));
            dropIntoTrigger.Conditions.Add(new Condition(System.Windows.Controls.TreeViewItem.IsSelectedProperty, false));
            dropIntoTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 248, 225))));
            dropIntoTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7))));
            dropIntoTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.ForegroundProperty, System.Windows.Media.Brushes.Black));
            itemContainerStyle.Triggers.Add(dropIntoTrigger);

            var dropBeforeTrigger = new MultiTrigger();
            dropBeforeTrigger.Conditions.Add(new Condition(FrameworkElement.TagProperty, "DropBefore"));
            dropBeforeTrigger.Conditions.Add(new Condition(System.Windows.Controls.TreeViewItem.IsSelectedProperty, false));
            dropBeforeTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7))));
            dropBeforeTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BorderThicknessProperty, new Thickness(1, 2, 1, 1)));
            dropBeforeTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.ForegroundProperty, System.Windows.Media.Brushes.Black));
            itemContainerStyle.Triggers.Add(dropBeforeTrigger);

            var dropAfterTrigger = new MultiTrigger();
            dropAfterTrigger.Conditions.Add(new Condition(FrameworkElement.TagProperty, "DropAfter"));
            dropAfterTrigger.Conditions.Add(new Condition(System.Windows.Controls.TreeViewItem.IsSelectedProperty, false));
            dropAfterTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7))));
            dropAfterTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.BorderThicknessProperty, new Thickness(1, 1, 1, 2)));
            dropAfterTrigger.Setters.Add(new Setter(System.Windows.Controls.TreeViewItem.ForegroundProperty, System.Windows.Media.Brushes.Black));
            itemContainerStyle.Triggers.Add(dropAfterTrigger);

            treeView.ItemContainerStyle = itemContainerStyle;

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
