using System;
// using System.Windows.Forms;
// using Microsoft.Win32;
// using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Benjis_Shop_Toolbox.Services
{
    /// <summary>
    /// Provides helper methods to choose files or directories using native dialogs.
    /// </summary>
    public class FileDialogService
    {
        private readonly NotificationService _notifications;

        public FileDialogService(NotificationService notifications)
        {
            _notifications = notifications;
        }

        /// <summary>
        /// Opens a file selection dialog and returns the chosen path.
        /// </summary>
        public string? OpenFile(string title, string filter, string initialDir = "")
        {
            // try
            // {
            //     var dialog = new OpenFileDialog
            //     {
            //         Title = title,
            //         Filter = filter,
            //         InitialDirectory = string.IsNullOrWhiteSpace(initialDir) ? Environment.CurrentDirectory : initialDir
            //     };
            //
            //     return dialog.ShowDialog() == true ? dialog.FileName : null;
            // }
            // catch (Exception ex)
            // {
            //     _notifications.Error($"Fehler beim Öffnen des Dateidialogs: {ex.Message}");
            //     return null;
            // }
            return null;
        }

        /// <summary>
        /// Opens a folder selection dialog and returns the chosen directory.
        /// </summary>
        public string? OpenFolder(string description, string initialDir = "")
        {
            // try
            // {
            //     using var dialog = new FolderBrowserDialog
            //     {
            //         Description = description,
            //         SelectedPath = string.IsNullOrWhiteSpace(initialDir) ? Environment.CurrentDirectory : initialDir
            //     };
            //
            //     return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            // }
            // catch (Exception ex)
            // {
            //     _notifications.Error($"Fehler beim Öffnen des Ordnerdialogs: {ex.Message}");
            //     return null;
            // }
            return null;
        }
    }
}
