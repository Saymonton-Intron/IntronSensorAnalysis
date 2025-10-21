using MaterialDesignThemes.Wpf;

namespace IntronFileController.Helpers;
public class ThemeHelper : IThemeHelper
{
    private readonly PaletteHelper palleteHelper = new();
    private Theme CurrentTheme => palleteHelper.GetTheme();
    public ThemeHelper()
    {

    }

    public BaseTheme GetCurrentBaseTheme() =>
        palleteHelper.GetTheme().GetBaseTheme();

    public void SetDarkTheme()
    {
        CurrentTheme.SetBaseTheme(BaseTheme.Dark);
        palleteHelper.SetTheme(CurrentTheme);
    }

    public void SetLightTheme()
    {
        CurrentTheme.SetBaseTheme(BaseTheme.Light);
        palleteHelper.SetTheme(CurrentTheme);
    }

    public void ToggleTheme()
    {
        if (CurrentTheme.GetBaseTheme() == BaseTheme.Light)
            SetDarkTheme();
        else
            SetLightTheme();
    }
}