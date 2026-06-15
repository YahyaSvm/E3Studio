using System.Text;
using E3Studio.Models;

namespace E3Studio.Services.PostProcessor;

/// <summary>
/// Generic Fanuc-style Post-Processor (base for industrial)
/// </summary>
public class GenericPostProcessor : PostProcessorBase
{
    public override string Name => "Generic";
    public override string Description => "Generic Fanuc-style G-Code";
    public override string FileExtension => ".nc";
    
    public double SafeHeight { get; set; } = 25.0;
    
    protected override void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("%");
        sb.AppendLine("O0001 (E3STUDIO CAM)");
        sb.AppendLine($"(Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        sb.AppendLine($"(Project: {project.Root.Name})");
        sb.AppendLine();
    }
    
    protected override void GenerateSetup(StringBuilder sb)
    {
        sb.AppendLine("(MACHINE SETUP)");
        sb.AppendLine("G21 G90 G17 G40 G49 G80");
        sb.AppendLine("G54");
        sb.AppendLine("M5 M9");
        sb.AppendLine($"G0 Z{F(SafeHeight)}");
        sb.AppendLine();
    }
    
    protected override void GenerateToolpath(StringBuilder sb, Toolpath toolpath, Stock stock)
    {
        sb.AppendLine();
        sb.AppendLine($"({toolpath.Name})");
        
        if (toolpath.Tool != null)
        {
            ToolChange(sb, toolpath.Tool.Number);
            sb.AppendLine($"G43 H{toolpath.Tool.Number}");
        }
        
        SpindleOn(sb, (int)toolpath.SpindleRPM);
        Dwell(sb, 2.0);
        
        if (toolpath.Moves != null)
        {
            foreach (var move in toolpath.Moves)
            {
                switch (move.Type)
                {
                    case MoveType.Rapid:
                        Rapid(sb, move.X, move.Y, move.Z);
                        break;
                    default:
                        Linear(sb, move.X, move.Y, move.Z, move.F > 0 ? move.F : toolpath.FeedRate);
                        break;
                }
            }
        }
        
        Rapid(sb, z: SafeHeight);
    }
    
    protected override void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine();
        SpindleOff(sb);
        CoolantOff(sb);
        Rapid(sb, z: SafeHeight);
        sb.AppendLine("G0 X0 Y0");
        sb.AppendLine("G28");
        sb.AppendLine("M30");
        sb.AppendLine("%");
    }
}

/// <summary>
/// Fanuc Post-Processor
/// </summary>
public class FanucPostProcessor : PostProcessorBase
{
    public override string Name => "Fanuc";
    public override string Description => "Fanuc CNC Controller";
    public override string FileExtension => ".nc";
    
    public override bool UseLineNumbers => true;
    public override int LineNumberIncrement => 5;
    
    public double SafeHeight { get; set; } = 50.0;
    
    protected override void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("%");
        sb.AppendLine(Line("O0001"));
        sb.AppendLine(Line($"(E3STUDIO - {project.Root.Name})"));
        sb.AppendLine(Line($"(DATE: {DateTime.Now:yyyy-MM-dd})"));
        sb.AppendLine();
    }
    
    protected override void GenerateSetup(StringBuilder sb)
    {
        sb.AppendLine(Line("G21 G90 G17 G40 G49 G80"));
        sb.AppendLine(Line("G54"));
        sb.AppendLine(Line("M5 M9"));
        sb.AppendLine(Line($"G0 Z{F(SafeHeight)}"));
        sb.AppendLine();
    }
    
    protected override void GenerateToolpath(StringBuilder sb, Toolpath toolpath, Stock stock)
    {
        sb.AppendLine();
        sb.AppendLine(Line($"({toolpath.Name})"));
        
        if (toolpath.Tool != null)
        {
            sb.AppendLine(Line($"T{toolpath.Tool.Number} M6"));
            sb.AppendLine(Line($"G43 H{toolpath.Tool.Number}"));
        }
        
        sb.AppendLine(Line($"M3 S{(int)toolpath.SpindleRPM}"));
        sb.AppendLine(Line("G4 P2000"));
        
        if (toolpath.Moves != null)
        {
            foreach (var move in toolpath.Moves)
            {
                switch (move.Type)
                {
                    case MoveType.Rapid:
                        sb.AppendLine(Line($"G0 X{F(move.X)} Y{F(move.Y)} Z{F(move.Z)}"));
                        break;
                    case MoveType.ArcCW:
                        sb.AppendLine(Line($"G2 X{F(move.X)} Y{F(move.Y)} I{F(move.I)} J{F(move.J)} F{Feed(toolpath.FeedRate)}"));
                        break;
                    case MoveType.ArcCCW:
                        sb.AppendLine(Line($"G3 X{F(move.X)} Y{F(move.Y)} I{F(move.I)} J{F(move.J)} F{Feed(toolpath.FeedRate)}"));
                        break;
                    default:
                        sb.AppendLine(Line($"G1 X{F(move.X)} Y{F(move.Y)} Z{F(move.Z)} F{Feed(move.F > 0 ? move.F : toolpath.FeedRate)}"));
                        break;
                }
            }
        }
        
        sb.AppendLine(Line($"G0 Z{F(SafeHeight)}"));
    }
    
    protected override void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine(Line("M5"));
        sb.AppendLine(Line("M9"));
        sb.AppendLine(Line($"G0 Z{F(SafeHeight)}"));
        sb.AppendLine(Line("G0 X0 Y0"));
        sb.AppendLine(Line("G28 G91 Z0"));
        sb.AppendLine(Line("G28 X0 Y0"));
        sb.AppendLine(Line("M30"));
        sb.AppendLine("%");
    }
}

