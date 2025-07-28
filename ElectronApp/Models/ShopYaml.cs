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

    public static void SaveConfiguration(string yamlFilePath, ZionConfiguration config)
    {
        var lines = File.ReadAllLines(yamlFilePath).ToList();

        void Replace(string key, string value)
        {
            var regex = new Regex(@"^(\s*)" + Regex.Escape(key) + @":.*$");
            for (int i = 0; i < lines.Count; i++)
            {
                var match = regex.Match(lines[i]);
                if (match.Success)
                {
                    var indent = match.Groups[1].Value;
                    lines[i] = $"{indent}{key}: {value}";
                    return;
                }
            }
        }

        void ReplaceSub(string section, string key, string value)
        {
            var sectionRegex = new Regex(@"^(\s*)" + Regex.Escape(section) + @":\s*$");
            for (int i = 0; i < lines.Count; i++)
            {
                var sectionMatch = sectionRegex.Match(lines[i]);
                if (sectionMatch.Success)
                {
                    var baseIndent = sectionMatch.Groups[1].Value;
                    var indent = baseIndent + "  ";
                    var keyRegex = new Regex(@"^" + Regex.Escape(indent + key) + @":.*$");
                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        if (!lines[j].StartsWith(indent))
                            break;
                        if (keyRegex.IsMatch(lines[j]))
                        {
                            lines[j] = $"{indent}{key}: {value}";
                            return;
                        }
                    }
                    break;
                }
            }
        }

        Replace("shopId", config.ShopId.ToString());
        Replace("userId", config.UserId.ToString());
        Replace("baseTheme", config.BaseTheme);
        Replace("themeOverwrite", config.ThemeOverwrite);

        if (config.ApplicationConfiguration != null)
        {
            ReplaceSub("applicationConfiguration", "cacheMode", config.ApplicationConfiguration.CacheMode);
            ReplaceSub("applicationConfiguration", "applicationType", config.ApplicationConfiguration.ApplicationType);
            ReplaceSub("applicationConfiguration", "databaseTpe", config.ApplicationConfiguration.DatabaseTpe);
            ReplaceSub("applicationConfiguration", "databaseVersion", config.ApplicationConfiguration.DatabaseVersion);
        }

        if (config.DatabaseConnections != null)
        {
            var sectionRegex = new Regex(@"^(\s*)databaseConnections:\s*$");
            for (int i = 0; i < lines.Count; i++)
            {
                var m = sectionRegex.Match(lines[i]);
                if (m.Success)
                {
                    var baseIndent = m.Groups[1].Value;
                    var indentItem = baseIndent + "  - ";
                    var indentProp = baseIndent + "    ";
                    int removeIndex = i + 1;
                    while (removeIndex < lines.Count && lines[removeIndex].StartsWith(baseIndent + "  "))
                    {
                        lines.RemoveAt(removeIndex);
                    }

                    var newLines = new List<string>();
                    foreach (var conn in config.DatabaseConnections)
                    {
                        newLines.Add($"{indentItem}server: {conn.Server}");
                        newLines.Add($"{indentProp}database: {conn.Database}");
                        newLines.Add($"{indentProp}user: {conn.User}");
                        newLines.Add($"{indentProp}password: {conn.Password}");
                        newLines.Add($"{indentProp}maxPoolSize: {conn.MaxPoolSize}");
                        newLines.Add($"{indentProp}encrypt: {conn.Encrypt.ToString().ToLower()}");
                        newLines.Add($"{indentProp}trustServerCertificate: {conn.TrustServerCertificate.ToString().ToLower()}");
                    }

                    lines.InsertRange(i + 1, newLines);
                    break;
                }
            }
        }

        if (config.Extensions != null)
            ReplaceSub("extensions", "directory", config.Extensions.Directory);
        if (config.MainAssembly != null)
            ReplaceSub("mainAssembly", "assemblyName", config.MainAssembly.AssemblyName);
        if (config.LicenseService != null)
            ReplaceSub("licenseService", "address", config.LicenseService.Address);
        if (config.ApiConnection != null)
        {
            ReplaceSub("apiConnection", "url", config.ApiConnection.Url);
            ReplaceSub("apiConnection", "token", config.ApiConnection.Token);
        }

        File.WriteAllLines(yamlFilePath, lines);
    }

}