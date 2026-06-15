using System.Text;
using E3Studio.Models;

namespace E3Studio.Services.PostProcessor;

/// <summary>
/// LinuxCNC Post-Processor
/// </summary>
public class LinuxCNCPostProcessor : PostProcessorBase
{
    public override string Name => "LinuxCNC";
    public override string Description => "LinuxCNC (EMC2)";
    public override string FileExtension => ".ngc";
    
    public override bool SupportsCannedCycles => true;
    public override bool SupportsSubprograms => true;
    public override string CommentStart => "(";
    public override string CommentEnd => ")";
    
    public double SafeHeight { get; set; } = 25.0;
    
    protected override void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("(E3Studio CAM - LinuxCNC Post-Processor)");
        sb.AppendLine($"(Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        sb.AppendLine($"(Project: {project.Root.Name})");
        sb.AppendLine($"(Stock: {project.Stock.Width}x{project.Stock.Height}x{project.Stock.Thickness}mm)");
        sb.AppendLine();
        
        // LinuxCNC specific
        sb.AppendLine("(LinuxCNC specific settings:)");
        sb.AppendLine("(Adaptive Feed: G64 Pn - Path blending)");
        sb.AppendLine("(Exact Stop: G61)");
        sb.AppendLine();
    }
    
    protected override void GenerateSetup(StringBuilder sb)
    {
        sb.AppendLine("(MACHINE SETUP)");
        sb.AppendLine("G21 (Metric - mm)");
        sb.AppendLine("G90 (Absolute positioning)");
        sb.AppendLine("G17 (XY plane for arcs)");
        sb.AppendLine("G40 (Cancel cutter compensation)");
        sb.AppendLine("G49 (Cancel tool length offset)");
        sb.AppendLine("G80 (Cancel canned cycles)");
        sb.AppendLine("G54 (Work coordinate system 1)");
        sb.AppendLine("G64 P0.01 (Path blending - 0.01mm tolerance)");
        sb.AppendLine();
        sb.AppendLine("(SAFE STARTUP)");
        sb.AppendLine("M5 (Spindle stop)");
        sb.AppendLine("M9 (Coolant off)");
        sb.AppendLine($"G0 Z{F(SafeHeight)} (Safe retract)");
        sb.AppendLine();
    }
    
    protected override void GenerateToolpath(StringBuilder sb, Toolpath toolpath, Stock stock)
    {
        sb.AppendLine();
        sb.AppendLine($"(──────────────────────────────────────)");
        sb.AppendLine($"(OPERATION: {toolpath.Name})");
        sb.AppendLine($"(Type: {toolpath.Type} | Depth: {toolpath.FinalDepth}mm)");
        if (toolpath.Tool != null)
        {
            sb.AppendLine($"(Tool: T{toolpath.Tool.Number} {toolpath.Tool.Name} Ø{toolpath.Tool.Diameter}mm)");
        }
        sb.AppendLine($"(──────────────────────────────────────)");
        
        // Tool change
        if (toolpath.Tool != null)
        {
            sb.AppendLine();
            Rapid(sb, z: SafeHeight);
            ToolChange(sb, toolpath.Tool.Number, toolpath.Tool.Name);
            sb.AppendLine($"G43 H{toolpath.Tool.Number} (Tool length compensation)");
        }
        
        // Spindle
        SpindleOn(sb, (int)toolpath.SpindleRPM);
        Dwell(sb, 2.0);
        
        // Generate moves
        if (toolpath.Moves != null)
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
        
        Rapid(sb, z: SafeHeight);
    }
    
    protected override void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("(PROGRAM END)");
        SpindleOff(sb);
        CoolantOff(sb);
        Rapid(sb, z: SafeHeight);
        sb.AppendLine("G0 X0 Y0 (Return to origin)");
        sb.AppendLine("M2 (Program end)");
        sb.AppendLine("%");
    }
}
