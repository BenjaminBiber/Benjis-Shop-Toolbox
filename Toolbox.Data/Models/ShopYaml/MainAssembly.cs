using YamlDotNet.Serialization;

namespace Toolbox.Data.Models.ShopYaml;

public class MainAssembly : IEquatable<MainAssembly>
{
    [YamlMember(Alias = "assemblyName")]
    public string AssemblyName { get; set; }

    public MainAssembly()
    {
        AssemblyName = "4sellers.Redwood.Web.Shop";
    }
    public bool Equals(MainAssembly? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return string.Equals(AssemblyName ?? string.Empty, other.AssemblyName ?? string.Empty, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is MainAssembly other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(AssemblyName ?? string.Empty, StringComparer.Ordinal);
        return hc.ToHashCode();
    }
}
