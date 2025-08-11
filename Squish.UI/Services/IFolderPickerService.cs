using System.Threading.Tasks;

namespace Squish.UI.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(string title = "Select Folder");
}