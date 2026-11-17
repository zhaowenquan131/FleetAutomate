using System.Collections;
using System.Globalization;
using System.IO;
using System.Text.Json;
using FleetAutomate.Services;
using YamlDotNet.Serialization;

namespace FleetAutomate.Model.Project;

public sealed record ConfigValueReference(string FileId, string ItemPath)
{
    public string Serialize() => JsonSerializer.Serialize(this);
    public static ConfigValueReference Parse(string text) =>
        JsonSerializer.Deserialize<ConfigValueReference>(text) ?? throw new ArgumentException("Select a configuration item.");
}

public sealed class ConfigValueNode
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public object? Value { get; init; }
    public List<ConfigValueNode> Children { get; init; } = [];
    public string TypeName => Value switch
    {
        null => "null", Dictionary<string, object?> => "object", List<object?> => "array",
        bool => "bool", int => "int", long => "long", double => "double", string => "string", _ => Value.GetType().Name
    };
    public string Preview => Value is Dictionary<string, object?> or List<object?>
        ? $"{Children.Count} items" : Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "null";
    public string DisplayName => $"{Name}  ({TypeName})";
}

public static class ProjectConfigValues
{
    private static readonly AsyncLocal<TestProject?> CurrentProject = new();

    public static IDisposable Enter(TestProject? project)
    {
        var previous = CurrentProject.Value;
        if (project != null) CurrentProject.Value = project;
        return new Scope(previous);
    }

    public static object? ResolveCurrent(string reference) => Resolve(
        CurrentProject.Value ?? throw new InvalidOperationException("This variable needs an open project."),
        ConfigValueReference.Parse(reference));

    public static object? Resolve(TestProject project, ConfigValueReference reference)
    {
        var file = project.Configs.FirstOrDefault(file => file.Id == reference.FileId)
            ?? throw new InvalidOperationException("The configuration file is no longer part of this project.");
        var root = Read(project, file);
        var node = Find(root, reference.ItemPath)
            ?? throw new InvalidOperationException($"Configuration item '{reference.ItemPath}' was not found in '{file.FilePath}'.");
        return node.Value;
    }

    public static ConfigValueNode Read(TestProject project, ProjectFileReference file)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectFilePath)) throw new InvalidOperationException("Save the project before selecting a config.");
        var path = ProjectContentService.ResolveFilePath(project.ProjectFilePath, file.FilePath);
        var text = File.ReadAllText(path);
        JsonElement element;
        if (System.IO.Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(text);
            element = document.RootElement.Clone();
        }
        else
        {
            var yaml = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build().Deserialize<object?>(text);
            element = JsonSerializer.SerializeToElement(NormalizeYaml(yaml));
        }
        return BuildNode("(root)", string.Empty, element);
    }

    public static ConfigValueNode? Find(ConfigValueNode node, string path)
    {
        if (node.Path == path) return node;
        foreach (var child in node.Children)
            if (Find(child, path) is { } match) return match;
        return null;
    }

    private static object? NormalizeYaml(object? value)
    {
        if (value is IDictionary map)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry item in map)
            {
                if (item.Key is not string key) throw new InvalidDataException("Configuration map keys must be text.");
                result.Add(key, NormalizeYaml(item.Value));
            }
            return result;
        }
        if (value is IList list) return list.Cast<object?>().Select(NormalizeYaml).ToList();
        return value;
    }

    private static ConfigValueNode BuildNode(string name, string path, JsonElement element)
    {
        var children = new List<ConfigValueNode>();
        object? value;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                children = element.EnumerateObject().Select(property => BuildNode(property.Name,
                    path + "/" + property.Name.Replace("~", "~0").Replace("/", "~1"), property.Value)).ToList();
                value = children.ToDictionary(child => child.Name, child => child.Value, StringComparer.Ordinal);
                break;
            case JsonValueKind.Array:
                children = element.EnumerateArray().Select((item, index) => BuildNode($"[{index}]", path + "/" + index, item)).ToList();
                value = children.Select(child => child.Value).ToList();
                break;
            case JsonValueKind.Number:
                value = element.TryGetInt32(out var integer) ? (object)integer
                    : element.TryGetInt64(out var number) ? (object)number : element.GetDouble();
                break;
            case JsonValueKind.String: value = element.GetString(); break;
            case JsonValueKind.True: value = true; break;
            case JsonValueKind.False: value = false; break;
            default: value = null; break;
        }
        return new ConfigValueNode { Name = name, Path = path, Value = value, Children = children };
    }

    private sealed class Scope(TestProject? previous) : IDisposable
    {
        public void Dispose() => CurrentProject.Value = previous;
    }
}
