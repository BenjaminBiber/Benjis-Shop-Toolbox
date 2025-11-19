using System.Diagnostics;
using System.Text;
using Toolbox.Data.Models;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Data.Services;

public static class SqlCmdService
{
    public static async Task<(int ExitCode, string StdOut, string StdError)> RunAsync(
        SqlCmdRequest req,
        IProgress<string>? outputProgress = null,
        IProgress<string>? errorProgress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.ScriptPath))
        {
            return new ValueTuple<int, string, string>(-1, null, "ScriptPath darf nicht leer sein.");
        }
        if (!req.UseIntegratedSecurity && string.IsNullOrEmpty(req.Username))
        {
            return new ValueTuple<int, string, string>(-1, null, "Username erforderlich, wenn UseIntegratedSecurity=false.");
        }

        string args = BuildArguments(req);

        var psi = new ProcessStartInfo
        {
            FileName = req.SqlCmdPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi };

        try
        {
            if (!p.Start())
            {
                return new ValueTuple<int, string, string>(-1, null, "sqlcmd-Prozess konnte nicht gestartet werden.");
            }

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            void OnOutputDataReceived(object? _, DataReceivedEventArgs e)
            {
                if (e.Data is null) return;
                lock (stdoutBuilder)
                {
                    stdoutBuilder.AppendLine(e.Data);
                }
                outputProgress?.Report(e.Data + Environment.NewLine);
            }

            void OnErrorDataReceived(object? _, DataReceivedEventArgs e)
            {
                if (e.Data is null) return;
                lock (stderrBuilder)
                {
                    stderrBuilder.AppendLine(e.Data);
                }
                errorProgress?.Report(e.Data + Environment.NewLine);
            }

            p.OutputDataReceived += OnOutputDataReceived;
            p.ErrorDataReceived += OnErrorDataReceived;

            try
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                using (ct.Register(() =>
                       {
                           try
                           {
                               if (!p.HasExited)
                                   p.Kill();
                           }
                           catch
                           {
                               // ignore
                           }
                       }))
                {
                    await p.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                p.OutputDataReceived -= OnOutputDataReceived;
                p.ErrorDataReceived -= OnErrorDataReceived;
            }

            var stdout = stdoutBuilder.ToString();
            var stderr = stderrBuilder.ToString();

            return (p.ExitCode, stdout, stderr);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception)
        {
            return new ValueTuple<int, string, string>(-1, null, ex.Message);
        }
    }

    private static string BuildArguments(SqlCmdRequest r)
    {
        var sb = new StringBuilder();

        sb.Append($"-S {Q(r.Server)} ");
        if (!string.IsNullOrWhiteSpace(r.Database))
            sb.Append($"-d {Q(r.Database!)} ");

        if (r.UseIntegratedSecurity)
        {
            sb.Append("-E ");
        }
        else
        {
            sb.Append($"-U {Q(r.Username!)} ");
            if (!string.IsNullOrEmpty(r.Password))
                sb.Append($"-P {Q(r.Password!)} ");
        }

        if (r.AbortOnError) sb.Append("-b ");
        if (r.QuotedIdentifierOn) sb.Append("-I ");
        if (r.TrustServerCertificate) sb.Append("-C ");
        if (r.LoginTimeoutSeconds is { } l) sb.Append($"-l {l} ");
        if (r.QueryTimeoutSeconds is { } t) sb.Append($"-t {t} ");

        sb.Append($"-i {Q(r.ScriptPath)} ");

        if (r.Variables is { Count: > 0 })
        {
            foreach (var (key, val) in r.Variables)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                sb.Append($"-v {key}={Q(val ?? string.Empty)} ");
            }
        }

        if (!string.IsNullOrWhiteSpace(r.AdditionalArgs))
            sb.Append(r.AdditionalArgs).Append(' ');

        return sb.ToString().TrimEnd();
    }

    private static string Q(string s)
    {
        if (s is null) return "\"\"";
        var escaped = s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
    
    public static string ResolveSqlCmdPath(string preferred = "sqlcmd")
    {
        if (IsInPath(preferred))
        {
            return preferred;
        }

        var candidates = new[]
        {
            @"C:\Program Files\Microsoft SQL Server\sqlcmd\sqlcmd.exe",
            @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe",
            @"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe",
            @"C:\Program Files\Microsoft SQL Server\110\Tools\Binn\sqlcmd.exe",
            @"C:\Program Files\Microsoft SQL Server\120\Tools\Binn\sqlcmd.exe",
            @"C:\Program Files\Microsoft SQL Server\130\Tools\Binn\sqlcmd.exe",
            @"C:\Program Files\Microsoft SQL Server\140\Tools\Binn\sqlcmd.exe",
            @"C:\Program Files (x86)\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd.exe",
            @"C:\Program Files (x86)\Microsoft SQL Server\130\Tools\Binn\sqlcmd.exe",
            @"C:\Program Files\SqlCmd\sqlcmd.exe",
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null) return found;

        return null;
    }

    public static bool IsInPath(string exe)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c where {exe}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 && output.Split(new[] { '\r','\n' }, StringSplitOptions.RemoveEmptyEntries).Any(File.Exists);
        }
        catch { return false; }
    }
}
