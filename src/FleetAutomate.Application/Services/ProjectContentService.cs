using System.IO;
using System.Xml.XPath;
using FleetAutomate.Model.Project;

namespace FleetAutomate.Services;

public static class ProjectContentService
{
    public static string ResolveFilePath(string projectPath, string filePath) =>
        Path.GetFullPath(Path.IsPathRooted(filePath) ? filePath : Path.Combine(ProjectDirectory(projectPath), filePath));

    public static string StoreFilePath(string projectPath, string filePath)
    {
        var absolute = Path.GetFullPath(filePath);
        var relative = Path.GetRelativePath(ProjectDirectory(projectPath), absolute);
        return relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? absolute : relative;
    }

    public static string ProjectDirectory(string projectPath) =>
        Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? throw new ArgumentException("The project needs a file path.");

    public static bool IsConfigFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".json" or ".yaml" or ".yml";

    public static ProjectFileReference AddFile(TestProject project, string projectPath, string filePath, bool config)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("The selected file does not exist.", filePath);
        if (config && !IsConfigFile(filePath)) throw new ArgumentException("Choose a JSON or YAML configuration file.");
        var files = config ? project.Configs : project.Resources;
        var absolute = Path.GetFullPath(filePath);
        var existing = files.FirstOrDefault(file => string.Equals(ResolveFilePath(projectPath, file.FilePath), absolute, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;
        var reference = new ProjectFileReference { FilePath = StoreFilePath(projectPath, absolute) };
        files.Add(reference);
        return reference;
    }

    public static void RebaseFiles(TestProject project, string oldProjectPath, string newProjectPath)
    {
        foreach (var file in project.Configs.Concat(project.Resources))
        {
            file.FilePath = StoreFilePath(newProjectPath, ResolveFilePath(oldProjectPath, file.FilePath));
        }
    }

    public static void SetUiPath(TestProject project, UiPathReference? existing, string name, string xpath)
    {
        name = name.Trim();
        xpath = xpath.Trim();
        if (name.Length == 0 || name.IndexOfAny(['{', '}', '\r', '\n']) >= 0)
            throw new ArgumentException("Enter a reference name without braces or line breaks.");
        if (xpath.Length == 0) throw new ArgumentException("Enter an XPath for the reference.");
        if (project.UiPathReferences.Any(reference => !ReferenceEquals(reference, existing) && reference.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"UI path reference '{name}' already exists.");
        XPathExpression.Compile(xpath);
        if (existing == null)
        {
            project.UiPathReferences.Add(new UiPathReference { Name = name, XPath = xpath });
        }
        else
        {
            existing.Name = name;
            existing.XPath = xpath;
        }
    }
}
