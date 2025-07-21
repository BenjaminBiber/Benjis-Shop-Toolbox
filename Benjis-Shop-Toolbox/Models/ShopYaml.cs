using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Benjis_Shop_Toolbox.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
public class ZionRoot
{
    [YamlMember(Alias = "zionConfiguration")]
    public ZionConfiguration ZionConfiguration { get; set; }
}

public class ZionConfiguration
{
    public int ShopId { get; set; }
    public int UserId { get; set; }
    public string BaseTheme { get; set; }
    public string ThemeOverwrite { get; set; }
    public ApplicationConfiguration ApplicationConfiguration { get; set; }
    public List<DatabaseConnection> DatabaseConnections { get; set; }
    public Extensions Extensions { get; set; }
    public MainAssembly MainAssembly { get; set; }
    public LicenseService LicenseService { get; set; }
    public ApiConnection ApiConnection { get; set; }
}

public class ApiConnection
{
    public string Url { get; set; }
    public string Token { get; set; }
}
public class ApplicationConfiguration
{
    public string CacheMode { get; set; }
    public string ApplicationType { get; set; }
    public string DatabaseTpe { get; set; }
    public string DatabaseVersion { get; set; }
}

public class DatabaseConnection
{
    public string Server { get; set; }
    public string Database { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
    public int MaxPoolSize { get; set; }
    public bool Encrypt { get; set; }
    public bool TrustServerCertificate { get; set; }
}

public class Extensions
{
    public string Directory { get; set; }
}

public class MainAssembly
{
    public string AssemblyName { get; set; }
}

public class LicenseService
{
    public string Address { get; set; }
}

public static class ShopYamlLoader
{
    public static ZionConfiguration LoadConfiguration(string pfadZurYamlDatei)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        using var reader = new StreamReader(pfadZurYamlDatei);
        var root = deserializer.Deserialize<ZionRoot>(reader);
        return root.ZionConfiguration;
    }
    
    public static bool IsThemeOverride(ZionConfiguration config, string themeName)
    {
        return !string.IsNullOrEmpty(config.ThemeOverwrite) && config.ThemeOverwrite == themeName;
    }
    
    public static void UpdateConfig(string yamlFilePath, string newTheme)
    {
        var lines = File.ReadAllLines(yamlFilePath);
        var pattern = @"^\s*themeOverwrite:\s*.*$";

        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], pattern))
            {
                var indent = Regex.Match(lines[i], @"^\s*").Value;
                lines[i] = $"{indent}themeOverwrite: {newTheme}";
                break;
            }
        }

        File.WriteAllLines(yamlFilePath, lines);
    }

}