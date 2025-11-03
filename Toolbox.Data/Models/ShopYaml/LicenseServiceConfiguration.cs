using YamlDotNet.Serialization;

namespace Toolbox.Data.Models.ShopYaml;

public class LicenseServiceConfiguration : IEquatable<LicenseServiceConfiguration>
{
    [YamlMember(Alias = "address")]
    public string Address { get; set; }
    
    [YamlMember(Alias = "customerNumber")]
    public string CustomerNumber { get; set; }
    
    [YamlMember(Alias = "username")]
    public string Username { get; set; }
    
    [YamlMember(Alias = "password")]
    public string Password { get; set; }
    
    [YamlMember(Alias = "isStaging")]
    public bool IsStaging { get; set; }

    public LicenseServiceConfiguration()
    {
        Address = "http://qa-shop-v2-90:4444/4sellersLicenseService/unsecure";
        CustomerNumber = "placeholder";
        Username = "placeholder";
        Password = "placeholder";
        IsStaging = false;
    }
    public bool Equals(LicenseServiceConfiguration? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return string.Equals(Address ?? string.Empty, other.Address ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(CustomerNumber ?? string.Empty, other.CustomerNumber ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(Username ?? string.Empty, other.Username ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(Password ?? string.Empty, other.Password ?? string.Empty, StringComparison.Ordinal)
               && IsStaging == other.IsStaging;
    }

    public override bool Equals(object? obj) => obj is LicenseServiceConfiguration other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Address ?? string.Empty, StringComparer.Ordinal);
        hc.Add(CustomerNumber ?? string.Empty, StringComparer.Ordinal);
        hc.Add(Username ?? string.Empty, StringComparer.Ordinal);
        hc.Add(Password ?? string.Empty, StringComparer.Ordinal);
        hc.Add(IsStaging);
        return hc.ToHashCode();
    }
}
