using System;
using System.Linq;

namespace Toolbox.Data.Services;

public interface IVersionService
{
    string GetCurrentVersion();
    bool IsNewer(string candidate, string baseline);
}

public sealed class VersionService : IVersionService
{
    public string GetCurrentVersion()
    {
        var current = GetInformationalVersion()
                      ?? (typeof(VersionService).Assembly.GetName().Version?.ToString() ?? "0.0.0");
        return Versioning.NormalizeVersionText(current);
    }

    public bool IsNewer(string candidate, string baseline)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(baseline))
        {
            return false;
        }

        var c = Versioning.NormalizeVersionText(candidate);
        var b = Versioning.NormalizeVersionText(baseline);
        return Versioning.CompareVersions(c, b) > 0;
    }

    private static string? GetInformationalVersion()
    {
        try
        {
            var asm = typeof(VersionService).Assembly;
            var attr = asm
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault();
            var v = attr?.InformationalVersion ?? asm.GetName().Version?.ToString();
            if (!string.IsNullOrEmpty(v))
            {
                var idx = v.IndexOf('+');
                if (idx > 0) v = v.Substring(0, idx);
            }
            return v;
        }
        catch
        {
            return null;
        }
    }
}

