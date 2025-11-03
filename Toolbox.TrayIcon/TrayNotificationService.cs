using System;
using System.Windows.Forms;
using Toolbox.Data.Models.Interfaces;

sealed class TrayNotificationService : INotificationService
{
    private readonly NotifyIcon _tray;
    private readonly Control _invoker;

    public TrayNotificationService(NotifyIcon tray, Control invoker)
    {
        _tray = tray;
        _invoker = invoker;
    }

    public void Success(string message) => Show(message, ToolTipIcon.Info);
    public void Error(string message) => Show(message, ToolTipIcon.Error);
    public void Info(string message) => Show(message, ToolTipIcon.Info);
    public void Warning(string message) => Show(message, ToolTipIcon.Warning);

    private void Show(string message, ToolTipIcon icon)
    {
        try
        {
            void ShowCore()
            {
                try
                {
                    if (!_tray.Visible) _tray.Visible = true;
                    _tray.BalloonTipTitle = "Toolbox";
                    _tray.BalloonTipText = message;
                    _tray.BalloonTipIcon = icon;
                    _tray.ShowBalloonTip(4000);
                    // Fallback-Toast (falls Balloons vom OS unterdrückt werden)
                    ToastForm.ShowToast(_invoker, message, icon);
                }
                catch { }
            }

            if (_invoker.IsHandleCreated)
            {
                _invoker.BeginInvoke((Action)ShowCore);
            }
            else
            {
                ShowCore();
            }
        }
        catch { }
    }
}

