using System.IO;
using System.Text.Json;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Manages application settings persistence
/// </summary>
public class SettingsManager
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "E3Studio"
    );
    
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");
    
    private static AppSettings? _instance;
    private static readonly object _lock = new();
    
    /// <summary>
    /// Get the current settings instance
    /// </summary>
    public static AppSettings Current
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// Load settings from file
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (settings != null)
                {
                    // Resolve selected machine
                    if (!string.IsNullOrEmpty(settings.SelectedMachineId))
                    {
                        var allMachines = settings.GetAllMachines();
                        settings.SelectedMachine = allMachines.FirstOrDefault(m => m.Id == settings.SelectedMachineId)
                                                   ?? allMachines.FirstOrDefault();
                    }
                    else
                    {
                        settings.SelectedMachine = Machine.GetDefaultMachines().FirstOrDefault();
                    }
                    
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }
        
        // Return default settings
        var defaultSettings = new AppSettings();
        defaultSettings.SelectedMachine = Machine.GetDefaultMachines().FirstOrDefault();
        return defaultSettings;
    }
    
    /// <summary>
    /// Save settings to file
    /// </summary>
    public static void Save()
    {
        Save(Current);
    }
    
    /// <summary>
    /// Save specific settings to file
    /// </summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(SettingsFolder);
            
            // Update machine ID
            if (settings.SelectedMachine != null)
            {
                settings.SelectedMachineId = settings.SelectedMachine.Id;
            }
            
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(SettingsFile, json);
            
            _instance = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Reset settings to defaults
    /// </summary>
    public static void Reset()
    {
        _instance = new AppSettings();
        _instance.SelectedMachine = Machine.GetDefaultMachines().FirstOrDefault();
        Save();
    }
    
    /// <summary>
    /// Add to recent projects
    /// </summary>
    public static void AddRecentProject(string path)
    {
        Current.AddRecentProject(path);
        Save();
    }
    
    /// <summary>
    /// Get recent projects that still exist
    /// </summary>
    public static List<string> GetValidRecentProjects()
    {
        return Current.RecentProjects.Where(File.Exists).ToList();
    }
}
