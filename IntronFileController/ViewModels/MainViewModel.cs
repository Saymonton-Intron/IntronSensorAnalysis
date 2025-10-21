using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntronFileController.Helpers;
using IntronFileController.Views;
using MaterialDesignThemes.Wpf;
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
            HomeView => "Clique ou arraste para adicionar um arquivo.",
            _ => ""
        };
        
    
    public PackIconKind CurrentThemeIcon =>
        themeHelper.GetCurrentBaseTheme() == BaseTheme.Dark
        ? PackIconKind.MoonWaxingCrescent
        : PackIconKind.WhiteBalanceSunny;



    public MainViewModel(IThemeHelper _themeHelper, HomeViewModel homeViewModel)
    {
        themeHelper = _themeHelper;
        CurrentUC = new HomeView(homeViewModel);

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
