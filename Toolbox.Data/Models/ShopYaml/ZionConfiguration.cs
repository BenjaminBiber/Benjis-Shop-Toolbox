using Toolbox.Data.Models.ShopYaml;
using YamlDotNet.Serialization;
namespace Toolbox.Data.Models;

public class ZionConfiguration : IEquatable<ZionConfiguration>
{
    [YamlMember(Alias = "shopId")]
    public int ShopId { get; set; }
    
    [YamlMember(Alias = "userId")]
    public int UserId { get; set; }
    
    [YamlMember(Alias = "baseTheme")]
    public string BaseTheme { get; set; }
    
    [YamlMember(Alias = "themeOverwrite")]
    public string ThemeOverwrite { get; set; }
    
    [YamlMember(Alias = "applicationConfiguration")]
    public ApplicationConfiguration ApplicationConfiguration { get; set; } 
    
    [YamlMember(Alias = "databaseConnections")]
    public List<DatabaseConnection> DatabaseConnections { get; set; } 
    
    [YamlMember(Alias = "extensions")]
    public ExtensionConfiguration Extensions { get; set; }
    
    [YamlMember(Alias = "mainAssembly")]
    public MainAssembly MainAssembly { get; set; } 
    
    [YamlMember(Alias = "licenseService")]
    public LicenseServiceConfiguration LicenseService { get; set; } 
    
    [YamlMember(Alias = "apiConnection")]
    public ApiConnectionConfiguration? ApiConnection { get; set; }

    public ZionConfiguration()
    {
        ShopId = 1;
        UserId = 1;
        BaseTheme = "4SELLERS_Responsive_4";
        ThemeOverwrite = "4SELLERS_Responsive_4";
        ApplicationConfiguration = new ApplicationConfiguration();
        DatabaseConnections = new List<DatabaseConnection>();
        Extensions = new ExtensionConfiguration();
        MainAssembly = new MainAssembly();
        LicenseService = new LicenseServiceConfiguration();
        ApiConnection = null;
    }
    public bool Equals(ZionConfiguration? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        bool dbEqual = DatabaseConnections != null && other.DatabaseConnections != null
                        ? DatabaseConnections.SequenceEqual(other.DatabaseConnections)
                        : DatabaseConnections == other.DatabaseConnections; // both null

        return ShopId == other.ShopId
               && UserId == other.UserId
               && string.Equals(BaseTheme ?? string.Empty, other.BaseTheme ?? string.Empty, StringComparison.Ordinal)
               && string.Equals(ThemeOverwrite ?? string.Empty, other.ThemeOverwrite ?? string.Empty, StringComparison.Ordinal)
               && EqualityComparer<ApplicationConfiguration>.Default.Equals(ApplicationConfiguration, other.ApplicationConfiguration)
               && dbEqual
               && EqualityComparer<ExtensionConfiguration>.Default.Equals(Extensions, other.Extensions)
               && EqualityComparer<MainAssembly>.Default.Equals(MainAssembly, other.MainAssembly)
               && EqualityComparer<LicenseServiceConfiguration>.Default.Equals(LicenseService, other.LicenseService)
               && EqualityComparer<ApiConnectionConfiguration?>.Default.Equals(ApiConnection, other.ApiConnection);
    }

    public override bool Equals(object? obj) => obj is ZionConfiguration other && Equals(other);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(ShopId);
        hc.Add(UserId);
        hc.Add(BaseTheme ?? string.Empty, StringComparer.Ordinal);
        hc.Add(ThemeOverwrite ?? string.Empty, StringComparer.Ordinal);
        hc.Add(ApplicationConfiguration);
        if (DatabaseConnections != null)
        {
            foreach (var c in DatabaseConnections)
                hc.Add(c);
        }
        hc.Add(Extensions);
        hc.Add(MainAssembly);
        hc.Add(LicenseService);
        hc.Add(ApiConnection);
        return hc.ToHashCode();
    }
}
