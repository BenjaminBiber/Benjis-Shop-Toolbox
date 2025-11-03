using YamlDotNet.Serialization;

namespace Toolbox.Data.Models.ShopYaml;

public class ExtensionConfiguration : IEquatable<ExtensionConfiguration>
{
    [YamlMember(Alias = "directory")]
    public string Directory { get; set; }

    public ExtensionConfiguration()
    {
        Directory = "extensions";
    }
    public bool Equals(ExtensionConfiguration? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return string.Equals(Directory ?? string.Empty, other.Directory ?? string.Empty, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is ExtensionConfiguration other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Directory ?? string.Empty, StringComparer.Ordinal);
        return hc.ToHashCode();
    }
}
