using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Toolbox.Services;

public sealed class ChangelogService
{
    private readonly string _pendingStatePath;
    private readonly string _changelogDir;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ChangelogService()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var updaterDir = Path.Combine(local, "Shop-Toolbox", "Updater");
        Directory.CreateDirectory(updaterDir);
        _pendingStatePath = Path.Combine(updaterDir, "pending-changelog.json");
        _changelogDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "Changelog");
    }

    public string GetCurrentVersion()
    {
        var current = GetInformationalVersion()
                      ?? (typeof(ChangelogService).Assembly.GetName().Version?.ToString() ?? "0.0.0");
        return NormalizeVersionText(current);
    }

    public void StorePendingVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return;
        try
        {
            var state = new PendingChangelogState
            {
                Version = NormalizeVersionText(version),
                SavedAtUtc = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            File.WriteAllText(_pendingStatePath, json);
        }
        catch
        {
            // ignore
        }
    }

    public async Task<ChangelogReport?> TryBuildPendingReportAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return null;
        }

        var pending = await ReadPendingVersionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(pending))
        {
            return null;
        }

        var normalizedCurrent = NormalizeVersionText(currentVersion);
        var normalizedPending = NormalizeVersionText(pending);
        if (CompareVersions(normalizedCurrent, normalizedPending) <= 0)
        {
            return null;
        }

        var entries = await LoadEntriesAsync(normalizedPending, normalizedCurrent, cancellationToken);
        return new ChangelogReport(normalizedPending, normalizedCurrent, entries);
    }

    public void MarkPendingAsShown()
    {
        try
        {
            if (File.Exists(_pendingStatePath))
            {
                File.Delete(_pendingStatePath);
            }
        }
        catch
        {
            // ignore
        }
    }

    private async Task<string?> ReadPendingVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_pendingStatePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_pendingStatePath, cancellationToken);
            var state = JsonSerializer.Deserialize<PendingChangelogState>(json, _jsonOptions);
            return state?.Version;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<ChangelogEntry>> LoadEntriesAsync(string fromVersionExclusive, string toVersionInclusive, CancellationToken cancellationToken)
    {
        var entries = new List<ChangelogEntry>();
        if (!Directory.Exists(_changelogDir))
        {
            return entries;
        }

        if (!TryParseVersionInfo(fromVersionExclusive, out var fromInfo) ||
            !TryParseVersionInfo(toVersionInclusive, out var toInfo))
        {
            return entries;
        }

        foreach (var file in Directory.EnumerateFiles(_changelogDir, "*.md", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!TryParseVersionInfo(name, out var info))
            {
                continue;
            }

            if (CompareVersionInfos(info, fromInfo) <= 0)
            {
                continue;
            }

            if (CompareVersionInfos(info, toInfo) > 0)
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            entries.Add(new ChangelogEntry(info.NormalizedText, content));
        }

        entries.Sort((a, b) => CompareVersions(a.Version, b.Version));
        return entries;
    }

    private static string? GetInformationalVersion()
    {
        try
        {
            var asm = typeof(ChangelogService).Assembly;
            var attr = asm
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault();
            var v = attr?.InformationalVersion ?? asm.GetName().Version?.ToString();
            if (!string.IsNullOrEmpty(v))
            {
                var idx = v.IndexOf('+');
                if (idx > 0) v = v.Substring(0, idx);
            }
            return v;
        }
        catch
        {
            return null;
        }
    }

    private sealed record PendingChangelogState
    {
        public string Version { get; init; } = "0.0.0";
        public DateTime SavedAtUtc { get; init; }
    }

    private sealed record VersionInfo(string NumericText, Version Numeric, string? PreRelease)
    {
        public string NormalizedText => string.IsNullOrWhiteSpace(PreRelease) ? NumericText : $"{NumericText}-{PreRelease}";
    }

    private static string NormalizeVersionText(string v)
    {
        return TryParseVersionInfo(v, out var info) ? info.NormalizedText : v.Trim();
    }

    private static bool TryParseVersionInfo(string v, out VersionInfo info)
    {
        info = new VersionInfo("0.0.0", new Version(0, 0, 0, 0), null);
        var s = (v ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

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
            return false;
        }

        info = new VersionInfo(s, ver, pre);
        return true;
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
        if (!TryParseVersionInfo(a, out var ai) || !TryParseVersionInfo(b, out var bi))
        {
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }
        return CompareVersionInfos(ai, bi);
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
        if (!hasA) return 1;
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
                return aNum ? -1 : 1;
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
}

public sealed record ChangelogEntry(string Version, string Content);

public sealed record ChangelogReport(string PreviousVersion, string CurrentVersion, IReadOnlyList<ChangelogEntry> Entries);
