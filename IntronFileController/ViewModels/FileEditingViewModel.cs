using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntronFileController.Helpers;
using IntronFileController.Models;
using IntronFileController.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace IntronFileController.ViewModels
{
    public partial class FileEditingViewModel : ObservableObject
    {
        public event EventHandler NavigateInvoked;

        [ObservableProperty]
        private ObservableCollection<ImportedFile> importedFiles = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
        private ImportedFile selectedFile;

        private readonly IFileHandlerHelper fileHandlerHelper;
        private readonly IFileImportService fileImportService;

        [ObservableProperty] private Visibility addCardVisibility = Visibility.Visible;
        [ObservableProperty] private Visibility restOfContentVisibility = Visibility.Hidden;

        public FileEditingViewModel(IFileHandlerHelper _fileHandlerHelper, IFileImportService _fileImportService)
        {
            fileHandlerHelper = _fileHandlerHelper;
            fileImportService = _fileImportService;
            ImportedFiles = fileHandlerHelper.ImportedFiles;
            SelectedFile = ImportedFiles?.FirstOrDefault()!;

            ImportedFiles!.CollectionChanged += (a, b) =>
            {
                RemoveSelectedCommand.NotifyCanExecuteChanged();
            };
        }

        [RelayCommand]
        private void NavigateBack()
        {
            ImportedFiles.Clear();
            //fileHandlerHelper.ImportedFiles.Clear();
            NavigateInvoked?.Invoke(this, new());
        }

        [RelayCommand]
        private async Task AddFiles()
        {
            // Abre dialogo para selecionar os arquivos para importar
            var textPaths = fileImportService.OpenDialogSelectTextFiles();

            // chama serviço para importar (faz leitura assíncrona)
            var imported = await fileImportService.ImportTextFilesAsync(textPaths);

            // Adiciona os arquivos no fileHandler
            fileHandlerHelper.AddFiles(imported);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteRemoveSelectedCommand))]
        private void RemoveSelected()
        {
            ImportedFiles.Remove(SelectedFile);
            SelectedFile = ImportedFiles!.FirstOrDefault()!;

        }
        private bool CanExecuteRemoveSelectedCommand() => SelectedFile != null && ImportedFiles.Count > 0;
    }
}
