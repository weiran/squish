using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Squish.Core;
using Squish.Core.Abstractions;
using Squish.Core.Services;
using Squish.UI.Services;
using Squish.UI.ViewModels;
using Squish.UI.Views;

namespace Squish.UI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and CT
            BindingPlugins.DataValidators.RemoveAt(0);
            
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register Squish Core services
        services.AddTransient<IFileSystemWrapper, FileSystemWrapper>();
        services.AddTransient<IProcessWrapper, ProcessWrapper>();
        services.AddTransient<IFileFinder, FileFinder>();
        services.AddTransient<IVideoInspector, VideoInspector>();
        services.AddTransient<IVideoConverter, VideoConverter>();
        services.AddTransient<IQueueManager, QueueManager>();
        services.AddSingleton<InMemoryLogger>();
        services.AddTransient<ILogger>(provider => provider.GetRequiredService<InMemoryLogger>());
        services.AddTransient<JobRunner>();
        services.AddTransient<IJobRunner>(provider => provider.GetRequiredService<JobRunner>());

        // Register UI services
        services.AddTransient<IFolderPickerService, FolderPickerService>();

        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();

        // Register Views
        services.AddTransient<MainWindow>();
    }

    public override void RegisterServices()
    {
        base.RegisterServices();
    }
}