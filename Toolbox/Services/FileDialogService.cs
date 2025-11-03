using System.Windows.Forms;

namespace Toolbox.Services
{
    public class FileFilter
    {
        public string Name { get; set; } = string.Empty;
        public string[] Extensions { get; set; } = Array.Empty<string>();
    }

    public class FileDialogService
    {
        private readonly NotificationService _notifications;

        public FileDialogService(NotificationService notifications)
        {
            _notifications = notifications;
        }

        public string? OpenFile(string title, FileFilter[]? filter, string initialDir = "")
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = title,
                    InitialDirectory = string.IsNullOrWhiteSpace(initialDir) ? null : initialDir,
                    Filter = BuildFilterString(filter)
                };

                return dialog.ShowDialog() == true ? dialog.FileName : null;
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Öffnen des Dateidialogs: {ex.Message}");
                return null;
            }
        }

        public string? OpenFolder(string description, string initialDir = "")
        {
            try
            {
                using var dlg = new FolderBrowserDialog
                {
                    Description = description,
                    SelectedPath = string.IsNullOrWhiteSpace(initialDir) ? string.Empty : initialDir,
                    ShowNewFolderButton = true
                };
                var result = dlg.ShowDialog();
                if (result == DialogResult.OK || result == DialogResult.Yes)
                {
                    return dlg.SelectedPath;
                }
                return null;
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Öffnen des Ordnerdialogs: {ex.Message}");
                return null;
            }
        }

        private static string BuildFilterString(FileFilter[]? filters)
        {
            if (filters == null || filters.Length == 0)
            {
                return "Alle Dateien (*.*)|*.*";
            }
            var parts = new List<string>();
            foreach (var f in filters)
            {
                var exts = string.Join(";", (f.Extensions ?? Array.Empty<string>()).Select(e => $"*.{e.TrimStart('.', '*')}"));
                parts.Add($"{f.Name}|{exts}");
            }
            return string.Join("|", parts);
        }
    }
}
