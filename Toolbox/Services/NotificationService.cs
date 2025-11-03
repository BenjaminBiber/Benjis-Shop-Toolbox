using MudBlazor;
using Toolbox.Data.Models.Interfaces;
using Toolbox.Data.Services;

namespace Toolbox.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ISnackbar _snackbar;

        public NotificationService(ISnackbar snackbar)
        {
            _snackbar = snackbar;
        }

        public void Success(string message) => _snackbar.Add(message, Severity.Success);
        public void Error(string message) => _snackbar.Add(message, Severity.Error);
        public void Info(string message) => _snackbar.Add(message, Severity.Info);
        public void Warning(string message) => _snackbar.Add(message, Severity.Warning);
    }
}
