using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntronFileController.Common;
using IntronFileController.Common.Extensions;
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
using System.Windows.Documents;
using static System.Net.Mime.MediaTypeNames;

namespace IntronFileController.ViewModels
{
    public partial class FileEditingViewModel : ObservableObject
    {
        public event EventHandler NavigateInvoked;

        [ObservableProperty]
        private ObservableCollection<ImportedFileViewModel> importedFiles = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
        [NotifyCanExecuteChangedFor(nameof(ProcessAllCommand))]
        [NotifyPropertyChangedFor(nameof(LabelsVisibility))]
        [NotifyPropertyChangedFor(nameof(FirstLinesTextBox))]
        [NotifyPropertyChangedFor(nameof(LastLinesTextBox))]
        [NotifyPropertyChangedFor(nameof(TextInitLabelVisibility))]
        [NotifyPropertyChangedFor(nameof(TextEndLabelVisibility))]
        private ImportedFileViewModel selectedFile;

        [ObservableProperty] private int previewLinesCount = 20;

        private readonly IFileHandlerHelper fileHandlerHelper;
        private readonly IFileImportService fileImportService;
        private readonly IFileExportService fileExportService;

        [ObservableProperty] private Visibility addCardVisibility = Visibility.Visible;
        [ObservableProperty] private Visibility restOfContentVisibility = Visibility.Hidden;
        public Visibility TextInitLabelVisibility => SelectedFile.TopContextCount >= SelectedFile.TopCutLine ? Visibility.Visible : Visibility.Hidden;
        public Visibility TextEndLabelVisibility => SelectedFile.BottomContextCount >= SelectedFile.Model.Preview.Lines().Length - SelectedFile.BottomCutLine ? Visibility.Visible : Visibility.Hidden;
        public Visibility LabelsVisibility => SelectedFile != null ? Visibility.Visible : Visibility.Collapsed;

        private string firstLinesTextBox = "0";
        public string FirstLinesTextBox
        {
            get
            {
                if (SelectedFile is null) return firstLinesTextBox;
                return SelectedFile.TopCutLine.ToString();
            }
            set
            {
                if (!int.TryParse(value, out var cut) || cut < 0) return; // aceita 0+; 0 = não corta nada no topo

                if (SelectedFile is not null)
                {
                    SelectedFile.TopCutLine = cut; // 1-based

                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));
                }

            }
        }


        private string lastLinesTextBox = "0";
        public string LastLinesTextBox
        {
            get
            {
                if (SelectedFile is null) return lastLinesTextBox;

                var total = SelectedFile.Model.Preview.Lines().Length;
                // offset a partir do fim (o que aparece no TextBox)
                // regra: bottomCutLine = total - offset
                var offset = total - SelectedFile.BottomCutLine;
                return offset.ToString();
            }
            set
            {
                if (!int.TryParse(value, out var offset) || offset < 0)
                    return;

                if (SelectedFile is not null)
                {
                    var total = SelectedFile.Model.Preview.Lines().Length;

                    // Corte no rodapé = (última linha) - offset  (1-based)
                    // Ex.: total=200, offset=3  -> corte = 197
                    // clamp para não sair do arquivo
                    int bottomCutLine = Math.Max(1, total - offset);

                    SelectedFile.BottomCutLine = bottomCutLine;
                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));
                    // NÃO mexe no BottomContextCount: ele já dita quantas linhas mostrar.
                }
            }
        }
        private string topContextTextBox = "5";
        public string TopContextTextBox
        {
            get => topContextTextBox;
            set
            {
                if (!int.TryParse(value, out var ctx) || ctx < 0) return;
                if (SetProperty(ref topContextTextBox, value))
                {
                    if (SelectedFile is not null)
                        SelectedFile.TopContextCount = ctx;
                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));
                }
            }
        }

        private string bottomContextTextBox = "5";
        public string BottomContextTextBox
        {
            get => bottomContextTextBox;
            set
            {
                if (!int.TryParse(value, out var ctx) || ctx < 0) return;
                if (SetProperty(ref bottomContextTextBox, value))
                {
                    if (SelectedFile is not null)
                        SelectedFile.BottomContextCount = ctx;

                    OnPropertyChanged(nameof(TextInitLabelVisibility));
                    OnPropertyChanged(nameof(TextEndLabelVisibility));
                }
            }
        }

        public FileEditingViewModel(IFileHandlerHelper _fileHandlerHelper, IFileImportService _fileImportService, IFileExportService _fileExportService)
        {
            fileHandlerHelper = _fileHandlerHelper;
            fileImportService = _fileImportService;
            fileExportService = _fileExportService;
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

        [RelayCommand(CanExecute = nameof(CanExecuteProcessAllCommand))]
        private async Task ProcessAll()
        {
            var readOnlyList = new ReadOnlyObservableCollection<ImportedFileViewModel>(ImportedFiles);
            await fileExportService.ExportAsync(readOnlyList, owner: System.Windows.Application.Current.MainWindow);
        }
        private bool CanExecuteProcessAllCommand() => SelectedFile != null && ImportedFiles.Count > 0; // Alterar depois

        [RelayCommand]
        private void ApplyToAll()
        {
            if (ImportedFiles.Count <= 1 || SelectedFile is null)
                return;

            // 1) Descobre N = quantas últimas linhas o SelectedFile está mantendo
            int selTotal = SelectedFile.Model.Preview.Lines().Length;

            // Se BottomCutLine é 1-based e inclusivo (ex.: 1 = primeira linha), então:
            // N_keep = total - BottomCutLine + 1
            int nKeepFromEnd = Math.Max(0, selTotal - SelectedFile.BottomCutLine + 1);

            foreach (var file in ImportedFiles)
            {
                if (ReferenceEquals(file, SelectedFile)) continue;

                // copia o TopCutLine "como está" (se você também quiser preservar “quantas primeiras linhas” ao invés do índice,
                // faça um cálculo similar ao do bottom)
                file.TopCutLine = SelectedFile.TopCutLine;

                // 2) Recalcula o índice de corte para ESTE arquivo,
                //     de modo que ele também mantenha as mesmas N últimas linhas
                int total = file.Model.Preview.Lines().Length;

                // índice 1-based do começo do “rabo” (onde começam as últimas N)
                int cutIndexFromTop = Math.Max(1, total - nKeepFromEnd + 1);

                file.BottomCutLine = cutIndexFromTop; // <<< agora atribui no alvo
            }
        }
    }
}
