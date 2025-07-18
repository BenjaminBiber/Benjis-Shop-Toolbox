using Microsoft.AspNetCore.Components.Web;
using BlazorDesktop.Hosting;
using Benjis_Shop_Toolbox.Components;
using MudBlazor.Services;

var builder = BlazorDesktopHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();

builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

if (builder.HostEnvironment.IsDevelopment())
{
    builder.UseDeveloperTools();
}

await builder.Build().RunAsync();