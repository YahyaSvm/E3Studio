using System;
using System.Windows;
using System.Windows.Media;

namespace E3Studio.Services;

/// <summary>
/// Theme management service for dark/light themes
/// </summary>
public class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();
    
    private AppTheme _currentTheme = AppTheme.Dark;
    
    public event EventHandler? ThemeChanged;
    
    public AppTheme CurrentTheme => _currentTheme;
    
    private ThemeService() { }
    
    public void SetTheme(AppTheme theme)
    {
        _currentTheme = theme;
        ApplyTheme();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public void ToggleTheme()
    {
        SetTheme(_currentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    }
    
    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;
        
        var colors = GetThemeColors(_currentTheme);
        
        // Update application resources
        app.Resources["BackgroundColor"] = new SolidColorBrush(colors.Background);
        app.Resources["ForegroundColor"] = new SolidColorBrush(colors.Foreground);
        app.Resources["PrimaryColor"] = new SolidColorBrush(colors.Primary);
        app.Resources["SecondaryColor"] = new SolidColorBrush(colors.Secondary);
        app.Resources["AccentColor"] = new SolidColorBrush(colors.Accent);
        app.Resources["BorderColor"] = new SolidColorBrush(colors.Border);
        app.Resources["CanvasBackground"] = new SolidColorBrush(colors.CanvasBackground);
        app.Resources["GridColor"] = new SolidColorBrush(colors.GridColor);
        app.Resources["SelectionColor"] = new SolidColorBrush(colors.Selection);
        app.Resources["ErrorColor"] = new SolidColorBrush(colors.Error);
        app.Resources["WarningColor"] = new SolidColorBrush(colors.Warning);
        app.Resources["SuccessColor"] = new SolidColorBrush(colors.Success);
        app.Resources["ToolpathColor"] = new SolidColorBrush(colors.Toolpath);
        app.Resources["RapidColor"] = new SolidColorBrush(colors.Rapid);
    }
    
    private ThemeColors GetThemeColors(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Dark => new ThemeColors
            {
                Background = Color.FromRgb(30, 30, 30),
                Foreground = Color.FromRgb(220, 220, 220),
                Primary = Color.FromRgb(45, 45, 48),
                Secondary = Color.FromRgb(37, 37, 38),
                Accent = Color.FromRgb(0, 122, 204),
                Border = Color.FromRgb(63, 63, 70),
                CanvasBackground = Color.FromRgb(20, 20, 25),
                GridColor = Color.FromRgb(50, 50, 55),
                Selection = Color.FromRgb(0, 150, 255),
                Error = Color.FromRgb(232, 17, 35),
                Warning = Color.FromRgb(255, 185, 0),
                Success = Color.FromRgb(16, 185, 129),
                Toolpath = Color.FromRgb(0, 200, 150),
                Rapid = Color.FromRgb(255, 100, 100)
            },
            AppTheme.Light => new ThemeColors
            {
                Background = Color.FromRgb(243, 243, 243),
                Foreground = Color.FromRgb(30, 30, 30),
                Primary = Color.FromRgb(255, 255, 255),
                Secondary = Color.FromRgb(240, 240, 240),
                Accent = Color.FromRgb(0, 120, 215),
                Border = Color.FromRgb(200, 200, 200),
                CanvasBackground = Color.FromRgb(255, 255, 255),
                GridColor = Color.FromRgb(220, 220, 220),
                Selection = Color.FromRgb(0, 120, 215),
                Error = Color.FromRgb(232, 17, 35),
                Warning = Color.FromRgb(255, 140, 0),
                Success = Color.FromRgb(16, 185, 129),
                Toolpath = Color.FromRgb(0, 150, 100),
                Rapid = Color.FromRgb(200, 50, 50)
            },
            AppTheme.HighContrast => new ThemeColors
            {
                Background = Color.FromRgb(0, 0, 0),
                Foreground = Color.FromRgb(255, 255, 255),
                Primary = Color.FromRgb(0, 0, 0),
                Secondary = Color.FromRgb(20, 20, 20),
                Accent = Color.FromRgb(0, 255, 255),
                Border = Color.FromRgb(255, 255, 255),
                CanvasBackground = Color.FromRgb(0, 0, 0),
                GridColor = Color.FromRgb(80, 80, 80),
                Selection = Color.FromRgb(255, 255, 0),
                Error = Color.FromRgb(255, 0, 0),
                Warning = Color.FromRgb(255, 255, 0),
                Success = Color.FromRgb(0, 255, 0),
                Toolpath = Color.FromRgb(0, 255, 255),
                Rapid = Color.FromRgb(255, 0, 0)
            },
            _ => GetThemeColors(AppTheme.Dark)
        };
    }
    
    public ThemeColors GetCurrentColors() => GetThemeColors(_currentTheme);
}

public enum AppTheme
{
    Dark,
    Light,
    HighContrast
}

public class ThemeColors
{
    public Color Background { get; set; }
    public Color Foreground { get; set; }
    public Color Primary { get; set; }
    public Color Secondary { get; set; }
    public Color Accent { get; set; }
    public Color Border { get; set; }
    public Color CanvasBackground { get; set; }
    public Color GridColor { get; set; }
    public Color Selection { get; set; }
    public Color Error { get; set; }
    public Color Warning { get; set; }
    public Color Success { get; set; }
    public Color Toolpath { get; set; }
    public Color Rapid { get; set; }
}
