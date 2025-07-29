using Benjis_Shop_Toolbox.Services;
using ElectronApp.Components;
using MudBlazor.Services;
using ElectronNET.API;
using ElectronNET.API.Entities;
using App = ElectronApp.Components.App;
using WebHostBuilderExtensions = ElectronNET.API.WebHostBuilderExtensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddMudServices();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<SolutionOpener>();
builder.Services.AddScoped<FileDialogService>();
builder.Services.AddScoped<ThemeLinkService>();
builder.WebHost.UseElectron(args);

AppInfo.StartTime = DateTime.Now;

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


if (HybridSupport.IsElectronActive)
{
    Task.Run(async () =>
    {
        AppInfo.Window = await Electron.WindowManager.CreateWindowAsync(new ElectronNET.API.Entities.BrowserWindowOptions
        {
            Show = true,
            WebPreferences = new WebPreferences
            {
                NodeIntegration = false,
            },
            Icon = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "favicon.ico")
        });
        AppInfo.Window.LoadURL("http://localhost:8005");

        AppInfo.Window.OnClosed += () => Electron.App.Quit();
    });
}


app.Run();