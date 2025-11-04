using IntronFileController.Helpers;
using IntronFileController.Services;
using IntronFileController.ViewModels;
using IntronFileController.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace IntronFileController;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IHost AppHost { get; private set; }
    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Views
                services.AddSingleton<MainView>();
                services.AddTransient<HomeView>();
                services.AddTransient<FileEditingView>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<HomeViewModel>();
                services.AddTransient<FileEditingViewModel>();

                // Helpers
                services.AddSingleton<IThemeHelper, ThemeHelper>();
                services.AddSingleton<IFileHandlerHelper, FileHandlerHelper>();

                // Services
                services.AddSingleton<IFileImportService, FileImportService>();
                services.AddSingleton<IFileExportService, FileExportService>();
                services.AddSingleton<ServiceProvider>(services.BuildServiceProvider());
            })
            .Build();

        MainWindow = AppHost.Services.GetRequiredService<MainView>();
        MainWindow.Show();
    }
}
