using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using FleetAutomate.Model.Flow;
using VariableEnvironment = FleetAutomate.Model.Actions.Logic.Environment;

namespace FleetAutomate.Model;

/// <summary>Explicit variable sources for string parameters, separate from their literal values.</summary>
public static class ActionTextParameters
{
    private sealed class Parameters
    {
        public IReadOnlyDictionary<string, string> Bindings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        public readonly Dictionary<string, object?> RuntimeLiterals = new();
        public bool Applying;
    }

    private static readonly ConditionalWeakTable<IAction, Parameters> Values = new();

    public static IReadOnlyDictionary<string, string> GetBindings(IAction action) => Values.GetOrCreateValue(action).Bindings;

    public static void SetBindings(IAction action, IReadOnlyDictionary<string, string> bindings)
    {
        foreach (var (name, variable) in bindings)
        {
            var property = action.GetType().GetProperty(name);
            if (property?.PropertyType != typeof(string) || !property.CanWrite || string.IsNullOrWhiteSpace(variable))
                throw new ArgumentException($"Invalid text variable binding for '{name}'.", nameof(bindings));
        }
        Values.GetOrCreateValue(action).Bindings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(bindings));
    }

    public static bool IsApplyingRuntimeValues(IAction action) => Values.TryGetValue(action, out var parameters) && parameters.Applying;

    public static object? GetLiteralValue(IAction action, PropertyInfo property)
    {
        return Values.TryGetValue(action, out var parameters) && parameters.RuntimeLiterals.TryGetValue(property.Name, out var literal)
            ? literal : property.GetValue(action);
    }

    public static async Task<bool> ExecuteAsync(IAction action, VariableEnvironment environment, CancellationToken cancellationToken)
    {
        if (!Values.TryGetValue(action, out var parameters) || parameters.Bindings.Count == 0)
            return await action.ExecuteAsync(cancellationToken);

        var resolved = new List<(PropertyInfo Property, string Value)>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (name, variableName) in parameters.Bindings)
            {
                var variable = environment.Variables.FirstOrDefault(v => string.Equals(v.Name, variableName, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException($"Variable '{variableName}' for parameter '{name}' is not available.");
                var property = action.GetType().GetProperty(name)!;
                resolved.Add((property, Convert.ToString(variable.Value, CultureInfo.InvariantCulture) ?? string.Empty));
            }

            // Keep the editable/persisted literals intact even while execution is in progress.
            parameters.Applying = true;
            foreach (var (property, value) in resolved)
            {
                parameters.RuntimeLiterals[property.Name] = property.GetValue(action);
                property.SetValue(action, value);
            }
            parameters.Applying = false;
            return await action.ExecuteAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            action.State = ActionState.Paused;
            return false;
        }
        catch (Exception ex)
        {
            NLog.LogManager.GetCurrentClassLogger().Error(ex, "Cannot resolve action text parameters");
            action.State = ActionState.Failed;
            return false;
        }
        finally
        {
            parameters.Applying = true;
            try
            {
                foreach (var (name, literal) in parameters.RuntimeLiterals)
                    action.GetType().GetProperty(name)!.SetValue(action, literal);
            }
            finally
            {
                parameters.RuntimeLiterals.Clear();
                parameters.Applying = false;
            }
        }
    }
}
