using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Toolbox.Services;

public class UpdaterService
{
    public Task LaunchInBackgroundAsync()
    {
        if (!OperatingSystem.IsWindows()) return Task.CompletedTask;
        _ = Task.Run(() => TryLaunchUpdaterInternal());
        return Task.CompletedTask;
    }

    public Task<bool> TryLaunchUpdaterAsync()
        => Task.Run(TryLaunchUpdaterInternal);

    private bool TryLaunchUpdaterInternal()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "Toolbox.Updater.exe"),
                Path.Combine(Directory.GetCurrentDirectory(), "Toolbox.Updater.exe")
            };
            var updater = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(updater)) return false;

            var current = GetInformationalVersion()
                          ?? (typeof(UpdaterService).Assembly.GetName().Version?.ToString() ?? "0.0.0");
            var args = $"--pid {Environment.ProcessId} --process-name \"Toolbox\" --current-version {current}";
#if DEBUG
            args += " --TOOLBOX_UPDATER_DEBUG=1";
#endif
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = updater,
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = baseDir,
                Verb = "runas"
            };
            try
            {
                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shop-Toolbox", "Logs");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "DesktopApp.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to launch updater: {ex}\n");
                }
                catch { }
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static string? GetInformationalVersion()
    {
        try
        {
            var asm = typeof(UpdaterService).Assembly;
            var attr = asm
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault();
            var v = attr?.InformationalVersion ?? asm.GetName().Version?.ToString();
            if (!string.IsNullOrEmpty(v))
            {
                var idx = v.IndexOfAny(new[] { '+', '-' });
                if (idx > 0) v = v.Substring(0, idx);
            }
            return v;
        }
        catch { return null; }
    }
}

