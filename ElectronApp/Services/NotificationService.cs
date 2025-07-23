using MudBlazor;

namespace Benjis_Shop_Toolbox.Services
{
    /// <summary>
    /// Provides helper methods to display messages via <see cref="ISnackbar"/>.
    /// </summary>
    public class NotificationService
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

