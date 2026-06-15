namespace E3Studio.Models;

/// <summary>
/// Represents a material with recommended cutting parameters
/// </summary>
public class Material
{
    public int Id { get; set; }
    public string Name { get; set; } = "Unknown Material";
    public string Category { get; set; } = "Wood";
    public string ImagePath { get; set; } = "";
    
    // Recommended cutting parameters (for 3mm endmill as baseline)
    public double FeedRate { get; set; } = 800;       // mm/min
    public double PlungeRate { get; set; } = 200;     // mm/min
    public double SpindleRPM { get; set; } = 10000;   // RPM
    public double DepthPerPass { get; set; } = 1.0;   // mm
    public double StepOver { get; set; } = 40;        // % of tool diameter
    
    public string Notes { get; set; } = "";

    public override string ToString() => Name;
}
