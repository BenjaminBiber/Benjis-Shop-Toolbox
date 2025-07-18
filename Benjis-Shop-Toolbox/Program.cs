using Microsoft.AspNetCore.Components.Web;
using BlazorDesktop.Hosting;
using Benjis_Shop_Toolbox.Components;
using MudBlazor.Services;
using Benjis_Shop_Toolbox.Services;

var builder = BlazorDesktopHostBuilder.CreateDefault(args);

AppInfo.StartTime = DateTime.Now;

builder.Services.AddMudServices();
builder.Services.AddSingleton<SettingsService>();

builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

if (builder.HostEnvironment.IsDevelopment())
{
    builder.UseDeveloperTools();
}

await builder.Build().RunAsync();