using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
            WriteError("Abbruch: Keine gueltige Version angegeben.");
            Environment.Exit(1);
            return;
        }

        WriteSection("Versioning");
        // Vorherige Version ermitteln, um nur bei Änderung zu committen
        var hadPrev = TryGetCurrentCsprojVersion(out var prevVersion);
        var versionChanged = false;

        // 1) Version auch in Toolbox.csproj setzen
        if (TryUpdateToolboxCsprojVersion(appVersion, out var csprojPath, out var csprojError))
        {
            WriteSuccess($"csproj aktualisiert: {csprojPath} -> Version {appVersion}");
            versionChanged = !hadPrev || !string.Equals(prevVersion, appVersion, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            WriteWarning($"Hinweis: csproj konnte nicht aktualisiert werden: {csprojError}");
        }

        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            WriteWarning("Hinweis: Kein Git-Repository gefunden (.git nicht vorhanden). Changelog-Erstellung wird uebersprungen.");
        }
        else
        {
            TryEnsureChangelogFile(repoRoot, appVersion, hadPrev ? prevVersion : null);
            TryUpdateChangelogIndex(repoRoot);
            if (!ConfirmChangelogReady(repoRoot, appVersion))
            {
                WriteWarning("Abbruch: Changelog nicht bestaetigt.");
                Environment.Exit(3);
                return;
            }
        }

        // 2) Toolbox publishen (Release) in ein temporäres Verzeichnis und Pfad an Inno weiterreichen
        WriteSection("Publish");
        var publishDir = PublishToolbox(appVersion);
        if (publishDir is null)
        {
            WriteError("Abbruch: Publish fehlgeschlagen.");
            Environment.Exit(2);
            return;
        }

        // 3) Inno Setup ausführen und Version + PublishDir an das Script übergeben
        WriteSection("Installer");
        var psi = new ProcessStartInfo
        {
            FileName = isccPath,
            Arguments = $"\"{scriptPath}\" /DMyAppVersion={appVersion} /DMyPublishDir=\"{publishDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var exitCode = RunProcessWithSpinner(psi, "Inno Setup laeuft");
        WriteInfo($"Exit-Code: {exitCode}");

        // Commit + Push erst NACH erfolgreichem Installer-Build
        if (exitCode == 0)
        {
            TryCleanupOldInstallers();
            if (repoRoot is null)
            {
                WriteWarning("Hinweis: Kein Git-Repository gefunden (.git nicht vorhanden). Commit/Push wird uebersprungen.");
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
                WriteWarning("Hinweis: OutputDir aus toolbox.iss nicht gefunden. Cleanup uebersprungen.");
                return;
            }

            outputDir = Environment.ExpandEnvironmentVariables(outputDir);
            if (!Directory.Exists(outputDir))
            {
                WriteWarning($"Hinweis: OutputDir nicht gefunden: {outputDir}. Cleanup uebersprungen.");
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
                WriteInfo($"Installer entfernt: {Path.GetFileName(c.FullPath)}");
                }
                catch (Exception ex)
                {
                    WriteWarning($"Hinweis: Konnte Installer nicht loeschen: {c.FullPath} ({ex.Message})");
                }
            }
        }
        catch (Exception ex)
        {
            WriteWarning($"Hinweis: Cleanup uebersprungen: {ex.Message}");
        }
    }

    private static void TryEnsureChangelogFile(string repoRoot, string appVersion, string? prevVersion)
    {
        try
        {
            var normalizedVersion = NormalizeVersionText(appVersion);
            var changelogDir = Path.Combine(repoRoot, "Toolbox", "wwwroot", "Changelog");
            Directory.CreateDirectory(changelogDir);
            var changelogPath = Path.Combine(changelogDir, $"{normalizedVersion}.md");

            if (File.Exists(changelogPath))
            {
                WriteInfo($"Changelog existiert bereits: {changelogPath}");
                return;
            }

            var seedContent = TryLoadPreviousChangelog(changelogDir, prevVersion);
            var content = string.IsNullOrWhiteSpace(seedContent)
                ? BuildChangelogContent(appVersion, prevVersion, BuildChangelogContext(repoRoot, prevVersion))
                : AdaptChangelogVersion(seedContent, normalizedVersion);

            File.WriteAllText(changelogPath, content);
            WriteSuccess($"Changelog erstellt: {changelogPath}");
        }
        catch (Exception ex)
        {
            WriteWarning($"Hinweis: Changelog konnte nicht erstellt werden: {ex.Message}");
        }
    }

    private static void TryUpdateChangelogIndex(string repoRoot)
    {
        try
        {
            var changelogDir = Path.Combine(repoRoot, "Toolbox", "wwwroot", "Changelog");
            if (!Directory.Exists(changelogDir))
            {
                return;
            }

            var versions = new List<string>();
            foreach (var file in Directory.EnumerateFiles(changelogDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                if (name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryParseVersionInput(name, out var parsed, out _))
                {
                    continue;
                }

                versions.Add(parsed.FullText);
            }

            versions = versions
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            versions.Sort(CompareVersions);

            var indexPath = Path.Combine(changelogDir, "index.json");
            var json = JsonSerializer.Serialize(versions, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(indexPath, json);
            WriteSuccess($"Changelog-Index aktualisiert: {indexPath}");
        }
        catch (Exception ex)
        {
            WriteWarning($"Hinweis: Changelog-Index konnte nicht aktualisiert werden: {ex.Message}");
        }
    }

    private static bool ConfirmChangelogReady(string repoRoot, string appVersion)
    {
        var skip = Environment.GetEnvironmentVariable("TOOLBOX_SKIP_CHANGELOG_CONFIRM");
        if (IsAffirmative(skip))
        {
            WriteInfo("Changelog-Check uebersprungen (TOOLBOX_SKIP_CHANGELOG_CONFIRM).");
            return true;
        }

        var normalizedVersion = NormalizeVersionText(appVersion);
        var changelogPath = Path.Combine(repoRoot, "Toolbox", "wwwroot", "Changelog", $"{normalizedVersion}.md");
        WriteInfo($"Changelog: {changelogPath}");

        if (File.Exists(changelogPath))
        {
            try
            {
                var content = File.ReadAllText(changelogPath);
                if (content.Contains("TODO", StringComparison.OrdinalIgnoreCase))
                {
                    WriteWarning("Hinweis: Changelog enthaelt noch TODO.");
                }
            }
            catch { }
        }
        else
        {
            WriteWarning("Hinweis: Changelog-Datei nicht gefunden.");
        }

        Console.Write("Changelog fertig und geprueft? (j/N): ");
        var input = Console.ReadLine();
        if (!IsAffirmative(input))
        {
            return false;
        }

        TryProofreadChangelogFile(changelogPath);
        return true;
    }

    private sealed record ChangelogContext(string? BaseCommit, string GitLog, string DiffStat);

    private static string? TryLoadPreviousChangelog(string changelogDir, string? prevVersion)
    {
        if (string.IsNullOrWhiteSpace(prevVersion))
        {
            return null;
        }

        try
        {
            var normalizedPrev = NormalizeVersionText(prevVersion);
            var path = Path.Combine(changelogDir, $"{normalizedPrev}.md");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string AdaptChangelogVersion(string content, string newVersion)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return $"# {newVersion}\n";
        }

        using var reader = new StringReader(content);
        var sb = new StringBuilder();
        var replacedHeader = false;
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (!replacedHeader && !string.IsNullOrWhiteSpace(line))
            {
                if (line.TrimStart().StartsWith("#"))
                {
                    sb.AppendLine($"# {newVersion}");
                    replacedHeader = true;
                    continue;
                }

                sb.AppendLine($"# {newVersion}");
                sb.AppendLine();
                replacedHeader = true;
            }

            sb.AppendLine(line);
        }

        if (!replacedHeader)
        {
            return $"# {newVersion}\n{content.Trim()}";
        }

        return sb.ToString().TrimEnd();
    }

    private static ChangelogContext BuildChangelogContext(string repoRoot, string? prevVersion)
    {
        var baseCommit = TryFindVersionCommit(repoRoot, prevVersion);
        var gitLog = string.Empty;
        var diffStat = string.Empty;

        if (!string.IsNullOrWhiteSpace(baseCommit))
        {
            RunProcess("git", $"log {baseCommit}..HEAD --pretty=format:%h %s", repoRoot, out gitLog, out _);
            RunProcess("git", $"diff --stat {baseCommit}..HEAD", repoRoot, out diffStat, out _);
        }
        else
        {
            RunProcess("git", "log -n 50 --pretty=format:%h %s", repoRoot, out gitLog, out _);
            RunProcess("git", "diff --stat HEAD~50..HEAD", repoRoot, out diffStat, out _);
        }

        gitLog = TrimToMax(gitLog, 6000);
        diffStat = TrimToMax(diffStat, 4000);

        return new ChangelogContext(baseCommit, gitLog, diffStat);
    }

    private static string BuildChangelogContent(string appVersion, string? prevVersion, ChangelogContext context)
    {
        var provider = (Environment.GetEnvironmentVariable("TOOLBOX_CHANGELOG_PROVIDER") ?? "openai").Trim().ToLowerInvariant();
        if (provider == "openai" || (provider.Length == 0 && !string.IsNullOrWhiteSpace(GetOpenAiKey())))
        {
            var ai = TryGenerateChangelogOpenAi(appVersion, prevVersion, context);
            if (!string.IsNullOrWhiteSpace(ai))
            {
                return ai.Trim();
            }
        }

        return BuildChangelogTemplate(appVersion, prevVersion, context, provider);
    }

    private static string BuildChangelogTemplate(string appVersion, string? prevVersion, ChangelogContext context, string provider)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {NormalizeVersionText(appVersion)}");
        sb.AppendLine();
        sb.AppendLine("## Hinzugefuegt");
        sb.AppendLine("- TODO");
        sb.AppendLine();
        sb.AppendLine("## Geaendert");
        sb.AppendLine("- TODO");
        sb.AppendLine();
        sb.AppendLine("## Behoben");
        sb.AppendLine("- TODO");

        if (!string.IsNullOrWhiteSpace(context.GitLog) || !string.IsNullOrWhiteSpace(context.DiffStat))
        {
            sb.AppendLine();
            sb.AppendLine("<!-- Kontext:");
            if (!string.IsNullOrWhiteSpace(prevVersion))
            {
                sb.AppendLine($"Vorherige Version: {NormalizeVersionText(prevVersion)}");
            }
            if (!string.IsNullOrWhiteSpace(context.BaseCommit))
            {
                sb.AppendLine($"Basis-Commit: {context.BaseCommit}");
            }
            if (!string.IsNullOrWhiteSpace(context.GitLog))
            {
                sb.AppendLine("Git-Log:");
                sb.AppendLine(context.GitLog);
            }
            if (!string.IsNullOrWhiteSpace(context.DiffStat))
            {
                sb.AppendLine("Diff-Stat:");
                sb.AppendLine(context.DiffStat);
            }
            if (provider == "codex")
            {
                sb.AppendLine("Hinweis: Changelog kann per Codex verfeinert werden.");
            }
            sb.AppendLine("-->");
        }

        return sb.ToString();
    }

    private static string? TryGenerateChangelogOpenAi(string appVersion, string? prevVersion, ChangelogContext context)
    {
        try
        {
            var apiKey = GetOpenAiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var model = Environment.GetEnvironmentVariable("TOOLBOX_OPENAI_MODEL");
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-4o-mini";
            }

            var prompt = BuildOpenAiPrompt(appVersion, prevVersion, context);
            var payload = new
            {
                model,
                temperature = 0.2,
                messages = new[]
                {
                    new { role = "system", content = "Du erstellst kurze, praezise Release-Notes fuer eine interne Toolbox. Ausgabe nur Markdown." },
                    new { role = "user", content = prompt }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var response = client.PostAsync("https://api.openai.com/v1/chat/completions",
                new StringContent(json, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryProofreadChangelogOpenAi(string markdown)
    {
        try
        {
            var apiKey = GetOpenAiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var proofreadFlag = Environment.GetEnvironmentVariable("TOOLBOX_CHANGELOG_PROOFREAD");
            if (!string.IsNullOrWhiteSpace(proofreadFlag) && IsExplicitFalse(proofreadFlag))
            {
                return null;
            }

            var model = Environment.GetEnvironmentVariable("TOOLBOX_OPENAI_MODEL");
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-4o-mini";
            }

            var prompt = new StringBuilder();
            prompt.AppendLine("Pruefe den folgenden Markdown-Text nur auf Rechtschreibung/Grammatik.");
            prompt.AppendLine("Aendere NICHT die Struktur: keine neuen Ueberschriften, keine Reihenfolge aendern,");
            prompt.AppendLine("keine Listenpunkte hinzufuegen/entfernen und keine Formatierung veraendern.");
            prompt.AppendLine("Gib NUR den korrigierten Markdown-Text zurueck.");
            prompt.AppendLine();
            prompt.AppendLine(markdown);

            var payload = new
            {
                model,
                temperature = 0.1,
                messages = new[]
                {
                    new { role = "system", content = "Du korrigierst nur Rechtschreibung/Grammatik und bewahrst die Markdown-Struktur 1:1." },
                    new { role = "user", content = prompt.ToString() }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var spinner = new ConsoleSpinner("Proofreading (OpenAI)");
            var response = client.PostAsync("https://api.openai.com/v1/chat/completions",
                new StringContent(json, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                spinner.Stop("Proofreading fehlgeschlagen.", ConsoleColor.Yellow);
                return null;
            }

            var responseJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                spinner.Stop("Proofreading ohne Ergebnis.", ConsoleColor.Yellow);
                return null;
            }

            spinner.Stop("Proofreading abgeschlossen.", ConsoleColor.Green);
            return content.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static void TryProofreadChangelogFile(string changelogPath)
    {
        try
        {
            if (!File.Exists(changelogPath))
            {
                return;
            }

            var content = File.ReadAllText(changelogPath);
            var proofread = TryProofreadChangelogOpenAi(content);
            if (string.IsNullOrWhiteSpace(proofread) || string.Equals(proofread, content, StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(changelogPath, proofread);
            WriteSuccess("Changelog Proofreading angewendet.");
        }
        catch (Exception ex)
        {
            WriteWarning($"Hinweis: Proofreading fehlgeschlagen: {ex.Message}");
        }
    }

    private static string BuildOpenAiPrompt(string appVersion, string? prevVersion, ChangelogContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Erstelle einen Changelog fuer Version {NormalizeVersionText(appVersion)}.");
        if (!string.IsNullOrWhiteSpace(prevVersion))
        {
            sb.AppendLine($"Vorherige Version: {NormalizeVersionText(prevVersion)}.");
        }
        if (!string.IsNullOrWhiteSpace(context.BaseCommit))
        {
            sb.AppendLine($"Basis-Commit: {context.BaseCommit}.");
        }
        sb.AppendLine();
        sb.AppendLine("Nutze die folgenden Informationen:");
        if (!string.IsNullOrWhiteSpace(context.GitLog))
        {
            sb.AppendLine("Git-Log:");
            sb.AppendLine(context.GitLog);
        }
        if (!string.IsNullOrWhiteSpace(context.DiffStat))
        {
            sb.AppendLine("Diff-Stat:");
            sb.AppendLine(context.DiffStat);
        }
        sb.AppendLine();
        sb.AppendLine("Nutze ausschliesslich die angegebenen Aenderungen zwischen Basis-Commit und HEAD.");
        sb.AppendLine("Gib ausschliesslich Markdown aus. Struktur:");
        sb.AppendLine("# <Version>");
        sb.AppendLine("## Hinzugefuegt");
        sb.AppendLine("- ...");
        sb.AppendLine("## Geaendert");
        sb.AppendLine("- ...");
        sb.AppendLine("## Behoben");
        sb.AppendLine("- ...");
        sb.AppendLine("Wenn es nichts gibt, schreibe '- Keine Eintraege.' in der passenden Sektion.");
        return sb.ToString();
    }

    private static string? TryFindVersionCommit(string repoRoot, string? prevVersion)
    {
        if (string.IsNullOrWhiteSpace(prevVersion))
        {
            return TryFindLatestBumpCommit(repoRoot);
        }

        var normalized = NormalizeVersionText(prevVersion);
        var code = RunProcess("git", $"log --grep \"Bump to Version {normalized}\" -n 1 --pretty=format:%H", repoRoot, out var so, out _);
        if (code != 0)
        {
            return TryFindLatestBumpCommit(repoRoot);
        }

        var sha = so.Trim();
        return string.IsNullOrWhiteSpace(sha) ? TryFindLatestBumpCommit(repoRoot) : sha;
    }

    private static string? TryFindLatestBumpCommit(string repoRoot)
    {
        var code = RunProcess("git", "log --grep \"Bump to Version\" -n 1 --pretty=format:%H", repoRoot, out var so, out _);
        if (code != 0)
        {
            return null;
        }

        var sha = so.Trim();
        return string.IsNullOrWhiteSpace(sha) ? null : sha;
    }

    private static string? GetOpenAiKey()
    {
        return Environment.GetEnvironmentVariable("TOOLBOX_OPENAI_API_KEY")
               ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    private static string NormalizeVersionText(string versionText)
    {
        if (TryParseVersionInput(versionText, out var parsed, out _))
        {
            return parsed.FullText;
        }

        var s = (versionText ?? string.Empty).Trim();
        var plusIndex = s.IndexOf('+');
        if (plusIndex >= 0)
        {
            s = s[..plusIndex];
        }
        if (s.StartsWith('v') || s.StartsWith('V'))
        {
            s = s[1..];
        }
        return s;
    }

    private static string TrimToMax(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Length <= max)
        {
            return value.Trim();
        }

        return value.Substring(0, max).TrimEnd() + "\n... (gekuerzt)";
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
                WriteWarning($"Git-Status fehlgeschlagen ({status}). Ueberspringe Commit/Push.\n{se}");
                return;
            }
            if (string.IsNullOrWhiteSpace(so))
            {
                WriteInfo("Keine Aenderungen zum Commit. Ueberspringe Commit/Push.");
                return;
            }

            // Add alle Änderungen
            var addCode = RunProcess("git", "add -A", repoRoot, out so, out se);
            if (addCode != 0)
            {
                WriteWarning($"Git add fehlgeschlagen ({addCode}).\n{se}");
                return;
            }

            // Commit
            var msg = $"Bump to Version {version}";
            var commitCode = RunProcess("git", $"commit -m \"{msg}\"", repoRoot, out so, out se);
            if (commitCode != 0)
            {
                // Häufig: nothing to commit
                WriteWarning($"Git commit nicht ausgefuehrt ({commitCode}).\n{so}{se}");
                return;
            }

            WriteSuccess("Git commit erstellt. Pushe Aenderungen...");
            TryPushAllRemotes(repoRoot);
        }
        catch (Exception ex)
        {
            WriteWarning($"Git Commit/Push uebersprungen: {ex.Message}");
        }
    }

    private static void TryPushAllRemotes(string repoRoot)
    {
        var remotesCode = RunProcess("git", "remote", repoRoot, out var remotesOut, out var remotesErr);
        if (remotesCode != 0)
        {
            WriteWarning($"Git remotes fehlgeschlagen ({remotesCode}).\n{remotesErr}");
            return;
        }

        var remotes = remotesOut
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        if (remotes.Count == 0)
        {
            WriteWarning("Keine Git Remotes gefunden. Push wird uebersprungen.");
            return;
        }

        foreach (var remote in remotes)
        {
            var pushCode = RunProcess("git", $"push \"{remote}\" --all", repoRoot, out var so, out var se);
            if (pushCode != 0)
            {
                WriteWarning($"Git push zu '{remote}' fehlgeschlagen ({pushCode}).\n{se}");
                continue;
            }

            WriteSuccess($"Git push zu '{remote}' erfolgreich.");
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
                WriteError("Toolbox.csproj nicht gefunden.");
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
                WriteError("dotnet publish fehlgeschlagen:");
                Console.Error.WriteLine(stdOut);
                Console.Error.WriteLine(stdErr);
                return null;
            }
            return outDir;
        }
        catch (Exception ex)
        {
            WriteError($"Fehler beim Publish: {ex.Message}");
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
                WriteInfo($"[{appName}] Warte auf Debugger... PID={pid}");
                var last = DateTime.UtcNow;
                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(250);
                    if ((DateTime.UtcNow - last) > TimeSpan.FromSeconds(5))
                    {
                        WriteInfo($"[{appName}] ...warte weiterhin auf Debugger...");
                        last = DateTime.UtcNow;
                    }
                }
                WriteSuccess($"[{appName}] Debugger angehaengt.");
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

            var parts = parsed.NumericText.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var major = parts.Length > 0 ? parts[0] : "0";
            var minor = parts.Length > 1 ? parts[1] : "0";
            var build = parts.Length > 2 ? parts[2] : "0";
            var revision = parts.Length > 3 ? parts[3] : "0";
            var fourPart = $"{major}.{minor}.{build}.{revision}";

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
                WriteWarning($"Ungueltiges Format. {parseError}");
                WriteInfo("Beispiele: 1.2.3, 1.2.3.4, 1.2.3-beta");
                WriteInfo("Trennzeichen: '.', '_' oder ',' (z.B. 1_2_3).");
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
            WriteWarning($"Hinweis: PreRelease '{preRelease}' ist nicht erlaubt.");
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

    private static bool IsExplicitFalse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim().ToLowerInvariant();
        return v is "0" or "false" or "nein" or "no" or "n";
    }

    private static void WriteSection(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"== {title} ==");
        Console.ResetColor();
    }

    private static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    private static int RunProcessWithSpinner(ProcessStartInfo psi, string label)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var spinner = new ConsoleSpinner(label);
        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            spinner.Stop("Inno Setup abgeschlossen.", ConsoleColor.Green);
        }
        else
        {
            spinner.Stop("Inno Setup fehlgeschlagen.", ConsoleColor.Red);
            if (stdout.Length > 0)
            {
                WriteInfo("Inno Output:");
                Console.WriteLine(stdout.ToString().TrimEnd());
            }
            if (stderr.Length > 0)
            {
                WriteWarning("Inno Errors:");
                Console.WriteLine(stderr.ToString().TrimEnd());
            }
        }

        return process.ExitCode;
    }

    private sealed class ConsoleSpinner : IDisposable
    {
        private readonly string _label;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly int _left;
        private readonly int _top;
        private bool _stopped;

        public ConsoleSpinner(string label)
        {
            _label = label;
            _left = Console.CursorLeft;
            _top = Console.CursorTop;
            Console.CursorVisible = false;
            _task = Task.Run(SpinAsync);
        }

        private async Task SpinAsync()
        {
            var frames = new[] { "|", "/", "-", "\\" };
            var i = 0;
            while (!_cts.IsCancellationRequested)
            {
                WriteFrame(frames[i++ % frames.Length]);
                await Task.Delay(120);
            }
        }

        private void WriteFrame(string frame)
        {
            var elapsed = _sw.Elapsed;
            Console.SetCursorPosition(_left, _top);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(frame);
            Console.ResetColor();
            Console.Write($" {_label} ({FormatElapsed(elapsed)})");
            Console.Write("   ");
        }

        public void Stop(string? finalMessage, ConsoleColor? color = null)
        {
            if (_stopped) return;
            _stopped = true;
            _cts.Cancel();
            try { _task.Wait(500); } catch { }
            ClearLine();
            if (!string.IsNullOrWhiteSpace(finalMessage))
            {
                if (color.HasValue) Console.ForegroundColor = color.Value;
                Console.WriteLine(finalMessage);
                Console.ResetColor();
            }
            Console.CursorVisible = true;
        }

        public void Dispose()
        {
            Stop(null);
            _cts.Dispose();
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalHours >= 1
                ? elapsed.ToString("hh\\:mm\\:ss")
                : elapsed.ToString("mm\\:ss");
        }

        private static void ClearLine()
        {
            var width = Console.WindowWidth;
            if (width <= 0) return;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', width - 1));
            Console.SetCursorPosition(0, Console.CursorTop);
        }
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
