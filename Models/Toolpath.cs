namespace E3Studio.Models;

/// <summary>
/// Represents a generated toolpath for CNC machining
/// </summary>
public class Toolpath
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Toolpath 1";
    public ToolpathType Type { get; set; } = ToolpathType.Profile;
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    
    // Tool and material
    public Tool? Tool { get; set; }
    public int ToolId { get; set; } = 1;
    public string ToolName { get; set; } = "End Mill";
    public double ToolDiameter { get; set; } = 6.0;
    
    // Cutting parameters
    public double CutDepth { get; set; } = 2.0;         // Depth per pass (mm)
    public double FinalDepth { get; set; } = 10.0;      // Total depth (mm)
    public double FeedRate { get; set; } = 800;         // mm/min
    public double PlungeRate { get; set; } = 200;       // mm/min
    public double SpindleRPM { get; set; } = 10000;
    public double StepDown { get; set; } = 2.0;         // Same as CutDepth alias
    public double Offset { get; set; } = 0.0;           // Tool offset (for profile)
    public bool ClimbCut { get; set; } = true;          // Climb milling (vs conventional)
    
    // Profile specific
    public ProfileSide Side { get; set; } = ProfileSide.Outside;
    public double TabWidth { get; set; } = 5.0;         // Tab width (mm)
    public double TabHeight { get; set; } = 2.0;        // Tab height (mm)
    public int TabCount { get; set; } = 4;
    
    // Pocket specific
    public double StepOver { get; set; } = 40;          // % of tool diameter
    public PocketDirection Direction { get; set; } = PocketDirection.Climb;
    
    // Lead-in/out
    public double LeadInRadius { get; set; } = 0;
    public double LeadOutRadius { get; set; } = 0;
    public LeadType LeadInType { get; set; } = LeadType.None;
    public LeadType LeadOutType { get; set; } = LeadType.None;
    public double LeadAngle { get; set; } = 90;  // Degrees for tangent lead
    
    // Entry mode (how tool enters material)
    public EntryMode EntryMode { get; set; } = EntryMode.Plunge;
    public double HelixDiameter { get; set; } = 5.0;    // Helix diameter (mm)
    public double RampAngle { get; set; } = 3.0;        // Ramp angle (degrees)
    public double HelixAngle { get; set; } = 2.0;       // Helix descent angle (degrees)
    
    // V-Carve specific
    public double VCarveMaxDepth { get; set; } = 5.0;   // Max carve depth (mm)
    public double VCarveFlatDepth { get; set; } = 0.0;  // Flat bottom depth (0 = sharp point)
    
    // Arc fitting
    public bool UseArcFitting { get; set; } = true;     // Convert lines to arcs where possible
    public double ArcTolerance { get; set; } = 0.01;    // Arc fitting tolerance (mm)
    
    // Generated path points
    public List<ToolpathMove> Moves { get; set; } = new();
    
    // Source geometry
    public List<string> SourcePathIds { get; set; } = new();
}

public enum ToolpathType
{
    Profile,
    Pocket,
    Drill,
    VCarve,
    Trace,
    Engrave,
    Facing,
    Contour,
    Adaptive
}

public enum ProfileSide
{
    Outside,
    Inside,
    OnLine
}

public enum PocketDirection
{
    Climb,     // Cutting edge enters material first (better finish)
    Conventional
}

/// <summary>
/// A single move in a toolpath
/// </summary>
public class ToolpathMove
{
    public MoveType Type { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double F { get; set; } // Feed rate for this move
    public double FeedRate { get => F; set => F = value; } // Alias for F
    
    // For arcs
    public double I { get; set; } // Arc center offset X
    public double J { get; set; } // Arc center offset Y
}

public enum MoveType
{
    Rapid,      // G0
    Linear,     // G1
    ArcCW,      // G2
    ArcCCW,     // G3
    Feed,       // G1 feed move (alias for Linear)
    Plunge,     // G1 plunge move (Z- direction)
    Helix,      // Helical entry (G2/G3 with Z)
    Ramp,       // Ramped entry (G1 with gradual Z)
    Cut         // Cutting move (alias for Linear)
}

/// <summary>
/// Lead-in/Lead-out types
/// </summary>
public enum LeadType
{
    None,       // Direct entry/exit
    Line,       // Straight line approach
    Arc,        // Circular arc approach
    Tangent,    // Tangent arc approach
    Ramp,       // Ramped entry
    Helix       // Helical entry
}

/// <summary>
/// Entry mode - how tool enters material
/// </summary>
public enum EntryMode
{
    Plunge,     // Direct vertical plunge
    Ramp,       // Linear ramped entry
    Helix,      // Helical/spiral entry
    PreDrill    // Requires pre-drilled hole
}
