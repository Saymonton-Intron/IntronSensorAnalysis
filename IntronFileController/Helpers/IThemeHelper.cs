using MaterialDesignThemes.Wpf;

namespace IntronFileController.Helpers;

public interface IThemeHelper
{
    BaseTheme GetCurrentBaseTheme();

    void SetDarkTheme();
    void SetLightTheme();

    void ToggleTheme();
}
