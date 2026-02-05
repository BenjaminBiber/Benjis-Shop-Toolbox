using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Toolbox.Data.Services;

public enum ExtensionSemanticVersion
{
    Major,
    Minor,
    Build,
    Shop,
    Feature,
    Bug
}

public sealed record ExtensionVersionResult(
    bool Success,
    Version? PreviousVersion,
    Version? NewVersion,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

public interface IExtensionVersionService
{
    ExtensionVersionResult SetVersion(string extensionRoot, Version version, string? changelogMessage = null);
    ExtensionVersionResult UpgradeVersion(string extensionRoot, ExtensionSemanticVersion semanticVersion, string? changelogMessage = null);
    Version? ReadVersion(string extensionRoot);
}

public sealed class ExtensionVersionService : IExtensionVersionService
{
    public ExtensionVersionResult SetVersion(string extensionRoot, Version version, string? changelogMessage = null)
    {
        var log = new ExtensionVersionLog();
        if (!ExtensionVersionPaths.TryNormalizeRoot(extensionRoot, log, out var root))
        {
            return log.ToResult(null, null);
        }

        var previousVersion = ExtensionVersionHelper.ReadVersion(root, log);

        ExtensionVersionHelper.SetInstallScriptVersion(root, version, log);
        ExtensionVersionHelper.SetAssembliesVersion(root, version, log);
        ExtensionVersionHelper.SetVersionJson(root, version, log);
        ExtensionVersionHelper.SetChangeLog(root, version, changelogMessage, log);

        return log.ToResult(previousVersion, version);
    }

    public ExtensionVersionResult UpgradeVersion(string extensionRoot, ExtensionSemanticVersion semanticVersion, string? changelogMessage = null)
    {
        var log = new ExtensionVersionLog();
        if (!ExtensionVersionPaths.TryNormalizeRoot(extensionRoot, log, out var root))
        {
            return log.ToResult(null, null);
        }

        var currentVersion = ExtensionVersionHelper.ReadVersion(root, log);
        if (currentVersion == null)
        {
            log.Error("Kann aktuelle Version nicht lesen (version.json fehlt oder ist ungueltig).");
            return log.ToResult(null, null);
        }

        var newVersion = ExtensionVersionHelper.CalculateNextVersion(currentVersion, semanticVersion);

        ExtensionVersionHelper.SetInstallScriptVersion(root, newVersion, log);
        ExtensionVersionHelper.SetAssembliesVersion(root, newVersion, log);
        ExtensionVersionHelper.SetVersionJson(root, newVersion, log);
        ExtensionVersionHelper.SetChangeLog(root, newVersion, changelogMessage, log);

        return log.ToResult(currentVersion, newVersion);
    }

    public Version? ReadVersion(string extensionRoot)
    {
        var log = new ExtensionVersionLog();
        if (!ExtensionVersionPaths.TryNormalizeRoot(extensionRoot, log, out var root))
        {
            return null;
        }

        return ExtensionVersionHelper.ReadVersion(root, log);
    }

}

internal sealed class ExtensionVersionLog
{
    private readonly List<string> _messages = new();
    private readonly List<string> _warnings = new();
    private readonly List<string> _errors = new();

    public IReadOnlyList<string> Messages => _messages;
    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<string> Errors => _errors;

    public void Info(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _messages.Add(message);
        }
    }

    public void Warning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _warnings.Add(message);
        }
    }

    public void Error(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _errors.Add(message);
        }
    }

    public ExtensionVersionResult ToResult(Version? previous, Version? current)
    {
        return new ExtensionVersionResult(_errors.Count == 0, previous, current, _messages, _warnings, _errors);
    }
}

