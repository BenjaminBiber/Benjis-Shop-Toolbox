using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var branch = ParseBranchName(args);
        using var ctx = new TrayAppContext(branch);

        var parentPid = ParseParentPid(args);
        if (parentPid > 0)
        {
            try
            {
                var parent = Process.GetProcessById(parentPid);
                _ = Task.Run(() =>
                {
                    try { parent.WaitForExit(); } catch { }
                    try { ctx.Shutdown(); } catch { }
                });
            }
            catch { }
        }

        Application.Run(ctx);
    }

    private static int ParseParentPid(string[] args)
    {
        try
        {
            foreach (var a in args ?? Array.Empty<string>())
            {
                var s = a.Trim();
                if (s.StartsWith("--parent-pid=", StringComparison.OrdinalIgnoreCase) ||
                    s.StartsWith("--ppid=", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = s.Split('=');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var pid)) return pid;
                }
            }
        }
        catch { }
        return 0;
    }

    private static string? ParseBranchName(string[] args)
    {
        try
        {
            foreach (var a in args ?? Array.Empty<string>())
            {
                var s = a.Trim();
                if (s.StartsWith("--branch-db=", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = s.Split('=');
                    if (parts.Length == 2) return parts[1];
                }
            }
        }
        catch { }
        return null;
    }
}

