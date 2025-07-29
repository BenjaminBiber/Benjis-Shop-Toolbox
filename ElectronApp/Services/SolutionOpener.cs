namespace Benjis_Shop_Toolbox.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

class SolutionOpener
{
    private readonly NotificationService _notifications;

    public SolutionOpener(NotificationService notificationService)
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

        string riderPath = FindRiderExecutable();
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
                UseShellExecute = false // Damit wir Env-Variablen setzen können
            };

            foreach (System.Collections.DictionaryEntry kvp in Environment.GetEnvironmentVariables())
                startInfo.EnvironmentVariables[kvp.Key.ToString()] = kvp.Value.ToString();

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
        // Prüfe typische Installationspfade für Rider unter Windows
        string[] possiblePaths =
        {
            Environment.ExpandEnvironmentVariables(@"C:\Program Files\JetBrains"),
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\JetBrains\Toolbox\apps\Rider")
        };

        foreach (var basePath in possiblePaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            var exe = Directory
                .EnumerateFiles(basePath, "rider64.exe", SearchOption.AllDirectories)
                .OrderByDescending(f => f) // ggf. neueste Version bevorzugen
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(exe))
                return exe;
        }

        // Fallback: versuche PATH
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
