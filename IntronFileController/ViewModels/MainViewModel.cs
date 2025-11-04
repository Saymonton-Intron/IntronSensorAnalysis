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
    private readonly IThemeHelper themeHelper;
    private readonly IFileImportService fileImportService;
    private readonly ServiceProvider serviceProvider;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleText))]
    private UserControl currentUC;
    partial void OnCurrentUCChanged(UserControl value)
    {
        if (value is HomeView home)
        {
            home.NavigateInvoked += Home_NavigateInvoked;
        }
    }
    partial void OnCurrentUCChanged(UserControl? oldValue, UserControl newValue)
    {
        if (newValue is HomeView home)
        {
            home.NavigateInvoked += Home_NavigateInvoked;
        }
        else if (oldValue is HomeView home1)
        {
            home1.NavigateInvoked -= Home_NavigateInvoked;
        }
        
        if (newValue is FileEditingView fileEdit)
        {
            fileEdit.NavigateInvoked += FileEditing_NavigateInvoked;
        }
        else if (oldValue is FileEditingView fileEdit1)
        {
            fileEdit1.NavigateInvoked -= FileEditing_NavigateInvoked;
        }
    }

    private void FileEditing_NavigateInvoked(object? sender, EventArgs e)
    {
        CurrentUC = serviceProvider.GetRequiredService<HomeView>();
    }

    private void Home_NavigateInvoked(object? sender, EventArgs e)
    {
        CurrentUC = serviceProvider.GetRequiredService<FileEditingView>();
    }

    public string TitleText => 
        CurrentUC switch 
        {
            HomeView => "Clique ou arraste para adicionar um arquivo",
            FileEditingView => "Processar arquivos selecionados",
            _ => ""
        };

    public PackIconKind CurrentThemeIcon =>
        themeHelper.GetCurrentBaseTheme() == BaseTheme.Dark
        ? PackIconKind.MoonWaxingCrescent
        : PackIconKind.WhiteBalanceSunny;

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
    }

    [RelayCommand]
    private void ChangeTheme()
    {
        themeHelper.ToggleTheme();
        OnPropertyChanged(nameof(CurrentThemeIcon));
    }

}
