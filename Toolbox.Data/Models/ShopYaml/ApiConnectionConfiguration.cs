using YamlDotNet.Serialization;

namespace Toolbox.Data.Models.ShopYaml;

public class ApiConnectionConfiguration : IEquatable<ApiConnectionConfiguration>
{
    [YamlMember(Alias = "url")]
    public string Url { get; set; }
    
    [YamlMember(Alias = "token")]
    public string Token { get; set; }

    public ApiConnectionConfiguration()
    {
        Url = string.Empty;
        Token = string.Empty;
    }
    public bool Equals(ApiConnectionConfiguration? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return string.Equals(Url ?? string.Empty, other.Url ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(Token ?? string.Empty, other.Token ?? string.Empty, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is ApiConnectionConfiguration other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Url ?? string.Empty, StringComparer.Ordinal);
        hc.Add(Token ?? string.Empty, StringComparer.Ordinal);
        return hc.ToHashCode();
    }
}
