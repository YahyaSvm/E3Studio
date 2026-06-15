using System.Text;
using E3Studio.Models;

namespace E3Studio.Services.PostProcessor;

/// <summary>
/// GRBL Post-Processor for Arduino-based CNC controllers
/// </summary>
public class GrblPostProcessor : PostProcessorBase
{
    public override string Name => "GRBL";
    public override string Description => "GRBL 1.1+ (Arduino/ESP32 CNC)";
    public override string FileExtension => ".nc";
    
    public override bool SupportsCannedCycles => false; // GRBL doesn't support canned cycles
    public override bool SupportsSubprograms => false;
    public override bool UseLineNumbers => false;
    public override string CommentStart => ";";
    public override string CommentEnd => "";
    
    // GRBL specific settings
    public bool UseMetric { get; set; } = true;
    public double SafeHeight { get; set; } = 25.0;
    public bool UseSoftLimits { get; set; } = true;
    
    protected override void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("; E3Studio CAM - GRBL Post-Processor");
        sb.AppendLine($"; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"; Project: {project.Root.Name}");
        sb.AppendLine($"; Stock: {project.Stock.Width}x{project.Stock.Height}x{project.Stock.Thickness}mm");
        sb.AppendLine();
        sb.AppendLine("; === GRBL SETTINGS ===");
        sb.AppendLine("; Ensure your GRBL settings match:");
        sb.AppendLine("; $13=0 (Report in mm)");
        sb.AppendLine("; $20=1 (Soft limits enabled)");
        sb.AppendLine("; $21=1 (Hard limits enabled)");
        sb.AppendLine("; $22=1 (Homing enabled)");
        sb.AppendLine();
    }
    
    protected override void GenerateSetup(StringBuilder sb)
    {
        sb.AppendLine("; === INITIALIZATION ===");
        sb.AppendLine(UseMetric ? "G21 ; Millimeters" : "G20 ; Inches");
        sb.AppendLine("G90 ; Absolute positioning");
        sb.AppendLine("G17 ; XY plane");
        sb.AppendLine("G94 ; Feed per minute");
        sb.AppendLine();
        sb.AppendLine("; Safe startup");
        sb.AppendLine("M5 ; Spindle off");
        sb.AppendLine($"G0 Z{F(SafeHeight)} ; Retract to safe height");
        sb.AppendLine();
    }
    
    protected override void GenerateToolpath(StringBuilder sb, Toolpath toolpath, Stock stock)
    {
        sb.AppendLine();
        sb.AppendLine($"; ──────────────────────────────────────");
        sb.AppendLine($"; Operation: {toolpath.Name}");
        sb.AppendLine($"; Type: {toolpath.Type}");
        if (toolpath.Tool != null)
        {
            sb.AppendLine($"; Tool: {toolpath.Tool.Name} (Ø{toolpath.Tool.Diameter}mm)");
        }
        sb.AppendLine($"; ──────────────────────────────────────");
        
        // Spindle on
        SpindleOn(sb, (int)toolpath.SpindleRPM);
        Dwell(sb, 2.0); // Wait for spindle
        
        // Process moves
        if (toolpath.Moves != null && toolpath.Moves.Count > 0)
        {
            foreach (var move in toolpath.Moves)
            {
                switch (move.Type)
                {
                    case MoveType.Rapid:
                        Rapid(sb, move.X, move.Y, move.Z);
                        break;
                    case MoveType.Linear:
                    case MoveType.Feed:
                    case MoveType.Plunge:
                        Linear(sb, move.X, move.Y, move.Z, move.F > 0 ? move.F : toolpath.FeedRate);
                        break;
                    case MoveType.ArcCW:
                        Arc(sb, true, move.X, move.Y, move.I, move.J, move.F > 0 ? move.F : toolpath.FeedRate);
                        break;
                    case MoveType.ArcCCW:
                        Arc(sb, false, move.X, move.Y, move.I, move.J, move.F > 0 ? move.F : toolpath.FeedRate);
                        break;
                }
            }
        }
        
        // Safe retract
        sb.AppendLine();
        Rapid(sb, z: SafeHeight);
    }
    
    protected override void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("; === PROGRAM END ===");
        SpindleOff(sb);
        Rapid(sb, z: SafeHeight);
        sb.AppendLine("G0 X0 Y0 ; Return to origin");
        sb.AppendLine("M30 ; Program end");
        sb.AppendLine("; === END ===");
    }
}
