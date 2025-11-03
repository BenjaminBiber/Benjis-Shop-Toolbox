using YamlDotNet.Serialization;
namespace Toolbox.Data.Models.ShopYaml;

public class ShopsystemConfig : IEquatable<ShopsystemConfig>
{
    [YamlMember(Alias = "zionConfiguration")]
    public ZionConfiguration ZionConfiguration { get; set; }

    public ShopsystemConfig()
    {
        ZionConfiguration = new ZionConfiguration();
    }
    public bool Equals(ShopsystemConfig? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return EqualityComparer<ZionConfiguration>.Default.Equals(ZionConfiguration, other.ZionConfiguration);
    }

    public override bool Equals(object? obj) => obj is ShopsystemConfig other && Equals(other);

    public override int GetHashCode()
    {
        return ZionConfiguration?.GetHashCode() ?? 0;
    }
}