internal static class ExtensionVersionPaths
{
    public static bool TryNormalizeRoot(string extensionRoot, ExtensionVersionLog log, out string root)
    {
        root = string.Empty;
        if (string.IsNullOrWhiteSpace(extensionRoot))
        {
            log.Error("Kein Extension-Ordner angegeben.");
            return false;
        }

        var fullPath = Path.GetFullPath(extensionRoot.Trim());
        if (!Directory.Exists(fullPath))
        {
            log.Error($"Extension-Ordner nicht gefunden: {fullPath}");
            return false;
        }

        root = fullPath;
        return true;
    }
}

internal static class ExtensionVersionHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static Version CalculateNextVersion(Version current, ExtensionSemanticVersion semanticVersion)
    {
        return semanticVersion switch
        {
            ExtensionSemanticVersion.Major or ExtensionSemanticVersion.Shop => new Version(current.Major + 1, 0, 0),
            ExtensionSemanticVersion.Build or ExtensionSemanticVersion.Bug => new Version(current.Major, current.Minor, SafeBuild(current) + 1),
            _ => new Version(current.Major, current.Minor + 1, 0)
        };
    }

    public static void SetInstallScriptVersion(string rootPath, Version version, ExtensionVersionLog log)
    {
        try
        {
            var installProjectPath = Directory.GetDirectories(rootPath, "*.Install").FirstOrDefault();
            if (installProjectPath == null)
            {
                log.Warning("Install-Projekt (*.Install) nicht gefunden.");
                return;
            }

            var installExtensionFile = Directory
                .GetFiles(installProjectPath, "InstallExtension.sql", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (installExtensionFile == null)
            {
                log.Warning("InstallExtension.sql nicht gefunden.");
                return;
            }

            var installFileContent = File.ReadAllText(installExtensionFile);
            var regex = new Regex(@"(?<=\[?UPDATE\]?.*?SET.*?\[?Version\]?\s*=\s*')[\.\d]*?(?=')",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            installFileContent = regex.Replace(installFileContent, version.ToString());
            File.WriteAllText(installExtensionFile, installFileContent);
            log.Info($"InstallExtension.sql gesetzt auf Version {version}.");
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Aktualisieren von InstallExtension.sql: {ex.Message}");
        }
    }

    public static void SetAssembliesVersion(string rootPath, Version version, ExtensionVersionLog log)
    {
        try
        {
            var projectFiles = Directory
                .EnumerateFiles(rootPath, "*.csproj", CommonOptions.FileSearchOptions())
                .ToList();

            if (projectFiles.Count == 0)
            {
                log.Warning("Keine csproj-Dateien gefunden.");
                return;
            }

            foreach (var projectFilePath in projectFiles)
            {
                if (IsNewDotNetVersion(projectFilePath, log))
                {
                    SetCurrentDotNetVersion(version, projectFilePath, log);
                    ExtensionProjectFileHelper.EnableNullableReferenceTypes(projectFilePath, log);
                    ExtensionProjectFileHelper.RemoveOldTags(projectFilePath, log);
                    ExtensionProjectFileHelper.UpdateCopyright(projectFilePath, log);
                    ExtensionProjectFileHelper.SetShopProjectWebSdk(projectFilePath, log);
                    ExtensionProjectFileHelper.RemoveExtensionConfigPreserve(projectFilePath, log);
                }
                else
                {
                    SetOldDotNetVersion(version, projectFilePath, log);
                }

                log.Info($"{Path.GetFileName(projectFilePath)} gesetzt auf Version {version}.");
            }
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Setzen der Assembly-Versionen: {ex.Message}");
        }
    }

    public static void SetVersionJson(string rootPath, Version version, ExtensionVersionLog log)
    {
        try
        {
            var versionJsonPath = Path.Combine(rootPath, "version.json");
            var payload = new ExtensionVersionJson { Version = version.ToString() };
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            File.WriteAllText(versionJsonPath, json);
            log.Info($"version.json gesetzt auf Version {version}.");
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Schreiben von version.json: {ex.Message}");
        }
    }

    public static Version? ReadVersion(string rootPath, ExtensionVersionLog log)
    {
        try
        {
            var versionJsonPath = Path.Combine(rootPath, "version.json");
            if (!File.Exists(versionJsonPath))
            {
                log.Warning("version.json ist nicht vorhanden.");
                return null;
            }

            var versionJsonContent = JsonSerializer.Deserialize<ExtensionVersionJson>(File.ReadAllText(versionJsonPath), JsonOptions);
            if (Version.TryParse(versionJsonContent?.Version, out var version))
            {
                return version;
            }

            log.Error("version.json konnte nicht gelesen werden.");
            return null;
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Lesen von version.json: {ex.Message}");
            return null;
        }
    }

    public static void SetChangeLog(string rootPath, Version version, string? changelogMessage, ExtensionVersionLog log)
    {
        try
        {
            var changelogJsonPath = Path.Combine(rootPath, "changelog.json");
            if (string.IsNullOrWhiteSpace(changelogMessage))
            {
                log.Info("changelog.json übersprungen (keine Nachricht).");
                return;
            }

            if (!File.Exists(changelogJsonPath))
            {
                var created = new Dictionary<string, string[]>
                {
                    [version.ToString()] = new[] { changelogMessage }
                };
                File.WriteAllText(changelogJsonPath, JsonSerializer.Serialize(created, JsonOptions));
                log.Info("changelog.json erstellt.");
                return;
            }

            var versionJsonContent = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(changelogJsonPath), JsonOptions)
                                     ?? new Dictionary<string, string[]>();

            var existingVersions = versionJsonContent
                .Keys
                .Select(key => Version.TryParse(key, out var parsed) ? parsed : null)
                .Where(v => v != null)
                .Select(v => v!)
                .ToList();

            var higherVersionsDefined = existingVersions.Any(existingVersion => existingVersion > version);
            if (versionJsonContent.TryGetValue(version.ToString(), out var value))
            {
                log.Warning($"Alter Changelog-Eintrag wurde ueberschrieben: {string.Join(", ", value)}");
            }
            else
            {
                log.Info("changelog.json erweitert.");
            }

            versionJsonContent[version.ToString()] = new[] { changelogMessage };
            if (higherVersionsDefined)
            {
                log.Warning("Changelog enthält höhere Versionen als die aktuelle.");
            }

            File.WriteAllText(changelogJsonPath, JsonSerializer.Serialize(versionJsonContent, JsonOptions));
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Schreiben von changelog.json: {ex.Message}");
        }
    }

    private static bool IsNewDotNetVersion(string filePath, ExtensionVersionLog log)
    {
        var xmlFile = XDocument.Load(filePath);
        var ns = xmlFile.Root?.Name.Namespace ?? XNamespace.None;
        var propertyGroups = xmlFile.Root?.Elements(ns + "PropertyGroup").ToList();
        if (propertyGroups == null || propertyGroups.Count == 0)
        {
            log.Warning($"ProjectFile hat keinen PropertyGroup: {filePath}");
            return false;
        }

        var targetFrameworkVersion = propertyGroups.Elements(ns + "TargetFrameworkVersion").ToList();
        return targetFrameworkVersion.Count == 0;
    }

    private static void SetOldDotNetVersion(Version version, string filePath, ExtensionVersionLog log)
    {
        try
        {
            var projectDirectory = Directory.GetParent(filePath);
            if (projectDirectory == null)
            {
                return;
            }

            var assemblyInfoFilePath = Directory
                .EnumerateFiles(projectDirectory.FullName, "AssemblyInfo.cs", CommonOptions.FileSearchOptions(2))
                .FirstOrDefault();
            if (string.IsNullOrEmpty(assemblyInfoFilePath))
            {
                return;
            }

            var assemblyInfoFileContent = File.ReadAllText(assemblyInfoFilePath);

            var regexAssemblyVersion = new Regex("""(?<=\[assembly: AssemblyVersion\(")[\.\d]*?(?="\)\])""",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            assemblyInfoFileContent = regexAssemblyVersion.Replace(assemblyInfoFileContent, version.ToString());

            var regexFileVersion = new Regex("""(?<=\[assembly: AssemblyFileVersion\(")[\.\d]*?(?="\)\])""",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            assemblyInfoFileContent = regexFileVersion.Replace(assemblyInfoFileContent, version.ToString());
            File.WriteAllText(assemblyInfoFilePath, assemblyInfoFileContent);
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Setzen der AssemblyInfo-Version: {ex.Message}");
        }
    }

    private static void SetCurrentDotNetVersion(Version version, string filePath, ExtensionVersionLog log)
    {
        try
        {
            var xmlFile = XDocument.Load(filePath);
            if (xmlFile.Root == null)
            {
                log.Warning($"ProjectFile hat keine Root: {filePath}");
                return;
            }

            var ns = xmlFile.Root.Name.Namespace;
            var propertyGroup = xmlFile.Root.Elements(ns + "PropertyGroup").FirstOrDefault();
            if (propertyGroup == null)
            {
                log.Warning($"ProjectFile hat keinen PropertyGroup: {filePath}");
                return;
            }

            SetOrAdd(propertyGroup, ns, "AssemblyVersion", version.ToString());
            SetOrAdd(propertyGroup, ns, "FileVersion", version.ToString());
            SetOrAdd(propertyGroup, ns, "PackageVersion", version.ToString());

            xmlFile.Save(filePath);
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Setzen der csproj-Version: {ex.Message}");
        }
    }

    private static void SetOrAdd(XElement propertyGroup, XNamespace ns, string name, string value)
    {
        var element = propertyGroup.Element(ns + name);
        if (element == null)
        {
            propertyGroup.Add(new XElement(ns + name, value));
        }
        else
        {
            element.Value = value;
        }
    }

    private static int SafeBuild(Version version) => version.Build >= 0 ? version.Build : 0;
}

internal sealed class ExtensionVersionJson
{
    public string? Version { get; set; }
}

internal static class CommonOptions
{
    public static EnumerationOptions FileSearchOptions(int depth = 1)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = depth
        };

        return options;
    }
}

