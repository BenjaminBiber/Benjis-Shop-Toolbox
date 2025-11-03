using Microsoft.Web.Administration;

namespace Toolbox.Data.Models.Extensions;

public static class BindingExtensions
{
    public static string? GetSiteUrl(this Binding binding)
    {
        var protocol = binding.Protocol;

        var parts = binding.BindingInformation.Split(':');

        if (parts.Length != 3)
        {
            return null;
        }

        string ip = parts[0];
        string port = parts[1];
        string host = binding.Host;

        string domain = !string.IsNullOrEmpty(host)
            ? host
            : (ip == "*" || ip == "0.0.0.0" ? "localhost" : ip);

        bool isDefaultPort = (protocol == "http" && port == "80") || (protocol == "https" && port == "443");

        string url = $"{protocol}://{domain}";

        if (!isDefaultPort)
            url += $":{port}";

        return url;
    }

    public static void OpenInBrowser(this Binding binding)
    {
        var url = binding.GetSiteUrl();
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Öffnen: {ex.Message}");
        }
    }
}