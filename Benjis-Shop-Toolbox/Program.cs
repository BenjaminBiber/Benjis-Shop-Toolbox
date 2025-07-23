using Benjis_Shop_Toolbox.Components;
using Benjis_Shop_Toolbox.Services;
using ElectronNET.API;
using MudBlazor.Services;
using App = Benjis_Shop_Toolbox.Components.App;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.WebHost.UseElectron(args);

AppInfo.StartTime = DateTime.Now;

builder.Services.AddMudServices();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<FileDialogService>();
builder.Services.AddScoped<ThemeLinkService>();

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
        var window = await Electron.WindowManager.CreateWindowAsync(new ElectronNET.API.Entities.BrowserWindowOptions
        {
            Show = true
        });

        window.OnClosed += () => Electron.App.Quit();
    });
}

app.Run();
