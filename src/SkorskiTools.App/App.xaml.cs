using System.IO;
using System.Windows;
using Serilog;
using SkorskiTools.App.Infrastructure;
using SkorskiTools.App.ViewModels;
using SkorskiTools.App.Views;
using SkorskiTools.Core.Infrastructure;
using SkorskiTools.Core.Services;

namespace SkorskiTools.App;

/// <summary>Application startup: logging, user folders and manual service composition.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureLogging();

        Log.Information("Skorski Tools {Version} starting", AppVersion.Current);

        var userFolders = new UserFoldersService();
        var folders = userFolders.EnsureCreated();
        Log.Information(
            "User folders ready at {Root}. Created: {CreatedCount}, existing: {ExistingCount}",
            folders.RootPath, folders.CreatedFolders.Count, folders.ExistingFolders.Count);

        var mainViewModel = new MainViewModel(folders, new ResourceCatalogService());
        MainWindow = new MainWindow { DataContext = mainViewModel };
        MainWindow.Show();

        // WPF-UI's FluentWindow resets Title while its template is applied,
        // so the final value must be set after the window is shown.
        MainWindow.Title = AppPaths.ApplicationName;

        Log.Information(
            "Main window shown on {Page} with {Count} navigation items",
            mainViewModel.CurrentPage.Title, mainViewModel.NavigationItems.Count);

        _ = mainViewModel.LoadCatalogAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Skorski Tools exiting");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging()
    {
        var logsDirectory = AppPaths.GetLogsDirectory();
        Directory.CreateDirectory(logsDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logsDirectory, "skorski-tools-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