/// <summary>
/// HAAS Post-Processor
/// </summary>
public class HaasPostProcessor : PostProcessorBase
{
    public override string Name => "HAAS";
    public override string Description => "HAAS CNC Mills";
    public override string FileExtension => ".nc";
    
    public override bool UseLineNumbers => false;
    public override string CommentStart => "(";
    public override string CommentEnd => ")";
    
    public double SafeHeight { get; set; } = 50.0;
    
    protected override void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("%");
        sb.AppendLine("O00001 (E3STUDIO CAM - HAAS)");
        sb.AppendLine($"(PROJECT: {project.Root.Name})");
        sb.AppendLine($"(DATE: {DateTime.Now:yyyy-MM-dd HH:mm:ss})");
        sb.AppendLine($"(STOCK: {project.Stock.Width}x{project.Stock.Height}x{project.Stock.Thickness}MM)");
        sb.AppendLine();
        
        var tools = toolpaths.Where(t => t.Tool != null).Select(t => t.Tool!).DistinctBy(t => t.Number).ToList();
        if (tools.Count > 0)
        {
            sb.AppendLine("(TOOL LIST:)");
            foreach (var tool in tools)
            {
                sb.AppendLine($"(T{tool.Number} - {tool.Name} - DIA {tool.Diameter}MM)");
            }
            sb.AppendLine();
        }
    }
    
    protected override void GenerateSetup(StringBuilder sb)
    {
        sb.AppendLine("(MACHINE SETUP)");
        sb.AppendLine("G00 G17 G21 G40 G49 G80 G90");
        sb.AppendLine("G54 (WORK OFFSET)");
        sb.AppendLine($"G00 Z{F(SafeHeight)} (SAFE HEIGHT)");
        sb.AppendLine();
    }
    
    protected override void GenerateToolpath(StringBuilder sb, Toolpath toolpath, Stock stock)
    {
        sb.AppendLine();
        sb.AppendLine($"(════════════════════════════════════)");
        sb.AppendLine($"(OPERATION: {toolpath.Name})");
        sb.AppendLine($"(════════════════════════════════════)");
        
        if (toolpath.Tool != null)
        {
            sb.AppendLine();
            sb.AppendLine($"T{toolpath.Tool.Number} M06 ({toolpath.Tool.Name})");
            sb.AppendLine($"G43 H{toolpath.Tool.Number} Z{F(SafeHeight)}");
        }
        
        sb.AppendLine($"S{(int)toolpath.SpindleRPM} M03");
        sb.AppendLine("G04 P2.0");
        
        // HAAS-specific: Enable high-speed machining for profiles
        if (toolpath.Type == ToolpathType.Profile || toolpath.Type == ToolpathType.Pocket)
        {
            sb.AppendLine("G187 P2 (HIGH SPEED MACHINING - MEDIUM)");
        }
        
        if (toolpath.Moves != null)
        {
            foreach (var move in toolpath.Moves)
            {
                switch (move.Type)
                {
                    case MoveType.Rapid:
                        sb.AppendLine($"G00 X{F(move.X)} Y{F(move.Y)} Z{F(move.Z)}");
                        break;
                    case MoveType.ArcCW:
                        sb.AppendLine($"G02 X{F(move.X)} Y{F(move.Y)} I{F(move.I)} J{F(move.J)} F{Feed(toolpath.FeedRate)}");
                        break;
                    case MoveType.ArcCCW:
                        sb.AppendLine($"G03 X{F(move.X)} Y{F(move.Y)} I{F(move.I)} J{F(move.J)} F{Feed(toolpath.FeedRate)}");
                        break;
                    default:
                        sb.AppendLine($"G01 X{F(move.X)} Y{F(move.Y)} Z{F(move.Z)} F{Feed(move.F > 0 ? move.F : toolpath.FeedRate)}");
                        break;
                }
            }
        }
        
        sb.AppendLine($"G00 Z{F(SafeHeight)}");
    }
    
    protected override void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("(════════════════════════════════════)");
        sb.AppendLine("(PROGRAM END)");
        sb.AppendLine("(════════════════════════════════════)");
        sb.AppendLine("M05 (SPINDLE STOP)");
        sb.AppendLine("M09 (COOLANT OFF)");
        sb.AppendLine("G00 G90 G53 Z0. (MACHINE Z HOME)");
        sb.AppendLine("G00 G90 G53 X0. Y0. (MACHINE XY HOME)");
        sb.AppendLine("M30 (PROGRAM END)");
        sb.AppendLine("%");
    }
}

