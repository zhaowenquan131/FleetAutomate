using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FleetAutomate.Model;
using FleetAutomate.ViewModel;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;
using IDataObject = System.Windows.IDataObject;

namespace FleetAutomate;

public partial class MainWindow
{
    public static readonly DependencyProperty IsActionMultiSelectedProperty = DependencyProperty.RegisterAttached(
        "IsActionMultiSelected", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public static bool GetIsActionMultiSelected(DependencyObject element) => (bool)element.GetValue(IsActionMultiSelectedProperty);
    public static void SetIsActionMultiSelected(DependencyObject element, bool value) => element.SetValue(IsActionMultiSelectedProperty, value);

    private sealed class ActionSelectionState
    {
        public HashSet<IAction> Actions { get; } = [];
        public HashSet<IAction> CollapsedActions { get; } = [];
        public IAction? Anchor { get; set; }
        public IAction? PendingSingleSelection { get; set; }
        public bool Updating { get; set; }
    }

    private sealed record ActionDragSelection(ObservableFlow SourceFlow, IReadOnlyList<IAction> Actions);

    private readonly ConditionalWeakTable<TreeView, ActionSelectionState> _actionSelections = new();
    private DispatcherTimer? _dragScrollTimer;
    private TreeView? _dragScrollTree;
    private IDataObject? _dragScrollData;
    private System.Windows.Point _dragScrollPoint;

    private static IEnumerable<TreeViewItem> EnumerateActionItems(ItemsControl parent, bool visibleOnly = false)
    {
        foreach (var item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem container)
            {
                continue;
            }

            yield return container;
            if (!visibleOnly || container.IsExpanded)
            {
                foreach (var child in EnumerateActionItems(container, visibleOnly))
                {
                    yield return child;
                }
            }
        }
    }

    private void RefreshActionSelection(TreeView tree)
    {
        var selection = _actionSelections.GetOrCreateValue(tree);
        foreach (var item in EnumerateActionItems(tree))
        {
            SetIsActionMultiSelected(item, item.DataContext is IAction action && selection.Actions.Contains(action));
        }
    }

    private void SelectActionRange(TreeView tree, IAction action)
    {
        var selection = _actionSelections.GetOrCreateValue(tree);
        var visible = EnumerateVisibleActions(tree.Items.OfType<IAction>(), selection.CollapsedActions).ToList();
        var start = selection.Anchor == null ? -1 : visible.IndexOf(selection.Anchor);
        var end = visible.IndexOf(action);
        selection.Actions.Clear();
        if (start >= 0 && end >= 0)
        {
            foreach (var item in visible.Skip(Math.Min(start, end)).Take(Math.Abs(end - start) + 1))
            {
                selection.Actions.Add(item);
            }
        }
        else if (action is not ActionBlock)
        {
            selection.Actions.Add(action);
            selection.Anchor = action;
        }
    }

    private static IEnumerable<IAction> EnumerateVisibleActions(IEnumerable<IAction> actions, HashSet<IAction> collapsed)
    {
        foreach (var action in actions)
        {
            if (action is not ActionBlock)
            {
                yield return action;
            }

            if (!collapsed.Contains(action) && action is ICompositeAction composite)
            {
                foreach (var child in EnumerateVisibleActions(composite.GetChildActions(), collapsed))
                {
                    yield return child;
                }
            }
        }
    }

