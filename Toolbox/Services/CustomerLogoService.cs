using System.IO;

namespace Toolbox.Services;

/// <summary>
/// Resolves logo image URLs for customers based on their CustomerId.
/// Logos are stored in wwwroot/images/Brands/{customerId}.{ext}
/// </summary>
public class CustomerLogoService
{
    private static readonly string[] Extensions = [".svg", ".png", ".jpg", ".jpeg", ".webp"];
    private readonly string _brandsDir;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public CustomerLogoService()
    {
        _brandsDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "Brands");
    }

    /// <summary>
    /// Returns the relative URL (e.g. "images/Brands/10464.png") if a logo exists for the given CustomerId,
    /// or null if no logo file is found.
    /// </summary>
    public string? GetLogoUrl(string? customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return null;

        if (_cache.TryGetValue(customerId, out var cached))
            return cached == "" ? null : cached;

        foreach (var ext in Extensions)
        {
            var file = Path.Combine(_brandsDir, customerId + ext);
            if (File.Exists(file))
            {
                var url = $"images/Brands/{customerId}{ext}";
                _cache[customerId] = url;
                return url;
            }
        }

        _cache[customerId] = "";
        return null;
    }
}
