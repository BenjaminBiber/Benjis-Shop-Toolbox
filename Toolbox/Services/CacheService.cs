using System.Collections.Concurrent;
using System.IO;
using Toolbox.Data.Models;

namespace Toolbox.Services;

/// <summary>
/// Simple in-memory cache for expensive theme/extension lookups.
/// </summary>
public class CacheService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, CacheEntry<ThemeInfo>> _themeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CacheEntry<ExtensionInfo>> _extensionCache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ThemeInfo> GetThemes(string shopThemesPath, string shopYamlPath, Func<IEnumerable<ThemeInfo>> factory)
    {
        var key = BuildThemeKey(shopThemesPath, shopYamlPath);
        if (TryGetValid(_themeCache, key, out var cached))
        {
            return cached;
        }

        var created = factory().ToList();
        _themeCache[key] = new CacheEntry<ThemeInfo>(created, DateTimeOffset.UtcNow, DefaultLifetime);
        return created;
    }

    public IReadOnlyList<ExtensionInfo> GetExtensions(Func<IEnumerable<ExtensionInfo>> factory)
    {
        const string key = "extensions";
        if (TryGetValid(_extensionCache, key, out var cached))
        {
            return cached;
        }

        var created = factory().ToList();
        _extensionCache[key] = new CacheEntry<ExtensionInfo>(created, DateTimeOffset.UtcNow, DefaultLifetime);
        return created;
    }

    public void InvalidateThemes(string? shopThemesPath = null, string? shopYamlPath = null)
    {
        if (string.IsNullOrWhiteSpace(shopThemesPath) && string.IsNullOrWhiteSpace(shopYamlPath))
        {
            _themeCache.Clear();
            return;
        }

        var normalizedShopPath = NormalizePath(shopThemesPath);
        var normalizedYamlPath = NormalizePath(shopYamlPath);

        foreach (var key in _themeCache.Keys)
        {
            var (_, shopPath, yamlPath) = ParseThemeKey(key);

            if (!string.IsNullOrWhiteSpace(normalizedShopPath)
                && !string.Equals(shopPath, normalizedShopPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalizedYamlPath)
                && !string.Equals(yamlPath, normalizedYamlPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _themeCache.TryRemove(key, out _);
        }
    }

    public void InvalidateExtensions() => _extensionCache.Clear();

    private static bool TryGetValid<T>(ConcurrentDictionary<string, CacheEntry<T>> cache, string key, out IReadOnlyList<T> value)
    {
        if (cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired)
            {
                value = entry.Value;
                return true;
            }

            cache.TryRemove(key, out _);
        }

        value = Array.Empty<T>();
        return false;
    }

    private static string BuildThemeKey(string shopThemesPath, string shopYamlPath)
    {
        return $"themes|{NormalizePath(shopThemesPath)}|{NormalizePath(shopYamlPath)}";
    }

    private static (string Prefix, string ShopThemesPath, string ShopYamlPath) ParseThemeKey(string key)
    {
        var parts = key.Split('|');
        if (parts.Length != 3)
            return (string.Empty, string.Empty, string.Empty);

        return (parts[0], parts[1], parts[2]);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            var normalized = Path.GetFullPath(path);
            return Path.TrimEndingDirectorySeparator(normalized);
        }
        catch
        {
            return path.Trim();
        }
    }

    private sealed record CacheEntry<T>(IReadOnlyList<T> Value, DateTimeOffset CreatedAt, TimeSpan Lifetime)
    {
        public bool IsExpired => Lifetime > TimeSpan.Zero && (DateTimeOffset.UtcNow - CreatedAt) > Lifetime;
    }
}