internal static class ExtensionProjectFileHelper
{
    public static void RemoveExtensionConfigPreserve(string filePath, ExtensionVersionLog log)
    {
        var projectFileName = new Regex(@"\.OrderService\.csproj");
        if (projectFileName.IsMatch(filePath))
        {
            return;
        }

        try
        {
            var xmlFile = XDocument.Load(filePath);
            var ns = xmlFile.Root?.Name.Namespace ?? XNamespace.None;
            var itemGroups = xmlFile.Root?.Elements(ns + "ItemGroup").ToList();
            if (itemGroups == null)
            {
                return;
            }

            foreach (var itemGroup in itemGroups)
            {
                var noneTags = itemGroup.Elements(ns + "None").ToList();

                var deletedExtensionConfigTag = false;
                foreach (var noneTag in noneTags)
                {
                    var updateAttribute = noneTag.Attribute("Update");
                    if (updateAttribute?.Value != "extension.config")
                    {
                        continue;
                    }

                    noneTag.Remove();
                    deletedExtensionConfigTag = true;
                    break;
                }

                if (deletedExtensionConfigTag && noneTags.Count == 1)
                {
                    itemGroup.Remove();
                }
            }

            xmlFile.Save(filePath);
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Entfernen von extension.config Preserve: {ex.Message}");
        }
    }

