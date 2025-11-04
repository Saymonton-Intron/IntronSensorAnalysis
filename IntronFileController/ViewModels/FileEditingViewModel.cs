using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntronFileController.Common;
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
using static System.Net.Mime.MediaTypeNames;

namespace IntronFileController.ViewModels
{
    public partial class FileEditingViewModel : ObservableObject
    {
        public event EventHandler NavigateInvoked;

        [ObservableProperty]
        private ObservableCollection<ImportedFile> importedFiles = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
        [NotifyPropertyChangedFor(nameof(LabelsVisibility))]
        private ImportedFile selectedFile;

        [ObservableProperty] private int previewLinesCount = 20;

        private readonly IFileHandlerHelper fileHandlerHelper;
        private readonly IFileImportService fileImportService;

        [ObservableProperty] private Visibility addCardVisibility = Visibility.Visible;
        [ObservableProperty] private Visibility restOfContentVisibility = Visibility.Hidden;
        public Visibility LabelsVisibility => SelectedFile != null ? Visibility.Visible : Visibility.Collapsed;

        private string firstLinesTextBox = "100";
        public string FirstLinesTextBox
        {
            get => firstLinesTextBox.ToString();
            set
            {
                if (!int.TryParse(value, out _))
                    return;

                SetProperty(ref firstLinesTextBox, value);
            }
        }

        private string lastLinesTextBox = "100";
        public string LastLinesTextBox
        {
            get => lastLinesTextBox;
            set
            {
                if (!int.TryParse(value, out _))
                    return;

                SetProperty(ref lastLinesTextBox, value);
            }
        }

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

            // Corta as primeiras e ultimas linhas de cada arquivo
            foreach (var file in imported)
            {
                char[] charArray = file.Preview.ToCharArray();
                int linhasPuladas = 0;
                int linhasTotais = charArray.Where((c) => c == '\r').ToList().Count();
                int listToFill = 0; // 0 == FirstLines; 1 == KeepLines; 2 == LastLines

                for (int charCount = 0; charCount < charArray.Length; charCount++)
                {
                    //switch (listToFill) 
                    //{
                    //    case 0: if (linhasPuladas >= int.Parse(FirstLinesTextBox)) listToFill = 1; break;
                    //    case 1: if (linhasPuladas >= linhasTotais - int.Parse(LastLinesTextBox)) listToFill = 2; break;
                    //}

                    //if (linhasPuladas >= int.Parse(FirstLinesTextBox))
                    //{
                    //    listToFill = 1;
                    //    if (linhasPuladas >= linhasTotais - int.Parse(LastLinesTextBox))
                    //    {
                    //        listToFill = 2;
                    //    }
                    //}

                    //if (linhasPuladas >= int.Parse(FirstLinesTextBox)) break;
                    if (charArray[charCount] == '\r' && charArray[charCount + 1] == '\n')
                    {
                        // Quebra de linha
                        linhasPuladas++;
                    }

                    switch (listToFill)
                    {
                        case 0: file.FirstLines += charArray[charCount]; if (linhasPuladas >= int.Parse(FirstLinesTextBox)) listToFill = 1; break;
                        case 1: file.KeepLines += charArray[charCount]; if (linhasPuladas >= linhasTotais - int.Parse(LastLinesTextBox)) listToFill = 2; break;
                        case 2: file.LastLines += charArray[charCount]; break;
                    }
                }

                file.FirstLines = file.FirstLines.LastLines(PreviewLinesCount);
                file.KeepFirstLines = file.KeepLines.FirstLines(PreviewLinesCount);
                file.KeepLastLines = file.KeepLines.LastLines(PreviewLinesCount);
                file.LastLines = file.LastLines.FirstLines(PreviewLinesCount);
            }

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