/// <summary>
/// Mazak Post-Processor
/// </summary>
public class MazakPostProcessor : PostProcessorBase
{
    public override string Name => "Mazak";
    public override string Description => "Mazak CNC (Mazatrol)";
    public override string FileExtension => ".eia";
    
    public override bool UseLineNumbers => true;
    public override int LineNumberIncrement => 10;
    
    public double SafeHeight { get; set; } = 50.0;
    
    protected override void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("%");
        sb.AppendLine(Line("(E3STUDIO CAM - MAZAK)"));
        sb.AppendLine(Line($"(PROJECT: {project.Root.Name})"));
        sb.AppendLine(Line($"(DATE: {DateTime.Now:yyyy-MM-dd})"));
        sb.AppendLine();
    }
    
    protected override void GenerateSetup(StringBuilder sb)
    {
        sb.AppendLine(Line("G21 G40 G80 G90"));
        sb.AppendLine(Line("G17"));
        sb.AppendLine(Line("G54"));
        sb.AppendLine(Line($"G0 Z{F(SafeHeight)}"));
        sb.AppendLine();
    }
    
    protected override void GenerateToolpath(StringBuilder sb, Toolpath toolpath, Stock stock)
    {
        sb.AppendLine();
        sb.AppendLine(Line($"({toolpath.Name})"));
        
        if (toolpath.Tool != null)
        {
            sb.AppendLine(Line($"T{toolpath.Tool.Number}"));
            sb.AppendLine(Line("M6"));
            sb.AppendLine(Line($"G43 H{toolpath.Tool.Number}"));
        }
        
        sb.AppendLine(Line($"M3 S{(int)toolpath.SpindleRPM}"));
        sb.AppendLine(Line("G4 P2."));
        
        if (toolpath.Moves != null)
        {
            foreach (var move in toolpath.Moves)
            {
                switch (move.Type)
                {
                    case MoveType.Rapid:
                        sb.AppendLine(Line($"G0 X{F(move.X)} Y{F(move.Y)} Z{F(move.Z)}"));
                        break;
                    case MoveType.ArcCW:
                        sb.AppendLine(Line($"G2 X{F(move.X)} Y{F(move.Y)} I{F(move.I)} J{F(move.J)} F{Feed(toolpath.FeedRate)}"));
                        break;
                    case MoveType.ArcCCW:
                        sb.AppendLine(Line($"G3 X{F(move.X)} Y{F(move.Y)} I{F(move.I)} J{F(move.J)} F{Feed(toolpath.FeedRate)}"));
                        break;
                    default:
                        sb.AppendLine(Line($"G1 X{F(move.X)} Y{F(move.Y)} Z{F(move.Z)} F{Feed(move.F > 0 ? move.F : toolpath.FeedRate)}"));
                        break;
                }
            }
        }
        
        sb.AppendLine(Line($"G0 Z{F(SafeHeight)}"));
    }
    
    protected override void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine(Line("M5"));
        sb.AppendLine(Line("M9"));
        sb.AppendLine(Line($"G0 Z{F(SafeHeight)}"));
        sb.AppendLine(Line("G91 G28 Z0"));
        sb.AppendLine(Line("G28 X0 Y0"));
        sb.AppendLine(Line("G90"));
        sb.AppendLine(Line("M30"));
        sb.AppendLine("%");
    }
}
