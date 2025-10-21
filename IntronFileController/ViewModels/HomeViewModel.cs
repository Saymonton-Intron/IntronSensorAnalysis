using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using IntronFileController.Helpers;
using IntronFileController.Models;
using IntronFileController.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;

namespace IntronFileController.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IFileImportService fileImportService;
    private readonly IFileHandlerHelper fileHandler;
    public event EventHandler NavigateInvoked;

    public HomeViewModel(IFileImportService _fileImportService, IFileHandlerHelper _fileHandler)
    {
        fileImportService = _fileImportService;
        fileHandler = _fileHandler;
    }

    [RelayCommand]
    private async Task AddFileButton()
    {
        // Abre dialogo para selecionar os arquivos para importar
        var textPaths = fileImportService.OpenDialogSelectTextFiles();

        if (textPaths.Length == 0) return;

        // chama serviço para importar (faz leitura assíncrona)
        var imported = await fileImportService.ImportTextFilesAsync(textPaths);

        // Adiciona os arquivos no fileHandler
        fileHandler.AddFiles(imported);

        // invoke back
        NavigateInvoked?.Invoke(this, new());
    }

    public async Task AddFilesFromPathsAsync(string[] paths)
    {
        var imported = await fileImportService.ImportTextFilesAsync(paths);
        fileHandler.AddFiles(imported);
        NavigateInvoked?.Invoke(this, new());
    }
}
