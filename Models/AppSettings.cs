namespace E3Studio.Models;

/// <summary>
/// Application settings and preferences
/// </summary>
public class AppSettings
{
    // File paths
    public string LastProjectPath { get; set; } = "";
    public List<string> RecentProjects { get; set; } = new();
    public int MaxRecentProjects { get; set; } = 10;
    
    // Selected machine
    public string SelectedMachineId { get; set; } = "";
    public Machine? SelectedMachine { get; set; }
    
    // Custom machines
    public List<Machine> CustomMachines { get; set; } = new();
    
    // G-Code settings
    public GCodeExportSettings GCodeSettings { get; set; } = new();
    
    // UI Preferences
    public bool ShowGrid { get; set; } = true;
    public bool ShowRulers { get; set; } = true;
    public bool ShowOrigin { get; set; } = true;
    public bool SnapToGrid { get; set; } = false;
    public double GridSpacing { get; set; } = 10.0;
    public string DefaultUnits { get; set; } = "mm";
    
    // Theme
    public string Theme { get; set; } = "Dark";
    
    // Canvas
    public double DefaultZoom { get; set; } = 1.0;
    public bool AutoFitOnImport { get; set; } = true;
    
    // Toolpath defaults
    public double DefaultFeedRate { get; set; } = 800;
    public double DefaultPlungeRate { get; set; } = 200;
    public double DefaultSpindleRPM { get; set; } = 12000;
    public double DefaultDepthPerPass { get; set; } = 2.0;
    public double DefaultSafeZ { get; set; } = 25.0;
    
    // Tool library
    public List<Tool> Tools { get; set; } = new();
    
    // Custom post processors
    public List<Dialogs.PostProcessorInfo>? CustomPostProcessors { get; set; }
    
    // Simulation
    public double SimulationSpeed { get; set; } = 1.0;
    public bool ShowToolpathPreview { get; set; } = true;
    
    // Auto-save
    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveInterval { get; set; } = 5; // minutes
    
    /// <summary>
    /// Add a project to recent list
    /// </summary>
    public void AddRecentProject(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        // Remove if already exists
        RecentProjects.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        
        // Add to beginning
        RecentProjects.Insert(0, path);
        
        // Trim to max
        while (RecentProjects.Count > MaxRecentProjects)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }
        
        LastProjectPath = path;
    }
    
    /// <summary>
    /// Get all available machines (default + custom)
    /// </summary>
    public List<Machine> GetAllMachines()
    {
        var machines = Machine.GetDefaultMachines();
        machines.AddRange(CustomMachines);
        return machines;
    }
}

/// <summary>
/// G-Code export specific settings
/// </summary>
public class GCodeExportSettings
{
    // Safety
    public double SafeZ { get; set; } = 25.0;
    public double RapidClearance { get; set; } = 5.0;
    public double ToolChangeHeight { get; set; } = 50.0;
    public double MinSafeHeight { get; set; } = 15.0;
    public double SafetyMargin { get; set; } = 5.0;
    public double FixtureHeight { get; set; } = 0.0;
    
    // Spindle
    public double SpindleWarmupTime { get; set; } = 3.0; // seconds
    public bool WaitForSpindleSpeed { get; set; } = true;
    
    // Coolant
    public bool UseCoolant { get; set; } = false;
    public CoolantType CoolantType { get; set; } = CoolantType.Flood;
    
    // Tool change
    public bool IncludeToolChange { get; set; } = true;
    public bool UseToolLengthComp { get; set; } = true;
    public bool ManualToolChange { get; set; } = true;
    
    // Canned cycles
    public bool UseCannedCycles { get; set; } = true;
    
    // Output format
    public string LineEnding { get; set; } = "\r\n";
    public bool IncludeLineNumbers { get; set; } = false;
    public int LineNumberIncrement { get; set; } = 10;
    public bool IncludeComments { get; set; } = true;
    public int DecimalPlaces { get; set; } = 3;
    
    // Post processor
    public string PostProcessor { get; set; } = "GRBL";
    public string FileExtension { get; set; } = ".nc";
    public string ProgramNumber { get; set; } = "O0001";
    
    // Arc output
    public bool OutputArcsAsArcs { get; set; } = true;
    public double ArcTolerance { get; set; } = 0.01;
    
    // Feed modes
    public bool UseInverseTime { get; set; } = false;
}

public enum CoolantType
{
    Off,
    Flood,
    Mist,
    Through,
    Air
}
