using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Toolbox.Versioning;

class Program
{
    private static string isccPath = @"C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe";
    private static string scriptPath = Path.Combine(AppContext.BaseDirectory, "toolbox.iss");
    private const int KeepInstallerCount = 5;

    static void Main(string[] args)
    {
        WaitForDebuggerIfRequested("Versioning");
        var appVersion = GetVersion(args);
        if (appVersion is null)
        {
            Console.Error.WriteLine("Abbruch: Keine gültige Version angegeben.");
            Environment.Exit(1);
            return;
        }

        // Vorherige Version ermitteln, um nur bei Änderung zu committen
        var hadPrev = TryGetCurrentCsprojVersion(out var prevVersion);
        var versionChanged = false;

        // 1) Version auch in Toolbox.csproj setzen
        if (TryUpdateToolboxCsprojVersion(appVersion, out var csprojPath, out var csprojError))
        {
            Console.WriteLine($"csproj aktualisiert: {csprojPath} -> Version {appVersion}");
            versionChanged = !hadPrev || !string.Equals(prevVersion, appVersion, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine($"Hinweis: csproj konnte nicht aktualisiert werden: {csprojError}");
            Console.ResetColor();
        }

        // 2) Toolbox publishen (Release) in ein temporäres Verzeichnis und Pfad an Inno weiterreichen
        var publishDir = PublishToolbox(appVersion);
        if (publishDir is null)
        {
            Console.Error.WriteLine("Abbruch: Publish fehlgeschlagen.");
            Environment.Exit(2);
            return;
        }

        // 3) Inno Setup ausführen und Version + PublishDir an das Script übergeben
        var psi = new ProcessStartInfo
        {
            FileName = isccPath,
            Arguments = $"\"{scriptPath}\" /DMyAppVersion={appVersion} /DMyPublishDir=\"{publishDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
        process.ErrorDataReceived  += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        Console.WriteLine($"Exit-Code: {process.ExitCode}");

        // Commit + Push erst NACH erfolgreichem Installer-Build
        if (process.ExitCode == 0)
        {
            TryCleanupOldInstallers();
            var repoRoot = FindRepoRoot();
            if (repoRoot is null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Hinweis: Kein Git-Repository gefunden (.git nicht vorhanden). Commit/Push wird übersprungen.");
                Console.ResetColor();
            }
            else
            {
                TryGitCommitAndPush(repoRoot, appVersion);
            }
        }
    }

    private static void TryCleanupOldInstallers()
    {
        try
        {
            var outputDir = TryGetOutputDirFromInno(scriptPath);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Hinweis: OutputDir aus toolbox.iss nicht gefunden. Cleanup uebersprungen.");
                Console.ResetColor();
                return;
            }

            outputDir = Environment.ExpandEnvironmentVariables(outputDir);
            if (!Directory.Exists(outputDir))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Hinweis: OutputDir nicht gefunden: {outputDir}. Cleanup uebersprungen.");
                Console.ResetColor();
                return;
            }

            var candidates = GetInstallerCandidates(outputDir);
            if (candidates.Count <= KeepInstallerCount)
            {
                return;
            }

            var ordered = candidates
                .OrderByDescending(c => c.Version, Comparer<string>.Create(CompareVersions))
                .ThenByDescending(c => c.WriteTimeUtc)
                .ToList();

            var keep = new HashSet<string>(
                ordered.Take(KeepInstallerCount).Select(c => c.FullPath),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var c in ordered.Skip(KeepInstallerCount))
            {
                if (keep.Contains(c.FullPath))
                {
                    continue;
                }

                try
                {
                    File.Delete(c.FullPath);
                    Console.WriteLine($"Installer entfernt: {Path.GetFileName(c.FullPath)}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Hinweis: Konnte Installer nicht loeschen: {c.FullPath} ({ex.Message})");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Hinweis: Cleanup uebersprungen: {ex.Message}");
            Console.ResetColor();
        }
    }

    private sealed class InstallerCandidate
    {
        public InstallerCandidate(string version, string fullPath, DateTime writeTimeUtc)
        {
            Version = version;
            FullPath = fullPath;
            WriteTimeUtc = writeTimeUtc;
        }

        public string Version { get; }
        public string FullPath { get; }
        public DateTime WriteTimeUtc { get; }
    }

    private static List<InstallerCandidate> GetInstallerCandidates(string outputDir)
    {
        var rx = new System.Text.RegularExpressions.Regex(
            @"^Installer_Shop\-Toolbox_(\d+(?:[._]\d+){1,3})(?:-([A-Za-z][0-9A-Za-z._-]*))?\.exe$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        var list = new List<InstallerCandidate>();
        foreach (var file in Directory.EnumerateFiles(outputDir, "*.exe", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file) ?? string.Empty;
            var m = rx.Match(name);
            if (!m.Success)
            {
                continue;
            }

            var versionText = m.Groups[1].Value;
            if (m.Groups[2].Success)
            {
                versionText = $"{versionText}-{m.Groups[2].Value}";
            }
            var parsed = ParseVersionOrDefault(versionText);
            var version = parsed.FullText;
            var fi = new FileInfo(file);
            list.Add(new InstallerCandidate(version, file, fi.LastWriteTimeUtc));
        }

        return list;
    }

    private static string? TryGetOutputDirFromInno(string issPath)
    {
        if (!File.Exists(issPath))
        {
            return null;
        }

        foreach (var raw in File.ReadLines(issPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";"))
            {
                continue;
            }

            if (!line.StartsWith("OutputDir", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var idx = line.IndexOf('=');
            if (idx < 0)
            {
                continue;
            }

            var value = line[(idx + 1)..].Trim();
            if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
            {
                value = value[1..^1];
            }

            return value;
        }

        return null;
    }

    private static string? FindRepoRoot()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var d = dir; d != null; d = d.Parent)
            {
                var gitDir = Path.Combine(d.FullName, ".git");
                if (Directory.Exists(gitDir))
                    return d.FullName;
            }
        }
        catch { }
        return null;
    }

    private static void TryGitCommitAndPush(string repoRoot, string version)
    {
        try
        {
            // Erst prüfen, ob überhaupt Änderungen vorhanden sind
            var status = RunProcess("git", "status --porcelain", repoRoot, out var so, out var se);
            if (status != 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Git-Status fehlgeschlagen ({status}). Überspringe Commit/Push.\n{se}");
                Console.ResetColor();
                return;
            }
            if (string.IsNullOrWhiteSpace(so))
            {
                Console.WriteLine("Keine Änderungen zum Commit. Überspringe Commit/Push.");
                return;
            }

            // Add alle Änderungen
            var addCode = RunProcess("git", "add -A", repoRoot, out so, out se);
            if (addCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Git add fehlgeschlagen ({addCode}).\n{se}");
                Console.ResetColor();
                return;
            }

            // Commit
            var msg = $"Bump to Version {version}";
            var commitCode = RunProcess("git", $"commit -m \"{msg}\"", repoRoot, out so, out se);
            if (commitCode != 0)
            {
                // Häufig: nothing to commit
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Git commit nicht ausgeführt ({commitCode}).\n{so}{se}");
                Console.ResetColor();
                return;
            }

            Console.WriteLine("Git commit erstellt. Pushe Änderungen…");
            TryPushAllRemotes(repoRoot);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Git Commit/Push übersprungen: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void TryPushAllRemotes(string repoRoot)
    {
        var remotesCode = RunProcess("git", "remote", repoRoot, out var remotesOut, out var remotesErr);
        if (remotesCode != 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Git remotes fehlgeschlagen ({remotesCode}).\n{remotesErr}");
            Console.ResetColor();
            return;
        }

        var remotes = remotesOut
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        if (remotes.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Keine Git Remotes gefunden. Push wird uebersprungen.");
            Console.ResetColor();
            return;
        }

        foreach (var remote in remotes)
        {
            var pushCode = RunProcess("git", $"push \"{remote}\" --all", repoRoot, out var so, out var se);
            if (pushCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Git push zu '{remote}' fehlgeschlagen ({pushCode}).\n{se}");
                Console.ResetColor();
                continue;
            }

            Console.WriteLine($"Git push zu '{remote}' erfolgreich.");
        }
    }

    private static int RunProcess(string fileName, string arguments, string workingDir, out string stdout, out string stderr)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi };
        p.Start();
        stdout = p.StandardOutput.ReadToEnd();
        stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode;
    }

    private static string? PublishToolbox(string appVersion)
    {
        try
        {
            var csproj = FindCsprojPath();
            if (csproj is null)
            {
                Console.Error.WriteLine("Toolbox.csproj nicht gefunden.");
                return null;
            }

            var outDir = Path.Combine(Path.GetTempPath(), $"ToolboxPublish_{appVersion}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(outDir);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{csproj}\" -c Release -r win-x64 -p:SelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=false -o \"{outDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return null;
            var stdOut = p.StandardOutput.ReadToEnd();
            var stdErr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Console.Error.WriteLine("dotnet publish fehlgeschlagen:");
                Console.Error.WriteLine(stdOut);
                Console.Error.WriteLine(stdErr);
                return null;
            }
            return outDir;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fehler beim Publish: {ex.Message}");
            return null;
        }
    }

    private static void WaitForDebuggerIfRequested(string appName)
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("TOOLBOX_WAIT_FOR_DEBUGGER");
            var requested = env == "1" || env?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            if (requested && !Debugger.IsAttached)
            {
                var pid = Environment.ProcessId;
                Console.WriteLine($"[{appName}] Warte auf Debugger… PID={pid}");
                var last = DateTime.UtcNow;
                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(250);
                    if ((DateTime.UtcNow - last) > TimeSpan.FromSeconds(5))
                    {
                        Console.WriteLine($"[{appName}] …warte weiterhin auf Debugger…");
                        last = DateTime.UtcNow;
                    }
                }
                Console.WriteLine($"[{appName}] Debugger angehängt.");
            }
        }
        catch
        {
            // ignore
        }
    }

    private static bool TryUpdateToolboxCsprojVersion(string versionText, out string csprojPath, out string error)
    {
        csprojPath = string.Empty;
        error = string.Empty;

        try
        {
            var path = FindCsprojPath();
            if (path is null)
            {
                error = "Toolbox.csproj nicht gefunden (Suche ab AppContext.BaseDirectory).";
                return false;
            }

            if (!TryParseVersionInput(versionText, out var parsed, out var parseError))
            {
                error = $"Ungültige Version: {versionText}. {parseError}";
                return false;
            }

            var v = parsed.Numeric;
            var fourPart = $"{v.Major}.{v.Minor}.{(v.Build >= 0 ? v.Build : 0)}.{(v.Revision >= 0 ? v.Revision : 0)}";

            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            if (doc.Root is null)
            {
                error = "Ungültige csproj-Struktur (Root fehlt).";
                return false;
            }

            var ns = doc.Root.Name.Namespace;
            var propGroups = doc.Root.Elements(ns + "PropertyGroup").ToList();
            var targetGroup = propGroups.FirstOrDefault(pg => pg.Attribute("Condition") == null)
                              ?? propGroups.FirstOrDefault();
            if (targetGroup == null)
            {
                targetGroup = new XElement(ns + "PropertyGroup");
                doc.Root.Add(targetGroup);
            }

            void SetOrAdd(string name, string value)
            {
                var existing = doc.Root!
                    .Elements(ns + "PropertyGroup")
                    .Select(pg => pg.Element(ns + name))
                    .FirstOrDefault(e => e != null);
                if (existing != null)
                {
                    existing.Value = value;
                }
                else
                {
                    targetGroup.Add(new XElement(ns + name, value));
                }
            }

            SetOrAdd("Version", parsed.FullText);       // NuGet/Produktversion inkl. Beta-Tag
            SetOrAdd("AssemblyVersion", fourPart);   // AssemblyVersion (4-teilig)
            SetOrAdd("FileVersion", fourPart);       // Dateiversion (4-teilig)
            SetOrAdd("InformationalVersion", parsed.FullText); // Anzeigen/Produktversion

            doc.Save(path);
            csprojPath = path;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? FindCsprojPath()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var d = dir; d != null; d = d.Parent)
            {
                var byFolder = Path.Combine(d.FullName, "Toolbox", "Toolbox.csproj");
                if (File.Exists(byFolder)) return byFolder;

                var directMatch = Directory.GetFiles(d.FullName, "Toolbox.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (directMatch != null) return directMatch;
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private static string? GetVersion(string[] args)
    {
        // 1) Falls als Argument uebergeben, zuerst pruefen
        if (args is { Length: > 0 })
        {
            if (TryParseVersionInput(args[0], out var parsed, out var parseError))
            {
                Console.WriteLine($"Verwende Version aus Argument: {parsed.FullText}");
                return parsed.FullText;
            }
            Console.Error.WriteLine($"Ungueltige Version im Argument: '{args[0]}' ({parseError})");
        }

        var currentFromCsproj = TryGetCurrentCsprojVersion(out var currentVer) ? currentVer : null;

        while (true)
        {
            Console.Write("Bitte neue Versionsnummer eingeben (z.B. 1.2.3 oder 1.2.3-beta). ");
            if (!string.IsNullOrWhiteSpace(currentFromCsproj))
                Console.Write($"Aktuell: {currentFromCsproj}. ");
            Console.Write("Eingabe 'q' zum Abbruch: ");
            var input = Console.ReadLine();

            if (input is null)
            {
                return null;
            }

            input = input.Trim();
            if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!TryParseVersionInput(input, out var parsed, out var parseError))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine($"Ungueltiges Format. {parseError}");
                Console.Error.WriteLine("Beispiele: 1.2.3, 1.2.3.4, 1.2.3-beta");
                Console.Error.WriteLine("Trennzeichen: '.', '_' oder ',' (z.B. 1_2_3).");
                Console.ResetColor();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(parsed.PreRelease))
            {
                if (!ConfirmUseBeta(parsed.PreRelease))
                {
                    continue;
                }
                return parsed.FullText;
            }

            if (ConfirmAddBeta())
            {
                parsed = new ParsedVersion(parsed.NumericText, parsed.Numeric, "beta");
            }
            return parsed.FullText;
        }
    }

    private sealed record ParsedVersion(string NumericText, Version Numeric, string? PreRelease)
    {
        public string FullText => string.IsNullOrWhiteSpace(PreRelease) ? NumericText : $"{NumericText}-{PreRelease}";
        public bool IsBeta => PreRelease?.StartsWith("beta", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool TryParseVersionInput(string value, out ParsedVersion parsed, out string error)
    {
        parsed = new ParsedVersion("0.0.0", new Version(0, 0, 0, 0), null);
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Leere Eingabe.";
            return false;
        }

        var s = value.Trim();
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

        s = s.Replace(',', '.').Replace('_', '.');
        while (s.Contains("..")) s = s.Replace("..", ".");
        s = s.Trim('.');

        if (!IsValidNumericVersion(s))
        {
            error = "Erlaubt sind 3 oder 4 numerische Segmente.";
            return false;
        }

        if (!Version.TryParse(s, out var v))
        {
            error = "Version konnte nicht geparst werden.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(pre) && !IsSupportedPreRelease(pre))
        {
            error = "Nur das PreRelease 'beta' ist erlaubt.";
            return false;
        }

        parsed = new ParsedVersion(s, v, string.IsNullOrWhiteSpace(pre) ? null : pre);
        return true;
    }

    private static ParsedVersion ParseVersionOrDefault(string value)
    {
        return TryParseVersionInput(value, out var parsed, out _) ? parsed : new ParsedVersion("0.0.0", new Version(0, 0, 0, 0), null);
    }

    private static bool IsValidNumericVersion(string value)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"^\d+(?:\.\d+){2,3}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return regex.IsMatch(value);
    }

    private static bool IsSupportedPreRelease(string value)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"^beta(?:[._-]?\d+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return regex.IsMatch(value);
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
            if (rest.Length > 0)
            {
                p = "beta." + rest;
            }
            else
            {
                p = "beta";
            }
        }
        return p;
    }

    private static int CompareVersions(string a, string b)
    {
        return CompareVersionInfos(ParseVersionOrDefault(a), ParseVersionOrDefault(b));
    }

    private static int CompareVersionInfos(ParsedVersion a, ParsedVersion b)
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

    private static bool ConfirmAddBeta()
    {
        Console.Write("Beta-Tag anhaengen? (j/N): ");
        var input = Console.ReadLine();
        return IsAffirmative(input);
    }

    private static bool ConfirmUseBeta(string preRelease)
    {
        if (!IsSupportedPreRelease(preRelease))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Hinweis: PreRelease '{preRelease}' ist nicht erlaubt.");
            Console.ResetColor();
            return false;
        }
        Console.Write("Beta-Version erkannt. Fortfahren? (j/N): ");
        var input = Console.ReadLine();
        return IsAffirmative(input);
    }

    private static bool IsAffirmative(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v is "j" or "ja" or "y" or "yes";
    }

    private static bool TryGetCurrentCsprojVersion(out string version)
    {
        version = string.Empty;
        try
        {
            var path = FindCsprojPath();
            if (path is null) return false;
            var doc = System.Xml.Linq.XDocument.Load(path);
            var ns = doc.Root?.Name.Namespace ?? System.Xml.Linq.XNamespace.None;
            var versionElem = doc.Root?
                .Elements(ns + "PropertyGroup")
                .Select(pg => pg.Element(ns + "Version"))
                .FirstOrDefault(e => e != null);
            if (versionElem != null && !string.IsNullOrWhiteSpace(versionElem.Value))
            {
                if (TryParseVersionInput(versionElem.Value, out var parsed, out _))
                {
                    version = parsed.FullText;
                    return true;
                }
            }
        }
        catch { }
        return false;
    }
}