    private void ActionItem_ExpansionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TreeView tree && e.OriginalSource is TreeViewItem { DataContext: IAction action } item)
        {
            var collapsed = _actionSelections.GetOrCreateValue(tree).CollapsedActions;
            if (item.IsExpanded)
            {
                collapsed.Remove(action);
            }
            else
            {
                collapsed.Add(action);
            }
        }
    }

    private void SelectSingleAction(TreeView tree, IAction? action)
    {
        var selection = _actionSelections.GetOrCreateValue(tree);
        selection.Actions.Clear();
        if (action is not null and not ActionBlock)
        {
            selection.Actions.Add(action);
        }

        selection.Anchor = action;
        RefreshActionSelection(tree);
    }

    private void ActionItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && FindAncestor<TreeView>(item) is TreeView tree)
        {
            var selection = _actionSelections.GetOrCreateValue(tree);
            SetIsActionMultiSelected(item, item.DataContext is IAction action && selection.Actions.Contains(action));
        }
    }

    private void TestFlowActionsTreeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _pendingTestFlowDragAction = null;
        if (sender is TreeView tree)
        {
            var selection = _actionSelections.GetOrCreateValue(tree);
            if (selection.PendingSingleSelection is IAction action)
            {
                SelectSingleAction(tree, action);
                selection.PendingSingleSelection = null;
            }
        }
    }

    private bool CanDropActions(TreeView tree, IDataObject data, DropInstruction instruction)
    {
        return data.GetData(TestFlowActionFormat) is ActionDragSelection dragged &&
            ReferenceEquals(tree.DataContext, dragged.SourceFlow) &&
            dragged.Actions.All(action => !ReferenceEquals(action, instruction.ValidationTarget) &&
                !IsDescendantDropTarget(action, instruction.ValidationTarget));
    }

    private void UpdateDragHighlight(TreeView tree, IDataObject data, System.Windows.Point point, DependencyObject? originalSource = null)
    {
        var instruction = ResolveDropInstruction(tree, originalSource, point);
        if (data.GetDataPresent(ToolboxActionTemplateFormat) || CanDropActions(tree, data, instruction))
        {
            SetActiveDropHighlight(instruction.HighlightItem, instruction.Placement);
        }
        else
        {
            ClearActiveDropHighlight();
        }
    }

    private double GetDragScrollDelta(TreeView tree, ScrollViewer scrollViewer, System.Windows.Point point)
    {
        var viewport = (FrameworkElement?)FindVisualChild<ScrollContentPresenter>(scrollViewer) ?? scrollViewer;
        var local = tree.TranslatePoint(point, viewport);
        if (!new Rect(viewport.RenderSize).Contains(local))
        {
            return 0;
        }

        var edge = Math.Min(48, viewport.ActualHeight / 3);
        if (edge <= 0)
        {
            return 0;
        }

        if (local.Y < edge && scrollViewer.VerticalOffset > 0)
        {
            return -4 - 20 * (edge - local.Y) / edge;
        }

        var distanceFromBottom = viewport.ActualHeight - local.Y;
        return distanceFromBottom < edge && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
            ? 4 + 20 * (edge - distanceFromBottom) / edge
            : 0;
    }

    private void UpdateDragAutoScroll(TreeView tree, IDataObject data, System.Windows.Point point)
    {
        _dragScrollTree = tree;
        _dragScrollData = data;
        _dragScrollPoint = point;
        if (FindVisualChild<ScrollViewer>(tree) is not ScrollViewer scrollViewer || GetDragScrollDelta(tree, scrollViewer, point) == 0)
        {
            StopDragAutoScroll();
            return;
        }

        if (_dragScrollTimer == null)
        {
            _dragScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _dragScrollTimer.Tick += DragAutoScroll_Tick;
        }

        _dragScrollTimer.Start();
    }

    private void DragAutoScroll_Tick(object? sender, EventArgs e)
    {
        if (_dragScrollTree is not TreeView tree || !tree.IsLoaded || _dragScrollData == null ||
            FindVisualChild<ScrollViewer>(tree) is not ScrollViewer scrollViewer)
        {
            StopDragAutoScroll();
            return;
        }

        var delta = GetDragScrollDelta(tree, scrollViewer, _dragScrollPoint);
        if (delta == 0)
        {
            StopDragAutoScroll();
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + delta);
        tree.UpdateLayout();
        // Scrolling changes the row under a stationary pointer; resolve the target again.
        UpdateDragHighlight(tree, _dragScrollData, _dragScrollPoint);
    }

    private void StopDragAutoScroll()
    {
        _dragScrollTimer?.Stop();
        _dragScrollTree = null;
        _dragScrollData = null;
    }
}
