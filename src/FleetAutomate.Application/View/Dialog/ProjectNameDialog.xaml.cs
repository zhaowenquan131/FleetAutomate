using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

using Wpf.Ui.Controls;

using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace FleetAutomate.View.Dialog
{
    public partial class ProjectNameDialog : Window
    {
        public string ProjectName { get; private set; } = string.Empty;
        public string ProjectFolder { get; private set; } = string.Empty;

        public ProjectNameDialog()
        {
            InitializeComponent();
            ProjectNameTextBox.Focus();
            ProjectNameTextBox.TextChanged += ProjectNameTextBox_TextChanged;
            ProjectFolderTextBox.TextChanged += ProjectFolderTextBox_TextChanged;
            
            // Set default project folder to Documents
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var defaultProjectFolder = Path.Combine(documentsPath, "TestRunner Projects");
            ProjectFolderTextBox.Text = defaultProjectFolder;
            
            UpdatePrimaryButtonState();
        }

        private void ProjectNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePrimaryButtonState();
        }

        private void ProjectFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePrimaryButtonState();
        }

        private void UpdatePrimaryButtonState()
        {
            var projectName = ProjectNameTextBox.Text?.Trim();
            var projectFolder = ProjectFolderTextBox.Text?.Trim();
            
            CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(projectName) && 
                                      IsValidProjectName(projectName) &&
                                      !string.IsNullOrWhiteSpace(projectFolder) &&
                                      IsValidFolderPath(projectFolder);
        }

        private static bool IsValidProjectName(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return false;

            // Check for invalid file name characters
            var invalidChars = Path.GetInvalidFileNameChars();
            return !projectName.Any(c => invalidChars.Contains(c));
        }

        private static bool IsValidFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return false;

            try
            {
                // Check for invalid path characters
                var invalidChars = Path.GetInvalidPathChars();
                if (folderPath.Any(c => invalidChars.Contains(c)))
                    return false;

                // Try to get full path to validate format
                Path.GetFullPath(folderPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select project folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            // Set initial directory if current path exists
            var currentPath = ProjectFolderTextBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
            {
                dialog.InitialDirectory = currentPath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ProjectFolderTextBox.Text = dialog.SelectedPath;
            }
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var projectName = ProjectNameTextBox.Text?.Trim();
            var projectFolder = ProjectFolderTextBox.Text?.Trim();
            
            if (string.IsNullOrWhiteSpace(projectName))
            {
                var messageBox = new MessageBox
                {
                    Title = "Invalid Input",
                    Content = "Please enter a project name.",
                    PrimaryButtonText = "OK"
                };
                await messageBox.ShowDialogAsync();
                return;
            }

            if (!IsValidProjectName(projectName))
            {
                var messageBox = new MessageBox
                {
                    Title = "Invalid Project Name",
                    Content = "Project name cannot contain invalid characters: \\ / : * ? \" < > |",
                    PrimaryButtonText = "OK"
                };
                await messageBox.ShowDialogAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(projectFolder))
            {
                var messageBox = new MessageBox
                {
                    Title = "Invalid Input",
                    Content = "Please select a project folder.",
                    PrimaryButtonText = "OK"
                };
                await messageBox.ShowDialogAsync();
                return;
            }

            if (!IsValidFolderPath(projectFolder))
            {
                var messageBox = new MessageBox
                {
                    Title = "Invalid Folder Path",
                    Content = "The selected folder path is not valid.",
                    PrimaryButtonText = "OK"
                };
                await messageBox.ShowDialogAsync();
                return;
            }

            // Create the project folder if it doesn't exist
            try
            {
                if (!Directory.Exists(projectFolder))
                {
                    Directory.CreateDirectory(projectFolder);
                }
            }
            catch (Exception ex)
            {
                var messageBox = new MessageBox
                {
                    Title = "Folder Creation Failed",
                    Content = $"Could not create the project folder:\n{ex.Message}",
                    PrimaryButtonText = "OK"
                };
                await messageBox.ShowDialogAsync();
                return;
            }

            ProjectName = projectName;
            ProjectFolder = projectFolder;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}