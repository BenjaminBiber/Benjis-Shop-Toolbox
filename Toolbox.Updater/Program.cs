using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Toolbox.Updater;

class Program
{
    private const string DefaultSourcePath = @"K:\\Programme\\Shop-Toolbox";

    public static int Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args);
            var upDebugArg = options.GetValueOrDefault("--TOOLBOX_UPDATER_DEBUG");
            var upDebugEnv = Environment.GetEnvironmentVariable("TOOLBOX_UPDATER_DEBUG");
            if (string.Equals(upDebugEnv, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(upDebugArg, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(upDebugArg, "true", StringComparison.OrdinalIgnoreCase))
            {
                while (!Debugger.IsAttached) Thread.Sleep(100);
            }

            Logger.Write($"DesktopUpdater start. Args={string.Join(' ', args ?? Array.Empty<string>())}");
            var sourcePath = options.GetValueOrDefault("--source")
                             ?? Environment.GetEnvironmentVariable("TOOLBOX_UPDATER_SOURCE")
                             ?? DefaultSourcePath;

            var destDir = options.GetValueOrDefault("--installer-dest")
                         ?? Path.Combine(Path.GetTempPath(), "ShopToolboxUpdater");

            var currentVersion = NormalizeVersion(options.GetValueOrDefault("--current-version") ?? TryGetLocalVersion() ?? "0.0.0");
            var pidArg = options.GetValueOrDefault("--pid");
            var processName = options.GetValueOrDefault("--process-name") ?? "Toolbox";
            var interactive = options.ContainsKey("--interactive") || string.Equals(options.GetValueOrDefault("--silent"), "false", StringComparison.OrdinalIgnoreCase);

            if (!Directory.Exists(sourcePath))
            {
                Logger.Write($"Source-Verzeichnis existiert nicht: {sourcePath}");
                Console.Error.WriteLine($"Source-Verzeichnis existiert nicht: {sourcePath}");
                return 2;
            }

            // Suche nach Inno-Installerdateien (flacher Ordner): Installer_Shop-Toolbox_<version>.exe
            var latest = FindLatestInstaller(sourcePath);
            if (latest is null)
            {
                Logger.Write("Kein Installer gefunden (Pattern: Installer_Shop-Toolbox_<version>.exe).");
                Console.Error.WriteLine("Kein Installer gefunden (Pattern: Installer_Shop-Toolbox_<version>.exe).");
                return 3;
            }

            Logger.Write($"Aktuelle Version={currentVersion}, verfügbare Version={latest.Value.version}, Datei={latest.Value.fullPath}");
            Console.WriteLine($"Aktuelle Version:  {currentVersion}");
            Console.WriteLine($"Verfügbare Version: {latest.Value.version} (Datei: {latest.Value.fullPath})");
            if (CompareVersions(latest.Value.version, currentVersion) <= 0)
            {
                Logger.Write("Keine neuere Version gefunden. Abbruch.");
                Console.WriteLine("Keine neuere Version gefunden. Abbruch.");
                WriteStatus(new UpdaterStatus
                {
                    TimestampUtc = DateTime.UtcNow,
                    LocalVersion = currentVersion,
                    AvailableVersion = latest.Value.version,
                    InstallerPath = latest.Value.fullPath,
                    Action = "noop",
                    Success = true
                });
                return 0;
            }

            Directory.CreateDirectory(destDir);
            var localInstaller = Path.Combine(destDir, Path.GetFileName(latest.Value.fullPath));
            Logger.Write($"Kopiere Installer nach: {localInstaller}");
            Console.WriteLine($"Kopiere Installer nach: {localInstaller}");
            File.Copy(latest.Value.fullPath, localInstaller, overwrite: true);

            // Laufende App stoppen
            if (int.TryParse(pidArg, out var pid))
            {
                TryKillProcessByPid(pid);
            }
            if (!string.IsNullOrWhiteSpace(processName))
            {
                TryKillProcessesByName(processName);
            }

            Thread.Sleep(1500);

            Logger.Write(interactive ? "Starte Installer (interaktiv)..." : "Starte Installer (silent)...");
            Console.WriteLine(interactive ? "Starte Installer (interaktiv)..." : "Starte Installer (silent)...");
            StartInnoInstaller(localInstaller, silent: !interactive);
            WriteStatus(new UpdaterStatus
            {
                TimestampUtc = DateTime.UtcNow,
                LocalVersion = currentVersion,
                AvailableVersion = latest.Value.version,
                InstallerPath = latest.Value.fullPath,
                CopiedInstallerPath = localInstaller,
                Action = "install",
                Success = true
            });
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Write($"Updater Fehler: {ex.Message}\n{ex}");
            Console.Error.WriteLine($"Updater Fehler: {ex.Message}\n{ex}");
            try
            {
                WriteStatus(new UpdaterStatus
                {
                    TimestampUtc = DateTime.UtcNow,
                    LocalVersion = TryGetLocalVersion() ?? "0.0.0",
                    AvailableVersion = null,
                    Action = "error",
                    Success = false,
                    Error = ex.ToString()
                });
            }
            catch { }
            return 1;
        }
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--"))
            {
                var val = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
                dict[a] = val;
            }
        }
        return dict;
    }

    private static (string version, string fileName, string fullPath)? FindLatestInstaller(string sourceDir)
    {
        var rx = new Regex(
            @"^Installer_Shop\-Toolbox_(\d+(?:[._]\d+){1,3})\.exe$",
            RegexOptions.IgnoreCase
        );
        var candidates = new List<(string version, string fileName, string fullPath, DateTime writeTimeUtc)>();

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*.exe", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file) ?? string.Empty;
            var m = rx.Match(name);
            if (!m.Success) continue;
            var ver = NormalizeVersion(m.Groups[1].Value);
            var fi = new FileInfo(file);
            candidates.Add((ver, name, file, fi.LastWriteTimeUtc));
        }

        if (candidates.Count == 0) return null;

        var best = candidates
            .OrderByDescending(c => c.version, Comparer<string>.Create(CompareVersions))
            .ThenByDescending(c => c.writeTimeUtc)
            .First();

        return (best.version, best.fileName, best.fullPath);
    }

    private static string? TryGetLocalVersion()
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
            {
                var info = FileVersionInfo.GetVersionInfo(exe);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                    return NormalizeVersion(info.ProductVersion!);
            }
        }
        catch { }
        return null;
    }

    private static string NormalizeVersion(string v)
    {
        // Accept underscores as separators and normalize to dots
        var s = (v ?? string.Empty).Replace('_', '.');
        var m = Regex.Match(s, @"\d+(?:\.\d+){0,3}");
        return m.Success ? m.Value : s.Trim();
    }

    private static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var pb = b.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var ai = i < pa.Length && int.TryParse(pa[i], out var av) ? av : 0;
            var bi = i < pb.Length && int.TryParse(pb[i], out var bv) ? bv : 0;
            var cmp = ai.CompareTo(bi);
            if (cmp != 0) return cmp;
        }
        return 0;
    }

    private static void TryKillProcessByPid(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            Console.WriteLine($"Beende Prozess PID {pid}...");
            proc.CloseMainWindow();
            if (!proc.WaitForExit(2000))
            {
                proc.Kill(true);
                proc.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hinweis: Prozess {pid} konnte nicht beendet werden: {ex.Message}");
        }
    }

    private static void TryKillProcessesByName(string name)
    {
        foreach (var proc in Process.GetProcessesByName(name))
        {
            try
            {
                Console.WriteLine($"Beende Prozess {name} (PID {proc.Id})...");
                proc.CloseMainWindow();
                if (!proc.WaitForExit(2000))
                {
                    proc.Kill(true);
                    proc.WaitForExit(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hinweis: Prozess {name} (PID {proc.Id}) konnte nicht beendet werden: {ex.Message}");
            }
        }
    }

    private static void StartInnoInstaller(string path, bool silent)
    {
        var psi = new ProcessStartInfo
        {
            FileName = path,
            Arguments = silent ? "/SILENT" : string.Empty,
            UseShellExecute = true,
            Verb = "runas"
        };
        Process.Start(psi);
    }

    // --- Logging & Status ---
    private static class Logger
    {
        private static readonly object _sync = new();
        private static string EnsureLogFile()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shop-Toolbox", "Logs");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "Updater.log");
        }
        public static void Write(string message)
        {
            try
            {
                var file = EnsureLogFile();
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                lock (_sync)
                {
                    File.AppendAllLines(file, new[] { line });
                }
            }
            catch { }
        }
    }

    private record UpdaterStatus
    {
        public DateTime TimestampUtc { get; set; }
        public string? LocalVersion { get; set; }
        public string? AvailableVersion { get; set; }
        public string? InstallerPath { get; set; }
        public string? CopiedInstallerPath { get; set; }
        public string? Action { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    private static void WriteStatus(UpdaterStatus status)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shop-Toolbox", "Updater");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "status.json");
            var json = System.Text.Json.JsonSerializer.Serialize(status, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }
}
