using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using IntronFileController.Models;
using IntronFileController.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;

namespace IntronFileController.ViewModels;

public partial class HomeViewModel : ObservableObject
{

    public string[]? TextPaths;

    [RelayCommand]
    private async Task AddFileButton()
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Selecione um ou mais arquivos .txt"
        };

        bool? result = dlg.ShowDialog();
        if (result != true) return;

        var selectedPaths = dlg.FileNames;
        if (selectedPaths == null || selectedPaths.Length == 0) return;

        // opcional: filtrar por tamanho, extensão, etc.
        TextPaths = selectedPaths.Where(p => p.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)).ToArray();


        WeakReferenceMessenger.Default.Send<HomeViewModel>(this);
    }
}