    public static void SetShopProjectWebSdk(string filePath, ExtensionVersionLog log)
    {
        var shopProjectName = new Regex(@"\.Shop\.csproj");
        if (!shopProjectName.IsMatch(filePath))
        {
            return;
        }

        try
        {
            var xmlFile = XDocument.Load(filePath);
            var projectTag = xmlFile.Root;
            if (projectTag == null || !string.Equals(projectTag.Name.LocalName, "Project", StringComparison.Ordinal))
            {
                return;
            }

            var sdkAttribute = projectTag.Attribute("Sdk");
            if (sdkAttribute == null || sdkAttribute.Value.Equals("Microsoft.NET.Sdk.Web"))
            {
                return;
            }

            sdkAttribute.Value = "Microsoft.NET.Sdk.Web";
            xmlFile.Save(filePath);
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Setzen des Web-SDKs: {ex.Message}");
        }
    }

    public static void EnableNullableReferenceTypes(string filePath, ExtensionVersionLog log)
    {
        try
        {
            var xmlFile = XDocument.Load(filePath);
            var ns = xmlFile.Root?.Name.Namespace ?? XNamespace.None;
            var propertyGroup = xmlFile.Root?.Elements(ns + "PropertyGroup").FirstOrDefault();
            if (propertyGroup == null)
            {
                log.Warning($"ProjectFile hat keinen PropertyGroup: {filePath}");
                return;
            }

            var nullableTag = propertyGroup.Element(ns + "Nullable");
            if (nullableTag == null)
            {
                propertyGroup.Add(new XElement(ns + "Nullable", "enable"));
            }
            else if (!nullableTag.Value.Equals("enable"))
            {
                nullableTag.Value = "enable";
            }

            xmlFile.Save(filePath);
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Setzen von Nullable: {ex.Message}");
        }
    }

