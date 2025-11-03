using System;
using System.Drawing;
using System.Windows.Forms;

// Einfache Toast-Form als Fallback für Benachrichtigungen
sealed class ToastForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;

    private ToastForm(string title, string message, ToolTipIcon icon)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 32, 32);
        Opacity = 0.95;

        var padding = 12;
        var maxWidth = 360;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 32, 32)
        };

        var lbl = new Label
        {
            AutoSize = false,
            ForeColor = Color.White,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9f),
            Text = string.IsNullOrWhiteSpace(title) ? message : ($"{title}\n{message}"),
            MaximumSize = new Size(maxWidth, 0),
        };
        lbl.Size = TextRenderer.MeasureText(lbl.Text, lbl.Font, new Size(maxWidth, int.MaxValue), TextFormatFlags.WordBreak);
        lbl.Width = Math.Min(lbl.Width + 4, maxWidth);
        lbl.Height += 4;
        lbl.Location = new Point(padding, padding);
        panel.Controls.Add(lbl);

        var totalWidth = lbl.Right + padding;
        var totalHeight = lbl.Bottom + padding;
        ClientSize = new Size(totalWidth, totalHeight);
        Controls.Add(panel);

        // Position am unteren rechten Rand des aktiven Bildschirms
        var wa = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(wa.Right - Width - 12, wa.Bottom - Height - 12);

        _timer = new System.Windows.Forms.Timer { Interval = 3500 };
        _timer.Tick += (_, _) => Close();
        _timer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    private static readonly object _listLock = new();
    private static readonly System.Collections.Generic.List<ToastForm> _active = new();

    public static void ShowToast(Control invoker, string message, ToolTipIcon icon)
    {
        void ShowCore()
        {
            try
            {
                var toast = new ToastForm("Toolbox", message, icon);
                toast.FormClosed += (_, __) =>
                {
                    lock (_listLock)
                    {
                        _active.Remove(toast);
                    }
                    try { toast.Dispose(); } catch { }
                };
                lock (_listLock)
                {
                    _active.Add(toast);
                }
                toast.Show();
            }
            catch { }
        }

        try
        {
            if (invoker != null && invoker.IsHandleCreated)
            {
                invoker.BeginInvoke((Action)ShowCore);
            }
            else
            {
                ShowCore();
            }
        }
        catch { }
    }
}

