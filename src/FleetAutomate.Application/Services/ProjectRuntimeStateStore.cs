using System.IO;
using System.Text.Json;

using FleetAutomate.Model;
using FleetAutomate.Model.Actions.Logic;
using FleetAutomate.Model.Flow;
using FleetAutomate.Model.Project;

namespace FleetAutomate.Services
{
    internal static class ProjectRuntimeStateStore
    {
        private const string StateDirectoryName = ".fleet";
        private const string StateFileName = "runtime-state.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static void Save(string? projectFilePath, TestProject? project)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath) || project?.TestFlows == null)
            {
                return;
            }

            var stateFilePath = GetStateFilePath(projectFilePath);
            var stateDirectory = Path.GetDirectoryName(stateFilePath);
            if (string.IsNullOrWhiteSpace(stateDirectory))
            {
                return;
            }

            Directory.CreateDirectory(stateDirectory);

            var projectDirectory = Path.GetDirectoryName(projectFilePath);
            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                return;
            }

            var snapshot = new ProjectRuntimeStateSnapshot
            {
                Flows =
                [
                    .. project.TestFlows.Select(flow => CaptureFlowState(flow, projectDirectory))
                ]
            };

            File.WriteAllText(stateFilePath, JsonSerializer.Serialize(snapshot, JsonOptions));
        }

        public static void Load(string? projectFilePath, TestProject? project)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath) || project?.TestFlows == null)
            {
                return;
            }

            var stateFilePath = GetStateFilePath(projectFilePath);
            if (!File.Exists(stateFilePath))
            {
                return;
            }

            var projectDirectory = Path.GetDirectoryName(projectFilePath);
            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                return;
            }

            var json = File.ReadAllText(stateFilePath);
            var snapshot = JsonSerializer.Deserialize<ProjectRuntimeStateSnapshot>(json, JsonOptions);
            if (snapshot?.Flows == null)
            {
                return;
            }

            var flowStates = snapshot.Flows
                .Where(flow => !string.IsNullOrWhiteSpace(flow.FlowKey))
                .ToDictionary(flow => flow.FlowKey, StringComparer.OrdinalIgnoreCase);

            foreach (var flow in project.TestFlows)
            {
                var flowKey = GetFlowKey(flow, projectDirectory);
                if (flowStates.TryGetValue(flowKey, out var flowState))
                {
                    ApplyFlowState(flow, flowState);
                }
            }
        }

        private static string GetStateFilePath(string projectFilePath)
        {
            var projectDirectory = Path.GetDirectoryName(projectFilePath) ?? Directory.GetCurrentDirectory();
            return Path.Combine(projectDirectory, StateDirectoryName, StateFileName);
        }

        private static FlowRuntimeStateSnapshot CaptureFlowState(TestFlow flow, string projectDirectory)
        {
            var actions = new List<ActionRuntimeStateSnapshot>();
            string? currentActionPath = null;

            CaptureActionStates(flow.Actions, string.Empty, actions, flow.CurrentAction, ref currentActionPath);

            return new FlowRuntimeStateSnapshot
            {
                FlowKey = GetFlowKey(flow, projectDirectory),
                State = flow.State,
                CurrentActionPath = currentActionPath,
                Actions = actions
            };
        }

        private static void CaptureActionStates(
            IEnumerable<IAction> actions,
            string prefix,
            ICollection<ActionRuntimeStateSnapshot> snapshots,
            IAction? currentAction,
            ref string? currentActionPath)
        {
            var index = 0;
            foreach (var action in actions)
            {
                var actionPath = BuildPath(prefix, index.ToString());
                snapshots.Add(new ActionRuntimeStateSnapshot
                {
                    Path = actionPath,
                    State = action.State
                });

                if (ReferenceEquals(action, currentAction))
                {
                    currentActionPath = actionPath;
                }

                switch (action)
                {
                    case IfAction ifAction:
                        CaptureActionStates(ifAction.IfBlock, BuildPath(actionPath, "if"), snapshots, currentAction, ref currentActionPath);
                        CaptureActionStates(ifAction.ElseBlock, BuildPath(actionPath, "else"), snapshots, currentAction, ref currentActionPath);
                        CaptureActionStates(ifAction.ElseIfs, BuildPath(actionPath, "elseif"), snapshots, currentAction, ref currentActionPath);
                        break;
                    case ICompositeAction compositeAction:
                        CaptureActionStates(compositeAction.GetChildActions(), BuildPath(actionPath, "children"), snapshots, currentAction, ref currentActionPath);
                        break;
                }

                index++;
            }
        }

        private static void ApplyFlowState(TestFlow flow, FlowRuntimeStateSnapshot snapshot)
        {
            var actionStates = snapshot.Actions?
                .Where(action => !string.IsNullOrWhiteSpace(action.Path))
                .ToDictionary(action => action.Path, action => action.State, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, ActionState>(StringComparer.OrdinalIgnoreCase);

            IAction? currentAction = null;
            ApplyActionStates(flow.Actions, string.Empty, actionStates, snapshot.CurrentActionPath, ref currentAction);

            flow.State = snapshot.State;
            flow.CurrentAction = currentAction!;
        }

        private static void ApplyActionStates(
            IEnumerable<IAction> actions,
            string prefix,
            IReadOnlyDictionary<string, ActionState> actionStates,
            string? currentActionPath,
            ref IAction? currentAction)
        {
            var index = 0;
            foreach (var action in actions)
            {
                var actionPath = BuildPath(prefix, index.ToString());
                if (actionStates.TryGetValue(actionPath, out var state))
                {
                    action.State = state;
                }

                if (string.Equals(actionPath, currentActionPath, StringComparison.OrdinalIgnoreCase))
                {
                    currentAction = action;
                }

                switch (action)
                {
                    case IfAction ifAction:
                        ApplyActionStates(ifAction.IfBlock, BuildPath(actionPath, "if"), actionStates, currentActionPath, ref currentAction);
                        ApplyActionStates(ifAction.ElseBlock, BuildPath(actionPath, "else"), actionStates, currentActionPath, ref currentAction);
                        ApplyActionStates(ifAction.ElseIfs, BuildPath(actionPath, "elseif"), actionStates, currentActionPath, ref currentAction);
                        break;
                    case ICompositeAction compositeAction:
                        ApplyActionStates(compositeAction.GetChildActions(), BuildPath(actionPath, "children"), actionStates, currentActionPath, ref currentAction);
                        break;
                }

                index++;
            }
        }

        private static string GetFlowKey(TestFlow flow, string projectDirectory)
        {
            if (!string.IsNullOrWhiteSpace(flow.FileName))
            {
                return Path.GetRelativePath(projectDirectory, flow.FileName);
            }

            return flow.Name ?? string.Empty;
        }

        private static string BuildPath(string prefix, string segment)
        {
            return string.IsNullOrWhiteSpace(prefix) ? segment : $"{prefix}/{segment}";
        }

        private sealed class ProjectRuntimeStateSnapshot
        {
            public List<FlowRuntimeStateSnapshot> Flows { get; set; } = [];
        }

        private sealed class FlowRuntimeStateSnapshot
        {
            public string FlowKey { get; set; } = string.Empty;
            public ActionState State { get; set; } = ActionState.Ready;
            public string? CurrentActionPath { get; set; }
            public List<ActionRuntimeStateSnapshot> Actions { get; set; } = [];
        }

        private sealed class ActionRuntimeStateSnapshot
        {
            public string Path { get; set; } = string.Empty;
            public ActionState State { get; set; } = ActionState.Ready;
        }
    }
}
