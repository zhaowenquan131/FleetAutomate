using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;

using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace FleetAutomate.ViewModel
{
    public partial class ObservableProject : ObservableObject
    {
        private readonly TestProject _model;
        private bool _refreshingFlows;

        public ObservableProject(TestProject model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            IEnumerable<ObservableFlow> observableFlows = model.TestFlows?.Select(fl => new ObservableFlow(fl));
            TestFlows = new ObservableCollection<ObservableFlow>(observableFlows);
            // Don't call RefreshTestFlows() here - we've already created the ObservableFlows above!
            // Calling it would create duplicate instances with duplicate event handlers.
            Name = _model.Name ?? string.Empty;
            foreach (var flow in TestFlows) flow.PropertyChanged += ObsTestfl_PropertyChanged;
            RefreshProjectTree();
        }
        protected override void OnPropertyChanging(PropertyChangingEventArgs e)
        {
            base.OnPropertyChanging(e);
            if (e.PropertyName == nameof(TestFlows))
            {
                if (TestFlows != null)
                {
                    TestFlows.CollectionChanged -= TestFlows_CollectionChanged;
                }
            }
        }

        private void TestFlows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Handle additions
            if (e.NewItems != null)
            {
                foreach (ObservableFlow newFlow in e.NewItems)
                {
                    if (!_refreshingFlows && newFlow.Model != null && !_model.TestFlows.Contains(newFlow.Model))
                    {
                        // Use internal method for in-memory operations
                        // Actual file operations should be done by TestProjectManager
                        _model.AddTestFlowToCollection(newFlow.Model);
                    }
                    newFlow.PropertyChanged += ObsTestfl_PropertyChanged;
                }
            }
            // Handle removals
            if (e.OldItems != null)
            {
                foreach (ObservableFlow oldFlow in e.OldItems)
                {
                    if (!_refreshingFlows && oldFlow.Model != null && _model.TestFlows.Contains(oldFlow.Model))
                    {
                        // Use RemoveTestFlow without deleteFile parameter (defaults to false)
                        _model.RemoveTestFlow(oldFlow.Model, deleteFile: false);
                    }
                    oldFlow.PropertyChanged -= ObsTestfl_PropertyChanged;
                }
            }
            RefreshProjectTree();
        }
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(TestFlows))
            {
                if (TestFlows != null)
                {
                    TestFlows.CollectionChanged += TestFlows_CollectionChanged;
                }
            }
        }

        private void ObsTestfl_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObservableFlow.Name)) RefreshProjectTree();
        }

        public ObservableCollection<ProjectTreeNode> ProjectNodes { get; } = [];

        public void RefreshProjectTree()
        {
            var previousRoot = ProjectNodes.FirstOrDefault();
            var expanded = previousRoot?.Children.ToDictionary(node => node.Kind, node => node.IsExpanded);
            var root = new ProjectTreeNode(ProjectNodeKind.Project, Name ?? _model.Name);
            var flows = new ProjectTreeNode(ProjectNodeKind.TestFlows, "test flows");
            foreach (var flow in TestFlows) flows.Children.Add(new ProjectTreeNode(ProjectNodeKind.TestFlow, flow.Name, flow, flow.FileName));
            var configs = new ProjectTreeNode(ProjectNodeKind.Configs, "configs");
            foreach (var file in _model.Configs) configs.Children.Add(new ProjectTreeNode(ProjectNodeKind.Config, Path.GetFileName(file.FilePath), file, file.FilePath));
            var paths = new ProjectTreeNode(ProjectNodeKind.UiPaths, "ui path references");
            foreach (var reference in _model.UiPathReferences) paths.Children.Add(new ProjectTreeNode(ProjectNodeKind.UiPath, reference.Name, reference, reference.XPath));
            var resources = new ProjectTreeNode(ProjectNodeKind.Resources, "resources");
            foreach (var file in _model.Resources) resources.Children.Add(new ProjectTreeNode(ProjectNodeKind.Resource, Path.GetFileName(file.FilePath), file, file.FilePath));
            root.Children.Add(flows);
            root.Children.Add(configs);
            root.Children.Add(paths);
            root.Children.Add(resources);
            if (previousRoot != null) root.IsExpanded = previousRoot.IsExpanded;
            foreach (var group in root.Children)
                if (expanded?.TryGetValue(group.Kind, out var isExpanded) == true) group.IsExpanded = isExpanded;
            ProjectNodes.Clear();
            ProjectNodes.Add(root);
        }

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private ObservableCollection<ObservableFlow> _testFlows;

        /// <summary>
        /// Gets the underlying model for serialization and business logic.
        /// </summary>
        public TestProject Model => _model;

        /// <summary>
        /// Refreshes the TestFlows collection from the model.
        /// </summary>
        public void RefreshTestFlows()
        {
            var models = _model.TestFlows?.ToList() ?? [];
            var existing = TestFlows.ToDictionary(flow => flow.Model);
            _refreshingFlows = true;
            try
            {
                foreach (var flow in TestFlows) flow.PropertyChanged -= ObsTestfl_PropertyChanged;
                TestFlows.Clear();
                foreach (var testFlow in models)
                {
                    TestFlows.Add(existing.TryGetValue(testFlow, out var flow) ? flow : new ObservableFlow(testFlow));
                }
            }
            finally { _refreshingFlows = false; }
            Name = _model.Name ?? string.Empty;
            RefreshProjectTree();
        }

        /// <summary>
        /// Finds a TestFlow by name.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <returns>The ObservableFlow if found, null otherwise.</returns>
        public ObservableFlow? FindTestFlowByName(string name)
        {
            return TestFlows.FirstOrDefault(tf => tf.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Finds a TestFlow by file name.
        /// </summary>
        /// <param name="fileName">The file name to search for.</param>
        /// <returns>The ObservableFlow if found, null otherwise.</returns>
        public ObservableFlow? FindTestFlowByFileName(string fileName)
        {
            return TestFlows.FirstOrDefault(tf =>
                !string.IsNullOrEmpty(tf.FileName) &&
                Path.GetFileName(tf.FileName).Equals(Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets all enabled TestFlows in the project.
        /// </summary>
        /// <returns>Collection of enabled ObservableFlows.</returns>
        public IEnumerable<ObservableFlow> GetEnabledTestFlows()
        {
            return TestFlows.Where(tf => tf.IsEnabled);
        }

        /// <summary>
        /// Gets all disabled TestFlows in the project.
        /// </summary>
        /// <returns>Collection of disabled ObservableFlows.</returns>
        public IEnumerable<ObservableFlow> GetDisabledTestFlows()
        {
            return TestFlows.Where(tf => !tf.IsEnabled);
        }

        /// <summary>
        /// Gets TestFlows that have missing file references.
        /// </summary>
        /// <returns>Collection of ObservableFlows with missing files.</returns>
        public IEnumerable<ObservableFlow> GetTestFlowsWithMissingFiles()
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
            _model.ClearTestFlows();
            TestFlows.Clear();
        }

        /// <summary>
        /// Loads all TestFlow objects from their file paths.
        /// DEPRECATED: Use TestProjectManager to load TestFlows with proper project directory.
        /// </summary>
        [Obsolete("Use TestProjectManager.LoadProject instead")]
        public void LoadTestFlowsFromFiles()
        {
            throw new InvalidOperationException("LoadTestFlowsFromFiles requires project directory. Use TestProjectManager instead.");
        }

        /// <summary>
        /// Saves all TestFlow objects to their respective files.
        /// DEPRECATED: Use TestProjectManager to save TestFlows with proper project directory.
        /// </summary>
        [Obsolete("Use TestProjectManager.SaveProject instead")]
        public void SaveTestFlowsToFiles()
        {
            throw new InvalidOperationException("SaveTestFlowsToFiles requires project directory. Use TestProjectManager instead.");
        }
    }
}
