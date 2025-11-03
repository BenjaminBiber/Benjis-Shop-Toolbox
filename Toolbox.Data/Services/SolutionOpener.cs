using System.Diagnostics;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Data.Services;

public class SolutionOpener
{
    private readonly INotificationService _notifications;

    public SolutionOpener(INotificationService notificationService)
    {
        _notifications = notificationService;
    }

    public void OpenSolutionInRider(string startDirectory)
    {
        if (!Directory.Exists(startDirectory))
        {
            _notifications.Error($"Ordner existiert nicht: {startDirectory}");
            return;
        }

        var solutionFile = Directory
            .EnumerateFiles(startDirectory, "*.sln", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (solutionFile == null)
        {
            _notifications.Error("Keine .sln-Datei gefunden.");
            return;
        }

        string? riderPath = FindRiderExecutable();
        if (riderPath == null)
        {
            _notifications.Error("Rider konnte nicht gefunden werden.");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = riderPath,
                Arguments = $"\"{solutionFile}\"",
                UseShellExecute = false
            };

            foreach (System.Collections.DictionaryEntry kvp in Environment.GetEnvironmentVariables())
                startInfo.EnvironmentVariables[kvp.Key.ToString()!] = kvp.Value?.ToString() ?? string.Empty;

            startInfo.EnvironmentVariables["JAVA_HOME"] = "";

            Process.Start(startInfo);

            _notifications.Info($"Rider wird mit {solutionFile} gestartet.");
        }
        catch (Exception ex)
        {
            _notifications.Error($"Fehler beim Starten von Rider: {ex.Message}");
        }
    }

    private string? FindRiderExecutable()
    {
        string[] possiblePaths =
        {
            Environment.ExpandEnvironmentVariables(@"C:\\Program Files\\JetBrains"),
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\\JetBrains\\Toolbox\\apps\\Rider"),
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\\Programs\\Rider\\bin")
        };

        foreach (var basePath in possiblePaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            var exe = Directory
                .EnumerateFiles(basePath, "rider64.exe", SearchOption.AllDirectories)
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(exe))
                return exe;
        }

        try
        {
            var which = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "rider64.exe",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(which);
            var output = proc?.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(output) && File.Exists(output))
                return output;
        }
        catch { }

        return null;
    }
}

