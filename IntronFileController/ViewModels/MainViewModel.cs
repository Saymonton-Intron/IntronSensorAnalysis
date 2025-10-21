using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using IntronFileController.Helpers;
using IntronFileController.Models;
using IntronFileController.Services;
using IntronFileController.Views;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace IntronFileController.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private IThemeHelper themeHelper;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleText))]
    private UserControl currentUC;
    public string TitleText => 
        CurrentUC switch 
        {
            HomeView => "Clique ou arraste para adicionar um arquivo",
            FileEditingView => "Processar arquivos selecionados",
            _ => ""
        };

    private readonly IFileImportService fileImportService;

    public PackIconKind CurrentThemeIcon =>
        themeHelper.GetCurrentBaseTheme() == BaseTheme.Dark
        ? PackIconKind.MoonWaxingCrescent
        : PackIconKind.WhiteBalanceSunny;

    private readonly HomeView homeView;
    private readonly FileEditingView fileEditingView;
    private readonly ServiceProvider serviceProvider;

    public MainViewModel(IThemeHelper _themeHelper, IFileImportService _fileImportService, ServiceProvider _serviceProvider)
    {
        themeHelper = _themeHelper;
        fileImportService = _fileImportService;
        serviceProvider = _serviceProvider;

        CurrentUC = serviceProvider.GetRequiredService<HomeView>();

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            themeHelper.SetDarkTheme();
            OnPropertyChanged(nameof(CurrentThemeIcon));
        });

        RegisterWeakMessages();

    }

    private void RegisterWeakMessages()
    {
        WeakReferenceMessenger.Default.Register<HomeViewModel>(this, async (sender, viewModel) =>
        {
            if (viewModel.TextPaths is string[] paths && paths.Length > 0)
            {
                ObservableCollection<ImportedFile> ImportedFiles = new();
                // chama serviço para importar (faz leitura assíncrona)
                var imported = await fileImportService.ImportTextFilesAsync(paths);

                // dedup pelo caminho
                foreach (var f in imported)
                {
                    if (!ImportedFiles.Any(x => x.FilePath.Equals(f.FilePath, System.StringComparison.OrdinalIgnoreCase)))
                        ImportedFiles.Add(f);
                }

                var editingView = serviceProvider.GetRequiredService<FileEditingView>();
                editingView.SetImportedFiles(ImportedFiles);
                CurrentUC = editingView;
                
                // seleciona o último adicionado
                //SelectedFile = ImportedFiles.LastOrDefault();

                // Mandar pra fileEditingView com a lista e lá ele seleciona o ultimo
            }
        });
    }

    [RelayCommand]
    private void ChangeTheme()
    {
        themeHelper.ToggleTheme();
        OnPropertyChanged(nameof(CurrentThemeIcon));
    }

}
