using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Toolbox.Data.Models.Interfaces;
using Toolbox.DataContexts;
using Toolbox.Services;

sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Control _invoker = new();
    private readonly InternalAppDbContext _db;
    private readonly IisService _iisService;
    private readonly ISettingsService _settingsService;

    public TrayAppContext()
    {
        _invoker.CreateControl();
        try
        {
            var appData  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dataRoot = Path.Combine(appData, "BenjisToolbox");
            Directory.CreateDirectory(dataRoot);
            var dbPath   = Path.Combine(dataRoot, "toolbox.db");
            var connStr  = $"Data Source={dbPath};Cache=Shared";

            var options = new DbContextOptionsBuilder<InternalAppDbContext>()
                .UseSqlite(connStr)
                .Options;
            _db = new InternalAppDbContext(options);

            try
            {
                var conn = _db.Database.GetDbConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA foreign_keys=ON;";
                cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch { }
        }
        catch { }

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Starten (IIS)", null, (_, _) => SafeInvoke(_iisService.StartTrayIconSite));
        _menu.Items.Add("Stoppen (IIS)", null, (_, _) => SafeInvoke(_iisService.StopTrayIconSite));
        _menu.Items.Add("Neustarten (IIS)", null, async (_, _) => await SafeInvokeAsync(_iisService.RestartTrayIconSite));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Beenden", null, (_, _) => Shutdown());

        _trayIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Benjis Toolbox",
            Visible = true,
            ContextMenuStrip = _menu
        };

        var notifications = new TrayNotificationService(_trayIcon, _invoker);
        _settingsService = new TraySettingsService(_db);
        var appInfoService = new TrayAppInfoService(_db);
        _iisService = new IisService(notifications, _settingsService, appInfoService);

        _trayIcon.MouseUp += (s, e) =>
        {
            if (e.Button is MouseButtons.Right or MouseButtons.Left)
            {
                var pos = Cursor.Position;
                var preferred = _menu.GetPreferredSize(System.Drawing.Size.Empty);
                var wa = Screen.FromPoint(pos).WorkingArea;
                var y = Math.Max(wa.Top, pos.Y - preferred.Height - 6);
                var x = Math.Min(Math.Max(wa.Left, pos.X - preferred.Width / 2), wa.Right - preferred.Width);
                _menu.Show(new System.Drawing.Point(x, y));
            }
        };
    }

    private static Icon LoadIcon()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.Combine(baseDir, "favicon.ico");
            if (File.Exists(candidate)) return new Icon(candidate);
        }
        catch { }
        return SystemIcons.Application;
    }

    private static string? FindToolboxExe()
    {
        try
        {
            var local = Path.Combine(AppContext.BaseDirectory, "Toolbox.exe");
            if (File.Exists(local)) return local;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var d = dir; d != null; d = d.Parent)
            {
                var debug = Path.Combine(d.FullName, "Toolbox", "bin", "Debug", "net9.0-windows10.0.22000.0", "Toolbox.exe");
                if (File.Exists(debug)) return debug;
                var release = Path.Combine(d.FullName, "Toolbox", "bin", "Release", "net9.0-windows10.0.22000.0", "Toolbox.exe");
                if (File.Exists(release)) return release;
            }
        }
        catch { }
        return null;
    }

    public void Shutdown()
    {
        try
        {
            if (_invoker.IsHandleCreated)
            {
                _invoker.BeginInvoke(new Action(() =>
                {
                    try { _trayIcon.Visible = false; } catch { }
                    ExitThread();
                }));
            }
            else
            {
                try { _trayIcon.Visible = false; } catch { }
                ExitThread();
            }
        }
        catch { }
    }

    protected override void ExitThreadCore()
    {
        try { _trayIcon.Dispose(); } catch { }
        try { _menu.Dispose(); } catch { }
        try { _db?.Dispose(); } catch { }
        base.ExitThreadCore();
    }

    private void SafeInvoke(Action action)
    {
        try { action(); }
        catch (Exception ex) { try { _trayIcon.ShowBalloonTip(3000, "Fehler", ex.Message, ToolTipIcon.Error); } catch { } }
    }

    private async Task SafeInvokeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { try { _trayIcon.ShowBalloonTip(3000, "Fehler", ex.Message, ToolTipIcon.Error); } catch { } }
    }
}

