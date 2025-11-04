using MudBlazor;
using Toolbox.Components.Dialogs;
using Toolbox.Data.Models;

namespace Toolbox.ExtensionMethods;

public static class DialogExtensions
{
    public static async Task<bool> ShowCloneDialog(this IDialogService dialogService, string title, string destination, List<RepoAction> actions)
    {
        var parameters = new DialogParameters<CloneRepoDialog>()
        {
            {x => x.DestinationRoot, destination},
            {x => x.Title, title},
            {x => x.Actions, actions}
        };
        var options = new DialogOptions()
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Medium,
            NoHeader = true,
            BackdropClick = true,
            CloseButton = true
        };
        var dlg = await dialogService.ShowAsync<CloneRepoDialog>("Repo klonen", parameters, options);
        var result = await dlg.Result;
        return result.Canceled;
    }
}