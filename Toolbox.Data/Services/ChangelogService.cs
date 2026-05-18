using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Toolbox.Data.Services;

public interface IChangelogService
{
    Task<string> BuildMarkdownAsync(string previousVersion, string currentVersion, CancellationToken cancellationToken = default);
}

public sealed class ChangelogService : IChangelogService
{
    private readonly string _changelogDir;
    private readonly string _indexPath;
    private readonly ILogger<ChangelogService>? _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChangelogService(ILogger<ChangelogService>? logger = null)
    {
        _logger = logger;
        _changelogDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "Changelog");
        _indexPath = Path.Combine(_changelogDir, "index.json");
    }

    public async Task<string> BuildMarkdownAsync(string previousVersion, string currentVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return string.Empty;
        }

        var normalizedCurrent = Versioning.NormalizeVersionText(currentVersion);
        var normalizedPrevious = Versioning.NormalizeVersionText(previousVersion);

        var index = await LoadIndexAsync(cancellationToken);
        if (index.Count == 0)
        {
            return await BuildSingleVersionMarkdownAsync(normalizedCurrent, cancellationToken);
        }

        if (!Versioning.TryParseVersionInfo(normalizedPrevious, out var fromInfo) ||
            !Versioning.TryParseVersionInfo(normalizedCurrent, out var toInfo))
        {
            _logger?.LogWarning("Changelog versions could not be parsed. previous={Previous} current={Current}", normalizedPrevious, normalizedCurrent);
            return await BuildSingleVersionMarkdownAsync(normalizedCurrent, cancellationToken);
        }

        var selected = new List<Versioning.VersionInfo>();
        foreach (var version in index)
        {
            if (!Versioning.TryParseVersionInfo(version, out var info))
            {
                _logger?.LogWarning("Changelog index entry skipped (invalid version): {Version}", version);
                continue;
            }

            if (Versioning.CompareVersionInfos(info, fromInfo) <= 0)
            {
                continue;
            }

            if (Versioning.CompareVersionInfos(info, toInfo) > 0)
            {
                continue;
            }

            selected.Add(info);
        }

        if (selected.Count == 0)
        {
            return await BuildSingleVersionMarkdownAsync(normalizedCurrent, cancellationToken);
        }

        selected.Sort(Versioning.CompareVersionInfos);

        var sb = new StringBuilder();
        foreach (var info in selected)
        {
            var content = await ReadMarkdownFileAsync(info.NormalizedText, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger?.LogWarning("Changelog markdown missing for version: {Version}", info.NormalizedText);
                continue;
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine($"## {info.NormalizedText}");
            sb.AppendLine();
            sb.AppendLine(content.Trim());
        }

        if (sb.Length == 0)
        {
            return await BuildSingleVersionMarkdownAsync(normalizedCurrent, cancellationToken);
        }

        return sb.ToString().Trim();
    }

    private async Task<List<string>> LoadIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_indexPath))
            {
                _logger?.LogWarning("Changelog index not found: {Path}", _indexPath);
                return new List<string>();
            }

            var json = await File.ReadAllTextAsync(_indexPath, cancellationToken);
            var list = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Changelog index could not be read: {Path}", _indexPath);
            return new List<string>();
        }
    }

    private async Task<string> BuildSingleVersionMarkdownAsync(string currentVersion, CancellationToken cancellationToken)
    {
        var content = await ReadMarkdownFileAsync(currentVersion, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return $"## {currentVersion}\nKeine Changelog-Datei gefunden.";
        }

        return $"## {currentVersion}\n\n{content.Trim()}";
    }

    private async Task<string?> ReadMarkdownFileAsync(string version, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(_changelogDir, $"{version}.md");
            if (!File.Exists(path))
            {
                return null;
            }

            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Changelog markdown could not be read for version: {Version}", version);
            return null;
        }
    }
}

