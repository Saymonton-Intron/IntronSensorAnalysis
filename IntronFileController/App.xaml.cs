using IntronFileController.Helpers;
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
                services.AddSingleton<HomeView>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<HomeViewModel>();

                // Helpers
                services.AddSingleton<IThemeHelper, ThemeHelper>();
            })
            .Build();

        MainWindow = AppHost.Services.GetRequiredService<MainView>();
        MainWindow.Show();
    }
}
