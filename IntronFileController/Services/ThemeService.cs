using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntronFileController.Services;

public interface IThemeService
{
    BaseTheme Current { get; }
    event EventHandler<BaseTheme>? ThemeChanged;
    void Set(BaseTheme theme);
    void Toggle();
}

public sealed class ThemeService : IThemeService
{
    private readonly PaletteHelper _palette = new();
    public event EventHandler<BaseTheme>? ThemeChanged;

    public BaseTheme Current => _palette.GetTheme().GetBaseTheme();

    public void Set(BaseTheme theme)
    {
        var th = _palette.GetTheme();
        th.SetBaseTheme(theme);
        _palette.SetTheme(th);
        ThemeChanged?.Invoke(this, theme);
    }

    public void Toggle() =>
        Set(Current == BaseTheme.Light ? BaseTheme.Dark : BaseTheme.Light);
}