    public static void UpdateCopyright(string filePath, ExtensionVersionLog log)
    {
        try
        {
            var xmlFile = XDocument.Load(filePath);
            var ns = xmlFile.Root?.Name.Namespace ?? XNamespace.None;
            var propertyGroups = xmlFile.Root?.Elements(ns + "PropertyGroup").ToList();
            if (propertyGroups == null || !propertyGroups.Any())
            {
                log.Warning($"ProjectFile hat keinen PropertyGroup: {filePath}");
                return;
            }

            foreach (var propertyGroup in propertyGroups)
            {
                var copyRightTag = propertyGroup.Element(ns + "Copyright");
                var regex = new Regex(@"\d{4}");
                if (copyRightTag != null)
                {
                    copyRightTag.Value = regex.Replace(copyRightTag.Value, DateTime.Now.Year.ToString());
                }
            }

            xmlFile.Save(filePath);
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Aktualisieren des Copyrights: {ex.Message}");
        }
    }

    public static void RemoveOldTags(string filePath, ExtensionVersionLog log)
    {
        try
        {
            var xmlFile = XDocument.Load(filePath);
            var ns = xmlFile.Root?.Name.Namespace ?? XNamespace.None;
            var propertyGroups = xmlFile.Root?.Elements(ns + "PropertyGroup").ToList();
            if (propertyGroups == null || !propertyGroups.Any())
            {
                log.Warning($"ProjectFile hat keinen PropertyGroup: {filePath}");
                return;
            }

            foreach (var propertyGroup in propertyGroups)
            {
                propertyGroup.RemoveTagIfExists("SccProjectName");
                propertyGroup.RemoveTagIfExists("SccLocalPath");
                propertyGroup.RemoveTagIfExists("SccAuxPath");
                propertyGroup.RemoveTagIfExists("SccProvider");
                propertyGroup.RemoveTagIfExists("GenerateAssemblyInfo");
            }

            xmlFile.Save(filePath);
        }
        catch (Exception ex)
        {
            log.Error($"Fehler beim Entfernen alter Tags: {ex.Message}");
        }
    }
}

internal static class ExtensionTagHelper
{
    public static void RemoveTagIfExists(this XElement element, string tagName)
    {
        var ns = element.Name.Namespace;
        var tagToRemove = element.Element(ns + tagName) ?? element.Element(tagName);
        tagToRemove?.Remove();
    }
}

