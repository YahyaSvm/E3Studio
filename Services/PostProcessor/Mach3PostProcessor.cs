using System.Text;
using E3Studio.Models;

namespace E3Studio.Services.PostProcessor;

/// <summary>
/// Mach3 Post-Processor
/// </summary>
public class Mach3PostProcessor : PostProcessorBase
{
    public override string Name => "Mach3";
    public override string Description => "Mach3 CNC Controller";
    public override string FileExtension => ".tap";
    
    public override bool SupportsCannedCycles => true;
    public override bool SupportsSubprograms => true;
    public override bool UseLineNumbers => false;
    public override string CommentStart => "(";
    public override string CommentEnd => ")";
    
    public double SafeHeight { get; set; } = 25.0;
    
    protected override void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("(E3Studio CAM - Mach3 Post-Processor)");
        sb.AppendLine($"(Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        sb.AppendLine($"(Project: {project.Root.Name})");
        sb.AppendLine($"(Stock: {project.Stock.Width}x{project.Stock.Height}x{project.Stock.Thickness}mm)");
        sb.AppendLine();
        
        // Tool list
        var tools = toolpaths.Where(t => t.Tool != null).Select(t => t.Tool!).DistinctBy(t => t.Number).ToList();
        if (tools.Count > 0)
        {
            sb.AppendLine("(TOOLS REQUIRED:)");
            foreach (var tool in tools)
            {
                sb.AppendLine($"(T{tool.Number} - {tool.Name} Ø{tool.Diameter}mm)");
            }
            sb.AppendLine();
        }
    }
    
    protected override void GenerateSetup(StringBuilder sb)
    {
        sb.AppendLine("(MACHINE SETUP)");
        sb.AppendLine("G21 (Metric)");
        sb.AppendLine("G90 (Absolute)");
        sb.AppendLine("G17 (XY Plane)");
        sb.AppendLine("G40 (Cancel cutter comp)");
        sb.AppendLine("G49 (Cancel tool length comp)");
        sb.AppendLine("G80 (Cancel canned cycle)");
        sb.AppendLine("G54 (Work coordinate system 1)");
        sb.AppendLine("G94 (Feed per minute)");
        sb.AppendLine();
        sb.AppendLine("(SAFE START)");
        sb.AppendLine("M5 (Spindle off)");
        sb.AppendLine("M9 (Coolant off)");
        sb.AppendLine($"G0 Z{F(SafeHeight)} (Safe height)");
        sb.AppendLine();
    }
    
    protected override void GenerateToolpath(StringBuilder sb, Toolpath toolpath, Stock stock)
    {
        sb.AppendLine();
        sb.AppendLine($"(═════════════════════════════════════)");
        sb.AppendLine($"(OPERATION: {toolpath.Name})");
        sb.AppendLine($"(Type: {toolpath.Type})");
        sb.AppendLine($"(═════════════════════════════════════)");
        
        // Tool change
        if (toolpath.Tool != null)
        {
            sb.AppendLine();
            sb.AppendLine($"(TOOL CHANGE: {toolpath.Tool.Name})");
            Rapid(sb, z: SafeHeight);
            ToolChange(sb, toolpath.Tool.Number, toolpath.Tool.Name);
            sb.AppendLine($"G43 H{toolpath.Tool.Number} (Tool length comp)");
        }
        
        // Spindle
        SpindleOn(sb, (int)toolpath.SpindleRPM);
        sb.AppendLine($"G4 P2.0 (Dwell for spindle)");
        
        // Generate based on type
        if (toolpath.Type == ToolpathType.Drill && SupportsCannedCycles)
        {
            GenerateDrillCannedCycle(sb, toolpath);
        }
        else
        {
            GenerateStandardMoves(sb, toolpath);
        }
        
        Rapid(sb, z: SafeHeight);
    }
    
    private void GenerateDrillCannedCycle(StringBuilder sb, Toolpath toolpath)
    {
        if (toolpath.Moves == null || toolpath.Moves.Count == 0) return;
        
        // Extract drill points
        var drillPoints = toolpath.Moves
            .Where(m => m.Type == MoveType.Rapid && m.Z >= 0)
            .Select(m => (m.X, m.Y))
            .Distinct()
            .ToList();
        
        if (drillPoints.Count == 0) return;
        
        sb.AppendLine();
        sb.AppendLine($"(PECK DRILL CYCLE - {drillPoints.Count} holes)");
        
        // Position over first hole
        Rapid(sb, drillPoints[0].X, drillPoints[0].Y);
        
        // G83 peck drill
        double retractPlane = 2.0;
        sb.AppendLine($"G83 Z{F(-toolpath.FinalDepth)} R{F(retractPlane)} Q{F(toolpath.CutDepth)} F{Feed(toolpath.PlungeRate)}");
        
        // Remaining holes
        for (int i = 1; i < drillPoints.Count; i++)
        {
            sb.AppendLine($"X{F(drillPoints[i].X)} Y{F(drillPoints[i].Y)}");
        }
        
        sb.AppendLine("G80 (Cancel canned cycle)");
    }
    
    private void GenerateStandardMoves(StringBuilder sb, Toolpath toolpath)
    {
        if (toolpath.Moves == null) return;
        
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
    
    protected override void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("(═════════════════════════════════════)");
        sb.AppendLine("(PROGRAM END)");
        sb.AppendLine("(═════════════════════════════════════)");
        SpindleOff(sb);
        CoolantOff(sb);
        Rapid(sb, z: SafeHeight);
        sb.AppendLine("G0 X0 Y0 (Return to origin)");
        sb.AppendLine("G28 (Return to home)");
        ProgramEnd(sb);
    }
}

/// <summary>
/// Mach4 Post-Processor (updated version of Mach3)
/// </summary>
public class Mach4PostProcessor : Mach3PostProcessor
{
    public override string Name => "Mach4";
    public override string Description => "Mach4 Industrial CNC Controller";
    
    protected override void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("(E3Studio CAM - Mach4 Post-Processor)");
        sb.AppendLine($"(Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        sb.AppendLine($"(Project: {project.Root.Name})");
        sb.AppendLine();
    }
}
