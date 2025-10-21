using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        [ObservableProperty]
        private ObservableCollection<ImportedFile> importedFiles;

        [ObservableProperty]
        private ImportedFile selectedFile;

        [ObservableProperty] private Visibility addCardVisibility = Visibility.Visible;
        [ObservableProperty] private Visibility restOfContentVisibility = Visibility.Hidden;

        [RelayCommand]
        private void SetImportedFiles(ObservableCollection<Models.ImportedFile> _importedFiles)
        {
            ImportedFiles = _importedFiles;
            SelectedFile = ImportedFiles.FirstOrDefault();
        }

        [RelayCommand]
        private void NavigateBack()
        {

        }

        [RelayCommand]
        private void AddFiles()
        {

        }

        [RelayCommand]
        private void RemoveSelected()
        {
        
        }
    }
}
