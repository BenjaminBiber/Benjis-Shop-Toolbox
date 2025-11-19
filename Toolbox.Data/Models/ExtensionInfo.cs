using Toolbox.Data.Models.Interfaces;
using Toolbox.Data.Models.ShopYaml;
using Toolbox.Data.Services;

namespace Toolbox.Data.Models;

public class ExtensionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool HasSolution { get; set; }
    public bool HasProjects { get; set; }
    public bool HasShopProject { get; set; }
    public bool HasInstallProject { get; set; }
    public bool HasDataProject { get; set; }
    public bool HasThemeV4 { get; set; }

    public string GetCustomerName()
    {
        var parts = Name.Split(".");
        if (parts.Length > 2)
        {
            return parts[parts.Length - 2];
        }

        if (parts.Length > 1 && parts.Length < 3)
        {
            return parts[parts.Length - 1];
        }
        return parts.First();
    }

    public async Task<bool> InstallAsync(DatabaseConnection connection, INotificationService notificationService)
    {
        string? mainSqlFile = Directory
            .EnumerateFiles(Path, "InstallExtension.sql", SearchOption.AllDirectories) 
            .FirstOrDefault();
        
        string? databaseFolder = Directory
            .EnumerateDirectories(Path, "Database", SearchOption.AllDirectories) 
            .FirstOrDefault();
        
        if (!HasInstallProject || mainSqlFile == null || databaseFolder == null || String.IsNullOrEmpty(Path))
        {
            return false;
        }
        
        var req = new SqlCmdRequest
        {
            Server = connection.Server,
            Database = connection.Database,
            UseIntegratedSecurity = false,
            ScriptPath = mainSqlFile,
            Variables = new Dictionary<string,string>
            {
                ["path"] = databaseFolder,
                ["shopId"] = "1",
                ["createdById"] = "1" 
            },
            Username = connection.User,
            Password = connection.Password,
            TrustServerCertificate = connection.TrustServerCertificate, 
            CodePage = 65001,              
            QueryTimeoutSeconds = 120,
            SqlCmdPath = SqlCmdService.ResolveSqlCmdPath()
        };

        var result = await SqlCmdService.RunAsync(req);
        if (result.ExitCode != 0)
        {
            notificationService.Error(result.StdError);
            return false;
        }
        else
        {
            notificationService.Success(result.StdOut);
            return true;
        }
    }

    public async Task<(bool Ok, string Log)> InstallWithLogAsync(
        DatabaseConnection connection,
        IProgress<string>? outputProgress = null,
        IProgress<string>? errorProgress = null)
    {
        string? mainSqlFile = Directory
            .EnumerateFiles(Path, "InstallExtension.sql", SearchOption.AllDirectories)
            .FirstOrDefault();

        string? databaseFolder = Directory
            .EnumerateDirectories(Path, "Database", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (!HasInstallProject || mainSqlFile == null || databaseFolder == null || String.IsNullOrEmpty(Path))
        {
            return (false, "Installationsskript oder Database-Ordner nicht gefunden.");
        }

        var req = new SqlCmdRequest
        {
            Server = connection.Server,
            Database = connection.Database,
            UseIntegratedSecurity = false,
            ScriptPath = mainSqlFile,
            Variables = new Dictionary<string, string>
            {
                ["path"] = databaseFolder,
                ["shopId"] = "1",
                ["createdById"] = "1"
            },
            Username = connection.User,
            Password = connection.Password,
            TrustServerCertificate = connection.TrustServerCertificate,
            CodePage = 65001,
            QueryTimeoutSeconds = 120,
            SqlCmdPath = SqlCmdService.ResolveSqlCmdPath()
        };

        var result = await SqlCmdService.RunAsync(req, outputProgress, errorProgress);
        var combined = string.Join(Environment.NewLine, new[] { result.StdOut, result.StdError }.Where(s => !string.IsNullOrEmpty(s)));
        return (result.ExitCode == 0, combined ?? string.Empty);
    }
}

