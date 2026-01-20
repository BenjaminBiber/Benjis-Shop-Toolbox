using Microsoft.AspNetCore.Components.Web;
using BlazorDesktop.Hosting;
using Toolbox.Components;
using System.Diagnostics;
using MudBlazor.Services;
using System.IO;
using System.Net.Mime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Toolbox.Services;
using Toolbox.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.Administration;
using Toolbox.Data.DataContexts;
using Toolbox.Data.Models;
using Toolbox.Data.Models.Interfaces;
using Application = System.Windows.Application;
using INotificationService = Toolbox.Data.Models.Interfaces.INotificationService;
using MessageBox = System.Windows.Forms.MessageBox;

var builder = BlazorDesktopHostBuilder.CreateDefault(args);

builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

if (builder.HostEnvironment.IsDevelopment())
{
    builder.UseDeveloperTools();
}

await WaitForDebuggerIfRequestedAsync(args, "Toolbox");

// Services & DI
builder.Services.AddMudServices();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<SolutionOpener>();
builder.Services.AddScoped<FileDialogService>();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddScoped<GitRepoService>();

//Initialize Internal DB Context (use branch-specific DB in Development)
var appData  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var dataRoot = Path.Combine(appData, "BenjisToolbox");
Directory.CreateDirectory(dataRoot);

string? branchName = null;
if (builder.HostEnvironment.IsDevelopment())
{
    branchName = TryGetGitBranchSafe(AppContext.BaseDirectory);
}

var dbFile   = string.IsNullOrWhiteSpace(branchName) ? "toolbox.db" : $"toolbox_{branchName}.db";
var dbPath   = Path.Combine(dataRoot, dbFile);
var connStr  = $"Data Source={dbPath};Cache=Shared";
builder.Services.AddDbContext<InternalAppDbContext>(options => options.UseSqlite(connStr));

//Initialize External DB Context
builder.Services.AddSingleton<IConnectionStringResolver, ConnectionStringResolver>();
builder.Services.AddScoped<IExternalDbContextFactory, ExternalDbContextFactory>();

builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<UiThemeState>();
builder.Services.AddScoped<ThemeLinkService>();
builder.Services.AddScoped<ExtensionsService>();
builder.Services.AddScoped<ShopSystemConfigService>();
builder.Services.AddSingleton<UpdaterService>();
builder.Services.AddScoped<AppInfoService>();
builder.Services.AddScoped<IAppInfoService, AppInfoService>();
builder.Services.AddScoped<IisService>();
builder.Services.AddScoped<DatabaseConnectionService>();
builder.Services.AddScoped<SqlBuilder>();
builder.Services.AddScoped<EasterEggService>();
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InternalAppDbContext>();
    
    await db.Database.OpenConnectionAsync();
    try
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }

    await db.Database.MigrateAsync();

    var appInfoService = scope.ServiceProvider.GetRequiredService<AppInfoService>();
    await appInfoService.SetStartTimeAsync(DateTime.Now);
    
    var settingService = scope.ServiceProvider.GetRequiredService<SettingsService>();
    var manager = new ServerManager();
    if (!settingService.AreThereAllShopPathSettings(manager.Sites))
    {
        settingService.FillShopPathSettings(manager.Sites);
    }
    var databaseConnectionService = scope.ServiceProvider.GetRequiredService<DatabaseConnectionService>();
    databaseConnectionService.FillDataBaseConnections();
}

try { app.Services.GetService<UpdaterService>()?.LaunchInBackgroundAsync(); } catch { }

TryStartTrayIconProcess(branchName);

await app.RunAsync();


static async Task WaitForDebuggerIfRequestedAsync(string[] args, string appName)
{
    try
    {
        var env = Environment.GetEnvironmentVariable("TOOLBOX_WAIT_FOR_DEBUGGER");
        var requested = (env == "1" || env?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
                        || args.Any(a => string.Equals(a, "--wait-for-debugger", StringComparison.OrdinalIgnoreCase));

        if (requested && !Debugger.IsAttached)
        {
            var pid = Environment.ProcessId;
            Console.WriteLine($"[{appName}] Warte auf Debugger… PID={pid}");
            Console.WriteLine($"[{appName}] Zum Überspringen env TOOLBOX_WAIT_FOR_DEBUGGER=0 setzen.");

            var last = DateTime.UtcNow;
            while (!Debugger.IsAttached)
            {
                await Task.Delay(250);
                if ((DateTime.UtcNow - last) > TimeSpan.FromSeconds(5))
                {
                    Console.WriteLine($"[{appName}] …warte weiterhin auf Debugger…");
                    last = DateTime.UtcNow;
                }
            }

            Console.WriteLine($"[{appName}] Debugger angehängt.");
        }
    }
    catch
    {
        // still continue startup
    }
}

static void TryStartTrayIconProcess(string? branchName)
{
    try
    {
        var exeName = "Toolbox.TrayIcon.exe";
        var path = Path.Combine(AppContext.BaseDirectory, exeName);
        if (!File.Exists(path))
        {
            return;
        }

        var procName = Path.GetFileNameWithoutExtension(exeName);
        var already = Process.GetProcessesByName(procName).Any();
        if (already) return;

        var ppid = Environment.ProcessId;
        var args = $"--parent-pid={ppid}";
        if (!string.IsNullOrWhiteSpace(branchName))
        {
            args += $" --branch-db={branchName}";
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = false
        });
    }
    catch
    {
        // ignore
    }
}

static string? TryGetGitBranchSafe(string startDir)
{
    try
    {
        var dir = new DirectoryInfo(startDir);
        for (var d = dir; d != null; d = d.Parent)
        {
            var gitDir = Path.Combine(d.FullName, ".git");
            if (Directory.Exists(gitDir))
            {
                var headFile = Path.Combine(gitDir, "HEAD");
                if (File.Exists(headFile))
                {
                    var head = File.ReadAllText(headFile).Trim();
                    // HEAD may be like: ref: refs/heads/feature/xyz
                    var branch = head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase)
                        ? head.Split('/').Last()
                        : "detached";
                    return SanitizeBranchName(branch);
                }
            }
        }
    }
    catch { }
    return null;
}

static string SanitizeBranchName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    var sanitized = string.Join('_', name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    sanitized = sanitized.Replace(' ', '_');
    return sanitized;
}
