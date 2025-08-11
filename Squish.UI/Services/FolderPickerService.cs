using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Squish.UI.Services;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(string title = "Select Folder")
    {
        // Get the main window
        var mainWindow = App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow?.StorageProvider is { } storageProvider)
        {
            var options = new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);
            return result.FirstOrDefault()?.Path.LocalPath;
        }

        return null;
    }
}