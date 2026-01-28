using System.Diagnostics;
using System.Linq;
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

            var currentVersion = NormalizeVersionText(options.GetValueOrDefault("--current-version") ?? TryGetLocalVersion() ?? "0.0.0");
            var pidArg = options.GetValueOrDefault("--pid");
            var processName = options.GetValueOrDefault("--process-name") ?? "Toolbox";
            var interactive = options.ContainsKey("--interactive") || string.Equals(options.GetValueOrDefault("--silent"), "false", StringComparison.OrdinalIgnoreCase);
            var allowBeta = IsTruthy(options.GetValueOrDefault("--allow-beta"));

            if (!Directory.Exists(sourcePath))
            {
                Logger.Write($"Source-Verzeichnis existiert nicht: {sourcePath}");
                Console.Error.WriteLine($"Source-Verzeichnis existiert nicht: {sourcePath}");
                return 2;
            }

            // Suche nach Inno-Installerdateien (flacher Ordner): Installer_Shop-Toolbox_<version>.exe
            var latest = FindLatestInstaller(sourcePath, allowBeta);
            if (latest is null)
            {
                Logger.Write("Kein Installer gefunden (Pattern: Installer_Shop-Toolbox_<version>.exe).");
                Console.Error.WriteLine("Kein Installer gefunden (Pattern: Installer_Shop-Toolbox_<version>.exe).");
                return 3;
            }

            Logger.Write($"Aktuelle Version={currentVersion}, verfügbare Version={latest.Value.version}{(latest.Value.isBeta ? " (beta)" : "")}, Datei={latest.Value.fullPath}");
            Console.WriteLine($"Aktuelle Version:  {currentVersion}");
            Console.WriteLine($"Verfügbare Version: {latest.Value.version}{(latest.Value.isBeta ? " (beta)" : "")} (Datei: {latest.Value.fullPath})");
            if (CompareVersions(latest.Value.version, currentVersion) <= 0)
            {
                Logger.Write("Keine neuere Version gefunden. Abbruch.");
                Console.WriteLine("Keine neuere Version gefunden. Abbruch.");
                WriteStatus(new UpdaterStatus
                {
                    TimestampUtc = DateTime.UtcNow,
                    LocalVersion = currentVersion,
                    AvailableVersion = latest.Value.version,
                    AvailableIsBeta = latest.Value.isBeta,
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
                AvailableIsBeta = latest.Value.isBeta,
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

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim().ToLowerInvariant();
        return v is "1" or "true" or "yes" or "y" or "ja" or "j";
    }

    private static (string version, bool isBeta, string fileName, string fullPath)? FindLatestInstaller(string sourceDir, bool allowBeta)
    {
        var rx = new Regex(
            @"^Installer_Shop\-Toolbox_(\d+(?:[._]\d+){1,3})(?:-([A-Za-z][0-9A-Za-z._-]*))?\.exe$",
            RegexOptions.IgnoreCase
        );
        var candidates = new List<(VersionInfo version, string fileName, string fullPath, DateTime writeTimeUtc)>();

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*.exe", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file) ?? string.Empty;
            var m = rx.Match(name);
            if (!m.Success) continue;
            var versionText = m.Groups[1].Value;
            if (m.Groups[2].Success)
            {
                versionText = $"{versionText}-{m.Groups[2].Value}";
            }
            var ver = ParseVersionInfo(versionText);
            var fi = new FileInfo(file);
            if (!allowBeta && ver.IsBeta)
            {
                continue;
            }
            candidates.Add((ver, name, file, fi.LastWriteTimeUtc));
        }

        if (candidates.Count == 0) return null;

        var best = candidates
            .OrderByDescending(c => c.version, Comparer<VersionInfo>.Create(CompareVersionInfos))
            .ThenByDescending(c => c.writeTimeUtc)
            .First();

        return (best.version.NormalizedText, best.version.IsBeta, best.fileName, best.fullPath);
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
                    return NormalizeVersionText(info.ProductVersion!);
            }
        }
        catch { }
        return null;
    }

    private sealed record VersionInfo(string NumericText, Version Numeric, string? PreRelease)
    {
        public bool IsBeta => PreRelease?.StartsWith("beta", StringComparison.OrdinalIgnoreCase) == true;
        public string NormalizedText => string.IsNullOrWhiteSpace(PreRelease) ? NumericText : $"{NumericText}-{PreRelease}";
    }

    private static string NormalizeVersionText(string v)
    {
        var info = ParseVersionInfo(v);
        return info.NormalizedText;
    }

    private static VersionInfo ParseVersionInfo(string v)
    {
        var s = (v ?? string.Empty).Trim();
        var plusIndex = s.IndexOf('+');
        if (plusIndex >= 0)
        {
            s = s[..plusIndex];
        }
        if (s.StartsWith('v') || s.StartsWith('V'))
        {
            s = s[1..];
        }

        string? pre = null;
        var hyphenIndex = s.IndexOf('-');
        if (hyphenIndex >= 0)
        {
            var after = s[(hyphenIndex + 1)..];
            if (ContainsLetters(after))
            {
                pre = NormalizePreRelease(after);
                s = s[..hyphenIndex];
            }
            else
            {
                s = s.Replace('-', '.');
            }
        }

        s = s.Replace('_', '.').Replace(',', '.');
        while (s.Contains("..")) s = s.Replace("..", ".");
        s = s.Trim('.');

        if (!Version.TryParse(s, out var ver))
        {
            ver = new Version(0, 0, 0, 0);
            s = "0.0.0";
            pre = null;
        }

        return new VersionInfo(s, ver, pre);
    }

    private static bool ContainsLetters(string value)
        => value.Any(c => char.IsLetter(c));

    private static string NormalizePreRelease(string value)
    {
        var p = (value ?? string.Empty).Trim();
        p = p.Replace('_', '.').Replace('-', '.').Trim('.');
        while (p.Contains("..")) p = p.Replace("..", ".");
        p = p.ToLowerInvariant();
        if (p.StartsWith("beta", StringComparison.OrdinalIgnoreCase))
        {
            var rest = p[4..].Trim('.');
            if (rest.Length > 0 && !rest.StartsWith('.'))
            {
                p = "beta." + rest;
            }
            else if (rest.Length == 0)
            {
                p = "beta";
            }
        }
        return p;
    }

    private static int CompareVersions(string a, string b)
    {
        return CompareVersionInfos(ParseVersionInfo(a), ParseVersionInfo(b));
    }

    private static int CompareVersionInfos(VersionInfo a, VersionInfo b)
    {
        var cmp = CompareNumeric(a.Numeric, b.Numeric);
        if (cmp != 0) return cmp;
        return ComparePreRelease(a.PreRelease, b.PreRelease);
    }

    private static int CompareNumeric(Version a, Version b)
    {
        var aa = new[]
        {
            a.Major,
            a.Minor,
            a.Build >= 0 ? a.Build : 0,
            a.Revision >= 0 ? a.Revision : 0
        };
        var bb = new[]
        {
            b.Major,
            b.Minor,
            b.Build >= 0 ? b.Build : 0,
            b.Revision >= 0 ? b.Revision : 0
        };

        for (var i = 0; i < aa.Length; i++)
        {
            var c = aa[i].CompareTo(bb[i]);
            if (c != 0) return c;
        }
        return 0;
    }

    private static int ComparePreRelease(string? a, string? b)
    {
        var hasA = !string.IsNullOrWhiteSpace(a);
        var hasB = !string.IsNullOrWhiteSpace(b);
        if (!hasA && !hasB) return 0;
        if (!hasA) return 1;  // stable > prerelease
        if (!hasB) return -1;

        var ap = SplitPreRelease(a!);
        var bp = SplitPreRelease(b!);
        var len = Math.Max(ap.Length, bp.Length);
        for (var i = 0; i < len; i++)
        {
            if (i >= ap.Length) return -1;
            if (i >= bp.Length) return 1;

            var ai = ap[i];
            var bi = bp[i];
            var aNum = int.TryParse(ai, out var av);
            var bNum = int.TryParse(bi, out var bv);

            if (aNum && bNum)
            {
                var cmp = av.CompareTo(bv);
                if (cmp != 0) return cmp;
            }
            else if (aNum != bNum)
            {
                return aNum ? -1 : 1; // numeric < non-numeric
            }
            else
            {
                var cmp = string.Compare(ai, bi, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
            }
        }
        return 0;
    }

    private static string[] SplitPreRelease(string value)
        => value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
        public bool AvailableIsBeta { get; set; }
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
