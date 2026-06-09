using System.Windows;

namespace DdosDefendSystem.AdminPanel.Services;

public enum AppTheme
{
    Dark,
    Light
}

public class ThemeService
{
    private readonly Uri _darkTheme = new("Themes/DarkTheme.xaml", UriKind.Relative);
    private readonly Uri _lightTheme = new("Themes/LightTheme.xaml", UriKind.Relative);

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public event Action? ThemeChanged;

    public void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        var app = Application.Current;
        if (app.Resources.MergedDictionaries.Count == 0)
            return;

        var themeDict = app.Resources.MergedDictionaries[0];
        themeDict.Source = theme == AppTheme.Dark ? _darkTheme : _lightTheme;
        ThemeChanged?.Invoke();
    }

    public void ToggleTheme() =>
        ApplyTheme(CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    public string ThemeToggleLabel => CurrentTheme == AppTheme.Dark ? "☀" : "☾";
}
