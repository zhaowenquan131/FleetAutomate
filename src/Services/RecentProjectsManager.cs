using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Canvas.TestRunner.Services
{
    /// <summary>
    /// Manages the list of recently opened projects.
    /// </summary>
    public class RecentProjectsManager
    {
        private const int MaxRecentProjects = 10;
        private readonly string _settingsDirectory;
        private readonly string _settingsFilePath;
        private List<RecentProject> _recentProjects;

        public RecentProjectsManager()
        {
            _settingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Canvas.TestRunner");

            _settingsFilePath = Path.Combine(_settingsDirectory, "recent-projects.json");
            _recentProjects = LoadRecentProjects();
        }

        /// <summary>
        /// Gets the list of recent projects, ordered by most recent first.
        /// </summary>
        public IReadOnlyList<RecentProject> RecentProjects => _recentProjects.AsReadOnly();

        /// <summary>
        /// Adds a project to the recent projects list.
        /// </summary>
        public void AddRecentProject(string filePath, string projectName)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            // Remove if already exists
            _recentProjects.RemoveAll(p => p.FilePath == filePath);

            // Add to the beginning (most recent)
            _recentProjects.Insert(0, new RecentProject
            {
                FilePath = filePath,
                ProjectName = projectName ?? Path.GetFileNameWithoutExtension(filePath),
                OpenedAt = DateTime.Now
            });

            // Keep only the most recent N projects
            if (_recentProjects.Count > MaxRecentProjects)
            {
                _recentProjects = _recentProjects.Take(MaxRecentProjects).ToList();
            }

            SaveRecentProjects();
        }

        /// <summary>
        /// Removes a project from the recent projects list.
        /// </summary>
        public void RemoveRecentProject(string filePath)
        {
            _recentProjects.RemoveAll(p => p.FilePath == filePath);
            SaveRecentProjects();
        }

        /// <summary>
        /// Clears all recent projects.
        /// </summary>
        public void ClearAll()
        {
            _recentProjects.Clear();
            SaveRecentProjects();
        }

        /// <summary>
        /// Gets recent projects filtered to only existing files.
        /// </summary>
        public IReadOnlyList<RecentProject> GetValidRecentProjects()
        {
            var validProjects = _recentProjects.Where(p => File.Exists(p.FilePath)).ToList();

            // If any were removed, save the updated list
            if (validProjects.Count != _recentProjects.Count)
            {
                _recentProjects = validProjects;
                SaveRecentProjects();
            }

            return validProjects.AsReadOnly();
        }

        private List<RecentProject> LoadRecentProjects()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                    return new List<RecentProject>();

                var json = File.ReadAllText(_settingsFilePath);
                var projects = JsonSerializer.Deserialize<List<RecentProject>>(json) ?? new List<RecentProject>();
                return projects.OrderByDescending(p => p.OpenedAt).ToList();
            }
            catch
            {
                // If there's an error reading the file, start with empty list
                return new List<RecentProject>();
            }
        }

        private void SaveRecentProjects()
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(_settingsDirectory);

                var json = JsonSerializer.Serialize(_recentProjects, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Silently fail if we can't save
            }
        }
    }

    /// <summary>
    /// Represents a recently opened project.
    /// </summary>
    public class RecentProject
    {
        /// <summary>
        /// Full file path to the project file.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the project.
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// When the project was last opened.
        /// </summary>
        public DateTime OpenedAt { get; set; }
    }
}
