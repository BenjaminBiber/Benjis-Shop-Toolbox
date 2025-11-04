using System.Text.Json;
using Toolbox.Data.Models;

namespace Toolbox.Data.Services;

public class ShopSystemConfigService
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<ShopSystemConfig> LoadAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ShopSystemConfig();
            }
            await using var fs = File.OpenRead(filePath);
            var config = await JsonSerializer.DeserializeAsync<ShopSystemConfig>(fs, _options);
            return config ?? new ShopSystemConfig();
        }
        catch
        {
            return new ShopSystemConfig();
        }
    }

    public async Task<bool> SaveAsync(string filePath, ShopSystemConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await using var fs = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fs, config, _options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetShopPathAsync(string filePath, string shopPath)
    {
        var cfg = await LoadAsync(filePath);
        cfg.Directories ??= new ShopSystemDirectories();
        cfg.Directories.ShopPath = shopPath;
        return await SaveAsync(filePath, cfg);
    }

    public string? TryLocateConfig(string rootFolder, string fileName = "shopsystem.json")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
                return null;

            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git", ".svn", ".hg", ".vs", ".idea", ".vscode",
                "bin", "obj", "packages", "node_modules"
            };

            var queue = new Queue<string>();
            queue.Enqueue(rootFolder);

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();
                try
                {
                    var candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate))
                        return candidate;

                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        var name = Path.GetFileName(sub);
                        if (ignore.Contains(name)) continue;
                        queue.Enqueue(sub);
                    }
                }
                catch
                {
                    // skip directories we can't access
                }
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    public async Task<bool> SetShopPathUsingRootAsync(string rootFolder, string shopPath)
    {
        var path = TryLocateConfig(rootFolder);
        if (string.IsNullOrEmpty(path))
            return false;
        return await SetShopPathAsync(path, shopPath);
    }
}
