using System.Diagnostics;
using Toolbox.Data.Models;
using Toolbox.Data.Models.Interfaces;
using Toolbox.Data.Models.ShopYaml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Toolbox.Data.Services;

public class ShopYamlService
{
    public static ShopsystemConfig LoadConfiguration(string yamlPath)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        using var reader = new StreamReader(yamlPath);
        var root = deserializer.Deserialize<ShopsystemConfig>(reader);
        return root;
    }

    public static void WriteConfiguration(string yamlPath, ShopsystemConfig shopsystemConfig)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(shopsystemConfig);
        using var writer = new StreamWriter(yamlPath);
        writer.Write(yaml);
        writer.Flush();
        writer.Close();
    }

    public static void SetNewThemeOverwrite(string yamlPath, string newTheme)
    {
        if (String.IsNullOrEmpty(newTheme) || !File.Exists(yamlPath))
        {
            return;
        }
        
        var currentConfig = LoadConfiguration(yamlPath);
        currentConfig.ZionConfiguration.ThemeOverwrite = newTheme;
        WriteConfiguration(yamlPath, currentConfig);
    }
    
    public static void OpenYamlInVSCode(string yamlPath)
    {
        if (string.IsNullOrWhiteSpace(yamlPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(yamlPath);

        if (!File.Exists(fullPath))
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"\"{fullPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            return;
        }
    }
}