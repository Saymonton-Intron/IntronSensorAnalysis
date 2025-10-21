using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntronFileController.Helpers;
using MaterialDesignThemes.Wpf;
using System.Windows;

namespace IntronFileController.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private IThemeHelper themeHelper;

    public PackIconKind CurrentThemeIcon
    {
        get
        {
            var x = themeHelper.GetCurrentBaseTheme() == BaseTheme.Dark
            ? PackIconKind.MoonWaxingCrescent
            : PackIconKind.WhiteBalanceSunny;

            return x;
        }
    }

    public MainViewModel(IThemeHelper _themeHelper)
    {
        themeHelper = _themeHelper;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            themeHelper.SetLightTheme();
            OnPropertyChanged(nameof(CurrentThemeIcon));
        });
    }


    [RelayCommand]
    private void ChangeTheme()
    {
        themeHelper.ToggleTheme();
        OnPropertyChanged(nameof(CurrentThemeIcon));
    }

    [RelayCommand]
    private void AddFileButton()
    {

    }
}
