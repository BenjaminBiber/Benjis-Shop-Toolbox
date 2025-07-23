using System;
using System.Linq;
using ElectronNET.API;
using ElectronNET.API.Entities;

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
        public string? OpenFile(string title, FileFilter[] filter, string initialDir = "")
        {
            if (!HybridSupport.IsElectronActive)
            {
                _notifications.Error($"Fehler beim Öffnen des Dateidialogs: Electron ist nicht aktiv.");
                return null;
            }

            try
            {
                var options = new OpenDialogOptions
                {
                    Title = title,
                    Properties = new[] { OpenDialogProperty.openFile },
                    Filters = filter
                };

                if (!string.IsNullOrWhiteSpace(initialDir))
                {
                    options.DefaultPath = initialDir;
                }

                var mainWindow = Electron.WindowManager.BrowserWindows.First();
                var result = Electron.Dialog
                    .ShowOpenDialogAsync(mainWindow, options)
                    .GetAwaiter()
                    .GetResult();

                return result?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Öffnen des Dateidialogs: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Opens a folder selection dialog and returns the chosen directory.
        /// </summary>
        public string? OpenFolder(string description, string initialDir = "")
        {
            if (!HybridSupport.IsElectronActive)
            {
                _notifications.Error($"Fehler beim Öffnen des Dateidialogs: Electron ist nicht aktiv.");
                return null;
            }

            try
            {
                var options = new OpenDialogOptions
                {
                    Title = description,
                    Properties = new[] { OpenDialogProperty.openDirectory }
                };

                if (!string.IsNullOrWhiteSpace(initialDir))
                {
                    options.DefaultPath = initialDir;
                }
                
                var mainWindow = Electron.WindowManager.BrowserWindows.First();
                var result = Electron.Dialog
                    .ShowOpenDialogAsync(mainWindow, options)
                    .GetAwaiter()
                    .GetResult();

                return result?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Öffnen des Ordnerdialogs: {ex.Message}");
                return null;
            }
        }

        private static FileFilter[]? ParseFilters(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return null;
            }

            var parts = filter.Split('|');
            var list = new System.Collections.Generic.List<FileFilter>();
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                var name = parts[i];
                var extensions = parts[i + 1]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim().TrimStart('*', '.'))
                    .ToArray();

                list.Add(new FileFilter { Name = name, Extensions = extensions });
            }

            return list.ToArray();
        }
    }
}
