using YamlDotNet.Serialization;
namespace Toolbox.Data.Models.ShopYaml;

public class ApplicationConfiguration : IEquatable<ApplicationConfiguration>
{
    [YamlMember(Alias = "cacheMode")]
    public string CacheMode { get; set; }
    
    [YamlMember(Alias = "applicationType")]
    public string ApplicationType { get; set; } 
    
    [YamlMember(Alias = "databaseTpe")]
    public string DatabaseTpe { get; set; }
    
    [YamlMember(Alias = "databaseVersion")]
    public string DatabaseVersion { get; set; }

    public ApplicationConfiguration()
    {
        CacheMode = "Disabled";
        ApplicationType = "Webshop";
        DatabaseTpe = "MSSQL";
        DatabaseVersion = "Mssql110";
    }
    public bool Equals(ApplicationConfiguration? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return string.Equals(CacheMode ?? string.Empty, other.CacheMode ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(ApplicationType ?? string.Empty, other.ApplicationType ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(DatabaseTpe ?? string.Empty, other.DatabaseTpe ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(DatabaseVersion ?? string.Empty, other.DatabaseVersion ?? string.Empty, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is ApplicationConfiguration other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(CacheMode ?? string.Empty, StringComparer.Ordinal);
        hc.Add(ApplicationType ?? string.Empty, StringComparer.Ordinal);
        hc.Add(DatabaseTpe ?? string.Empty, StringComparer.Ordinal);
        hc.Add(DatabaseVersion ?? string.Empty, StringComparer.Ordinal);
        return hc.ToHashCode();
    }
}
