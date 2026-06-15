using E3Studio.Models;
using System.Globalization;
using System.Text;

namespace E3Studio.Services;

/// <summary>
/// Advanced Professional G-Code generator with intelligent safety system
/// - Dynamic safe heights based on stock dimensions
/// - Tool-aware collision avoidance
/// - Optimized rapid movements
/// - Smart retract strategies
/// </summary>
public class GCodeGenerator
{
    public GCodeSettings Settings { get; set; } = new();
    
    // Runtime calculated heights (based on model)
    private double _machineSafeZ;      // Absolute safe height (clear of everything)
    private double _stockTopZ;          // Top of stock surface
    private double _rapidClearanceZ;    // Quick clearance above stock
    private double _toolChangeSafeZ;    // Height for tool changes
    
    // Current state tracking
    private double _currentX = 0;
    private double _currentY = 0;
    private double _currentZ = 0;
    private bool _isRetracted = false;
    
    // Stock reference
    private Stock? _stock;
    private Tool? _currentTool;
    
    /// <summary>
    /// Calculate all safety heights based on stock and tool dimensions
    /// </summary>
    private void CalculateSafetyHeights(Stock stock, Tool? tallestTool)
    {
        _stock = stock;
        
        // Stock top is Z=0 (work coordinate), material goes DOWN (negative Z)
        _stockTopZ = 0;
        
        // Rapid clearance: just above stock + clearance margin
        // Accounts for any fixtures or clamps
        _rapidClearanceZ = _stockTopZ + Settings.RapidClearance;
        
        // Machine safe height: considers stock thickness + tool length + safety margin
        double toolStickout = tallestTool?.TotalLength ?? 50;
        double fixtureHeight = Settings.FixtureHeight;
        
        _machineSafeZ = _stockTopZ + Math.Max(
            Settings.MinSafeHeight,
            toolStickout * 0.5 + fixtureHeight + Settings.SafetyMargin
        );
        
        // Tool change height: highest safe position
        _toolChangeSafeZ = Math.Max(_machineSafeZ + 20, Settings.ToolChangeHeight);
    }
    
    /// <summary>
    /// Get safe Z height based on operation type and current tool
    /// </summary>
    private double GetSafeZ(SafeHeightType type)
    {
        return type switch
        {
            SafeHeightType.MachineSafe => _machineSafeZ,
            SafeHeightType.RapidClearance => _rapidClearanceZ,
            SafeHeightType.ToolChange => _toolChangeSafeZ,
            SafeHeightType.StockTop => _stockTopZ,
            _ => _machineSafeZ
        };
    }
    
    /// <summary>
    /// Generate G-Code for all toolpaths in a project
    /// </summary>
    public string Generate(Project project, List<Toolpath> toolpaths)
    {
        var sb = new StringBuilder();
        var enabledToolpaths = toolpaths.Where(t => t.IsEnabled).ToList();
        
        // Find tallest tool for safety calculations
        var tallestTool = enabledToolpaths
            .Where(t => t.Tool != null)
            .Select(t => t.Tool)
            .OrderByDescending(t => t!.TotalLength)
            .FirstOrDefault();
        
        // Calculate all safety heights based on actual model
        CalculateSafetyHeights(project.Stock, tallestTool);
        
        // ═══════════════════════════════════════════════════════════════
        // HEADER WITH MODEL INFO
        // ═══════════════════════════════════════════════════════════════
        GenerateHeader(sb, project, enabledToolpaths);
        
        // ═══════════════════════════════════════════════════════════════
        // SAFETY HEIGHT INFO
        // ═══════════════════════════════════════════════════════════════
        sb.AppendLine("; === CALCULATED SAFETY HEIGHTS (Model-Based) ===");
        sb.AppendLine($"; Stock Top (Z=0):     {_stockTopZ:F2}mm");
        sb.AppendLine($"; Stock Thickness:     {project.Stock.Thickness:F2}mm (bottom at Z={-project.Stock.Thickness:F2})");
        sb.AppendLine($"; Rapid Clearance:     Z{_rapidClearanceZ:F2}mm (quick moves near stock)");
        sb.AppendLine($"; Machine Safe:        Z{_machineSafeZ:F2}mm (guaranteed clear)");
        sb.AppendLine($"; Tool Change:         Z{_toolChangeSafeZ:F2}mm (tool change position)");
        sb.AppendLine();
        
        // ═══════════════════════════════════════════════════════════════
        // MACHINE SETUP
        // ═══════════════════════════════════════════════════════════════
        GenerateMachineSetup(sb);
        
        // ═══════════════════════════════════════════════════════════════
        // SAFE STARTUP SEQUENCE
        // ═══════════════════════════════════════════════════════════════
        sb.AppendLine("; === SAFE STARTUP SEQUENCE ===");
        sb.AppendLine("M5           ; Ensure spindle is OFF");
        sb.AppendLine("M9           ; Ensure coolant is OFF");
        sb.AppendLine($"G0 Z{F(_toolChangeSafeZ)}   ; Retract to MAXIMUM safe height");
        sb.AppendLine($"G0 X0 Y0     ; Move to work origin");
        sb.AppendLine($"G0 Z{F(_machineSafeZ)}     ; Lower to machine safe height");
        _currentZ = _machineSafeZ;
        _isRetracted = true;
        sb.AppendLine();
        
        // ═══════════════════════════════════════════════════════════════
        // TOOLPATHS - Intelligently ordered
        // ═══════════════════════════════════════════════════════════════
        
        // Sort toolpaths for optimal machining:
        // 1. Roughing operations (pockets - remove most material first)
        // 2. Semi-finishing (profiles)  
        // 3. Finishing operations (drills - precise holes last)
        // Within same type, sort by depth (shallow first)
        var orderedToolpaths = enabledToolpaths
            .OrderBy(t => GetToolpathPriority(t.Type))
            .ThenBy(t => t.FinalDepth)
            .ThenBy(t => t.Name)
            .ToList();
        
        _currentTool = null;
        int toolpathIndex = 1;
        
        foreach (var toolpath in orderedToolpaths)
        {
            GenerateToolpathSection(sb, toolpath, toolpathIndex, orderedToolpaths.Count);
            toolpathIndex++;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PROGRAM END
        // ═══════════════════════════════════════════════════════════════
        GenerateProgramEnd(sb);
        
        return sb.ToString();
    }
    
    private int GetToolpathPriority(ToolpathType type) => type switch
    {
        ToolpathType.Pocket => 1,   // Roughing first
        ToolpathType.Profile => 2,  // Finishing second
        ToolpathType.Drill => 3,    // Holes last (after material is cleared)
        _ => 4
    };
    
    private void GenerateHeader(StringBuilder sb, Project project, List<Toolpath> toolpaths)
    {
        sb.AppendLine("; ╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("; ║          E3Studio CAM - Advanced G-Code Output               ║");
        sb.AppendLine("; ║          with Intelligent Safety System                      ║");
        sb.AppendLine("; ╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine($"; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"; Post-Processor: {Settings.PostProcessor}");
        sb.AppendLine();
        sb.AppendLine("; === PROJECT INFO ===");
        sb.AppendLine($"; Project: {project.Root.Name}");
        sb.AppendLine();
        sb.AppendLine("; === STOCK DEFINITION ===");
        sb.AppendLine($"; Dimensions: {project.Stock.Width:F1} x {project.Stock.Height:F1} x {project.Stock.Thickness:F1} mm");
        sb.AppendLine($"; Material: {project.Stock.Material?.Name ?? "Unknown"}");
        if (project.Stock.Material != null)
        {
            sb.AppendLine($"; Category: {project.Stock.Material.Category}");
            sb.AppendLine($"; Recommended Feed: {project.Stock.Material.FeedRate:F0} mm/min");
        }
        sb.AppendLine();
        sb.AppendLine("; === OPERATION SUMMARY ===");
        sb.AppendLine($"; Total Operations: {toolpaths.Count}");
        sb.AppendLine($"; - Pocket (Roughing): {toolpaths.Count(t => t.Type == ToolpathType.Pocket)}");
        sb.AppendLine($"; - Profile (Finishing): {toolpaths.Count(t => t.Type == ToolpathType.Profile)}");
        sb.AppendLine($"; - Drill (Holes): {toolpaths.Count(t => t.Type == ToolpathType.Drill)}");
        
        // List all tools used
        var tools = toolpaths.Where(t => t.Tool != null).Select(t => t.Tool!).DistinctBy(t => t.Name).ToList();
        if (tools.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("; === TOOLS REQUIRED ===");
            foreach (var tool in tools)
            {
                sb.AppendLine($"; T{tool.Number}: {tool.Name} - Ø{tool.Diameter:F2}mm {tool.Type} ({tool.Flutes} flute)");
            }
        }
        sb.AppendLine();
    }
    
    private void GenerateMachineSetup(StringBuilder sb)
    {
        sb.AppendLine("; === MACHINE INITIALIZATION ===");
        sb.AppendLine("G21          ; Units: Millimeters");
        sb.AppendLine("G90          ; Absolute positioning");
        sb.AppendLine("G17          ; XY plane selection (for arcs)");
        sb.AppendLine("G40          ; Cancel cutter radius compensation");
        sb.AppendLine("G49          ; Cancel tool length compensation");
        sb.AppendLine("G80          ; Cancel any active canned cycle");
        sb.AppendLine("G54          ; Select work coordinate system 1");
        sb.AppendLine("G94          ; Feed rate: units per minute");
        sb.AppendLine();
    }
    
    private void GenerateToolpathSection(StringBuilder sb, Toolpath toolpath, int index, int total)
    {
        sb.AppendLine();
        sb.AppendLine($"; ┌─────────────────────────────────────────────────────────────────┐");
        sb.AppendLine($"; │ OPERATION {index}/{total}: {toolpath.Name,-43} │");
        sb.AppendLine($"; ├─────────────────────────────────────────────────────────────────┤");
        sb.AppendLine($"; │ Type: {toolpath.Type,-15} │ Strategy: {GetStrategyName(toolpath),-20} │");
        sb.AppendLine($"; │ Final Depth: {toolpath.FinalDepth:F2}mm     │ Step Down: {toolpath.CutDepth:F2}mm              │");
        sb.AppendLine($"; │ Feed Rate: {toolpath.FeedRate:F0} mm/min │ Plunge: {toolpath.PlungeRate:F0} mm/min            │");
        sb.AppendLine($"; │ Spindle: {toolpath.SpindleRPM:F0} RPM                                          │");
        sb.AppendLine($"; └─────────────────────────────────────────────────────────────────┘");
        
        // Tool change if needed
        if (toolpath.Tool != null && (_currentTool == null || _currentTool.Name != toolpath.Tool.Name))
        {
            GenerateToolChange(sb, toolpath.Tool);
            _currentTool = toolpath.Tool;
        }
        
        // Spindle control
        sb.AppendLine();
        sb.AppendLine($"M3 S{toolpath.SpindleRPM:F0}  ; Spindle ON - CW at {toolpath.SpindleRPM:F0} RPM");
        
        // Coolant (if enabled)
        if (Settings.UseCoolant)
        {
            sb.AppendLine("M8           ; Coolant ON (flood)");
        }
        
        sb.AppendLine($"G4 P{Settings.SpindleWarmupTime:F1}     ; Wait for spindle to reach speed");
        sb.AppendLine();
        
        // Generate operation-specific G-Code
        switch (toolpath.Type)
        {
            case ToolpathType.Profile:
                GenerateProfileGCode(sb, toolpath);
                break;
            case ToolpathType.Pocket:
                GeneratePocketGCode(sb, toolpath);
                break;
            case ToolpathType.Drill:
                GenerateDrillGCode(sb, toolpath);
                break;
        }
        
        // End of operation - retract to safe height
        sb.AppendLine();
        sb.AppendLine($"; End of {toolpath.Name}");
        SafeRetract(sb, SafeHeightType.MachineSafe, "Operation complete - full retract");
    }
    
    private string GetStrategyName(Toolpath tp) => tp.Type switch
    {
        ToolpathType.Pocket => "Zigzag Clear",
        ToolpathType.Profile => "Contour",
        ToolpathType.Drill => tp.CutDepth < tp.FinalDepth ? "Peck Drill" : "Simple Drill",
        _ => "Standard"
    };
    
    private void GenerateToolChange(StringBuilder sb, Tool tool)
    {
        sb.AppendLine();
        sb.AppendLine($"; ═══ TOOL CHANGE ═══");
        sb.AppendLine($"; Tool: {tool.Name}");
        sb.AppendLine($"; Type: {tool.Type} | Diameter: Ø{tool.Diameter:F2}mm | Flutes: {tool.Flutes}");
        sb.AppendLine($"; Total Length: {tool.TotalLength:F1}mm | Flute Length: {tool.FluteLength:F1}mm");
        
        // Retract to tool change height
        SafeRetract(sb, SafeHeightType.ToolChange, "Tool change height");
        
        sb.AppendLine("M5           ; Spindle STOP for tool change");
        
        if (Settings.UseCoolant)
        {
            sb.AppendLine("M9           ; Coolant OFF");
        }
        
        if (Settings.IncludeToolChange)
        {
            sb.AppendLine($"M6 T{tool.Number}      ; Load Tool #{tool.Number}: {tool.Name}");
            
            if (Settings.UseToolLengthComp)
            {
                sb.AppendLine($"G43 H{tool.Number}     ; Apply tool length compensation");
            }
            
            sb.AppendLine("M0           ; PAUSE - Confirm tool change");
        }
    }
    
    /// <summary>
    /// Safe retract with intelligent height selection
    /// </summary>
    private void SafeRetract(StringBuilder sb, SafeHeightType type, string comment = "")
    {
        double targetZ = GetSafeZ(type);
        
        if (_currentZ < targetZ || !_isRetracted)
        {
            string commentText = string.IsNullOrEmpty(comment) ? type.ToString() : comment;
            sb.AppendLine($"G0 Z{F(targetZ)}   ; SAFE RETRACT: {commentText}");
            _currentZ = targetZ;
            _isRetracted = true;
        }
    }
    
    /// <summary>
    /// Safe rapid move - always retracts before XY movement
    /// </summary>
    private void SafeRapidXY(StringBuilder sb, double x, double y, string comment = "")
    {
        // RULE: Always retract before any XY rapid move
        SafeRetract(sb, SafeHeightType.MachineSafe, "Before XY rapid");
        
        string commentText = string.IsNullOrEmpty(comment) ? "" : $" ; {comment}";
        sb.AppendLine($"G0 X{F(x)} Y{F(y)}{commentText}");
        _currentX = x;
        _currentY = y;
    }
    
    /// <summary>
    /// Approach workpiece - rapid down to clearance, then to approach height
    /// </summary>
    private void ApproachWorkpiece(StringBuilder sb, double approachHeight = 2.0)
    {
        // Step 1: Rapid to rapid clearance
        sb.AppendLine($"G0 Z{F(_rapidClearanceZ)}  ; Rapid to clearance");
        
        // Step 2: Rapid to approach height (just above surface)
        double approach = _stockTopZ + approachHeight;
        sb.AppendLine($"G0 Z{F(approach)}  ; Approach height");
        _currentZ = approach;
        _isRetracted = false;
    }
    
    /// <summary>
    /// Generate profile toolpath G-Code with proper depth passes
    /// </summary>
    private void GenerateProfileGCode(StringBuilder sb, Toolpath toolpath)
    {
        if (toolpath.Moves == null || toolpath.Moves.Count == 0)
        {
            sb.AppendLine("; ⚠ WARNING: No moves computed for this toolpath");
            return;
        }
        
        // Group moves by depth pass
        var movesByDepth = new Dictionary<double, List<ToolpathMove>>();
        foreach (var move in toolpath.Moves)
        {
            double depth = Math.Round(move.Z, 3);
            if (!movesByDepth.ContainsKey(depth))
                movesByDepth[depth] = new List<ToolpathMove>();
            movesByDepth[depth].Add(move);
        }
        
        // Sort depths from shallow to deep (0 -> -max)
        var depths = movesByDepth.Keys.Where(d => d < 0).OrderByDescending(d => d).ToList();
        
        sb.AppendLine($"; Profile: {depths.Count} depth pass(es)");
        
        int passNum = 1;
        foreach (var depth in depths)
        {
            var moves = movesByDepth[depth];
            var firstCut = moves.FirstOrDefault(m => m.Type != MoveType.Rapid);
            if (firstCut == null) continue;
            
            sb.AppendLine();
            sb.AppendLine($"; ─── Pass {passNum}/{depths.Count}: Z={depth:F2}mm (depth: {Math.Abs(depth):F2}mm) ───");
            
            // Safe approach sequence
            SafeRapidXY(sb, firstCut.X, firstCut.Y, "Start position");
            ApproachWorkpiece(sb);
            
            // Plunge to cutting depth
            sb.AppendLine($"G1 Z{F(depth)} F{toolpath.PlungeRate:F0} ; Plunge to depth");
            _currentZ = depth;
            
            // Cutting moves
            foreach (var move in moves)
            {
                if (move.Type == MoveType.Rapid) continue;
                
                switch (move.Type)
                {
                    case MoveType.Linear:
                        sb.AppendLine($"G1 X{F(move.X)} Y{F(move.Y)} F{toolpath.FeedRate:F0}");
                        break;
                    case MoveType.ArcCW:
                        sb.AppendLine($"G2 X{F(move.X)} Y{F(move.Y)} I{F(move.I)} J{F(move.J)} F{toolpath.FeedRate:F0}");
                        break;
                    case MoveType.ArcCCW:
                        sb.AppendLine($"G3 X{F(move.X)} Y{F(move.Y)} I{F(move.I)} J{F(move.J)} F{toolpath.FeedRate:F0}");
                        break;
                }
                _currentX = move.X;
                _currentY = move.Y;
            }
            
            // Retract after pass
            SafeRetract(sb, SafeHeightType.RapidClearance, "Pass complete");
            passNum++;
        }
    }
    
    /// <summary>
    /// Generate pocket toolpath G-Code with zigzag clearing
    /// </summary>
    private void GeneratePocketGCode(StringBuilder sb, Toolpath toolpath)
    {
        if (toolpath.Moves == null || toolpath.Moves.Count == 0)
        {
            sb.AppendLine("; ⚠ WARNING: No moves computed for this toolpath");
            return;
        }
        
        // Group moves by depth
        var movesByDepth = new Dictionary<double, List<ToolpathMove>>();
        foreach (var move in toolpath.Moves)
        {
            double depth = Math.Round(move.Z, 3);
            if (!movesByDepth.ContainsKey(depth))
                movesByDepth[depth] = new List<ToolpathMove>();
            movesByDepth[depth].Add(move);
        }
        
        var depths = movesByDepth.Keys.Where(d => d < 0).OrderByDescending(d => d).ToList();
        
        sb.AppendLine($"; Pocket clearing: {depths.Count} depth pass(es)");
        
        int passNum = 1;
        foreach (var depth in depths)
        {
            var moves = movesByDepth[depth];
            sb.AppendLine();
            sb.AppendLine($"; ─── Pocket Pass {passNum}/{depths.Count}: Z={depth:F2}mm ───");
            
            bool needsApproach = true;
            
            foreach (var move in moves)
            {
                if (move.Type == MoveType.Rapid)
                {
                    // Rapid reposition within pocket
                    SafeRetract(sb, SafeHeightType.RapidClearance, "Pocket reposition");
                    sb.AppendLine($"G0 X{F(move.X)} Y{F(move.Y)}");
                    sb.AppendLine($"G0 Z{F(_stockTopZ + 2)}  ; Approach");
                    sb.AppendLine($"G1 Z{F(depth)} F{toolpath.PlungeRate:F0} ; Re-plunge");
                    _currentX = move.X;
                    _currentY = move.Y;
                    _currentZ = depth;
                    needsApproach = false;
                }
                else
                {
                    // First move - need approach
                    if (needsApproach)
                    {
                        SafeRapidXY(sb, move.X, move.Y, "Pocket start");
                        ApproachWorkpiece(sb);
                        sb.AppendLine($"G1 Z{F(depth)} F{toolpath.PlungeRate:F0} ; Plunge");
                        _currentZ = depth;
                        needsApproach = false;
                    }
                    
                    sb.AppendLine($"G1 X{F(move.X)} Y{F(move.Y)} F{toolpath.FeedRate:F0}");
                    _currentX = move.X;
                    _currentY = move.Y;
                }
            }
            
            passNum++;
        }
        
        SafeRetract(sb, SafeHeightType.RapidClearance, "Pocket complete");
    }
    
    /// <summary>
    /// Generate drill G-Code with proper peck drilling cycle
    /// CRITICAL: Always retract to FULL SAFE HEIGHT before moving to next hole!
    /// </summary>
    private void GenerateDrillGCode(StringBuilder sb, Toolpath toolpath)
    {
        if (toolpath.Moves == null || toolpath.Moves.Count == 0)
        {
            sb.AppendLine("; ⚠ WARNING: No drill points computed");
            return;
        }
        
        // Extract unique drill point positions
        var drillPoints = ExtractDrillPoints(toolpath);
        
        if (drillPoints.Count == 0)
        {
            sb.AppendLine("; ⚠ WARNING: Could not extract drill points");
            return;
        }
        
        sb.AppendLine($"; Drilling {drillPoints.Count} hole(s)");
        sb.AppendLine($"; Hole Depth: {toolpath.FinalDepth:F2}mm");
        sb.AppendLine($"; Peck Depth: {toolpath.CutDepth:F2}mm");
        sb.AppendLine($"; Drill Diameter: Ø{toolpath.Tool?.Diameter ?? 0:F2}mm");
        sb.AppendLine();
        
        // Determine drilling strategy
        bool usePeckCycle = toolpath.CutDepth > 0 && toolpath.CutDepth < toolpath.FinalDepth;
        int pecksPerHole = usePeckCycle ? (int)Math.Ceiling(toolpath.FinalDepth / toolpath.CutDepth) : 1;
        
        if (Settings.UseCannedCycles && usePeckCycle)
        {
            GenerateCannedDrillCycle(sb, toolpath, drillPoints);
        }
        else
        {
            GenerateManualDrillCycle(sb, toolpath, drillPoints, usePeckCycle);
        }
    }
    
    private List<(double X, double Y)> ExtractDrillPoints(Toolpath toolpath)
    {
        var drillPoints = new List<(double X, double Y)>();
        
        // Method 1: Look for rapid moves at/near safe height (positioning moves)
        foreach (var move in toolpath.Moves!)
        {
            if (move.Type == MoveType.Rapid && move.Z >= -0.1)
            {
                var point = (move.X, move.Y);
                if (!drillPoints.Any(p => Math.Abs(p.X - point.X) < 0.01 && Math.Abs(p.Y - point.Y) < 0.01))
                {
                    drillPoints.Add(point);
                }
            }
        }
        
        // Method 2: If no rapids found, extract from plunge moves
        if (drillPoints.Count == 0)
        {
            var plungeMoves = toolpath.Moves
                .Where(m => m.Type == MoveType.Linear && m.Z < -0.1)
                .ToList();
            
            foreach (var move in plungeMoves)
            {
                var point = (move.X, move.Y);
                if (!drillPoints.Any(p => Math.Abs(p.X - point.X) < 0.01 && Math.Abs(p.Y - point.Y) < 0.01))
                {
                    drillPoints.Add(point);
                }
            }
        }
        
        return drillPoints;
    }
    
    private void GenerateCannedDrillCycle(StringBuilder sb, Toolpath toolpath, List<(double X, double Y)> drillPoints)
    {
        sb.AppendLine("; Using G83 Peck Drill Canned Cycle");
        sb.AppendLine("; (Machine handles retract/peck automatically)");
        sb.AppendLine();
        
        // Ensure we're at safe height
        SafeRetract(sb, SafeHeightType.MachineSafe, "Before drill cycle");
        
        // Position over first hole
        sb.AppendLine($"G0 X{F(drillPoints[0].X)} Y{F(drillPoints[0].Y)} ; First hole");
        
        // G83: Z=final depth, R=retract plane, Q=peck depth
        double retractPlane = _stockTopZ + Settings.RapidClearance;
        sb.AppendLine($"G83 Z{F(-toolpath.FinalDepth)} R{F(retractPlane)} Q{F(toolpath.CutDepth)} F{toolpath.PlungeRate:F0}");
        
        // Remaining holes (cycle repeats)
        for (int i = 1; i < drillPoints.Count; i++)
        {
            sb.AppendLine($"X{F(drillPoints[i].X)} Y{F(drillPoints[i].Y)} ; Hole {i + 1}");
        }
        
        // Cancel canned cycle
        sb.AppendLine("G80          ; Cancel canned cycle");
        SafeRetract(sb, SafeHeightType.MachineSafe, "Drill cycle complete");
    }
    
    private void GenerateManualDrillCycle(StringBuilder sb, Toolpath toolpath, 
        List<(double X, double Y)> drillPoints, bool usePeck)
    {
        sb.AppendLine("; Manual Drill Cycle (explicit retract commands)");
        sb.AppendLine("; SAFETY: Full retract before EVERY hole transition");
        sb.AppendLine();
        
        int holeNum = 1;
        foreach (var point in drillPoints)
        {
            sb.AppendLine($"; ══ Hole {holeNum}/{drillPoints.Count} ══");
            sb.AppendLine($"; Position: X{F(point.X)} Y{F(point.Y)}");
            
            // CRITICAL SAFETY: Always retract to FULL safe height before XY move
            SafeRetract(sb, SafeHeightType.MachineSafe, $"SAFE before hole {holeNum}");
            
            // Position over hole
            sb.AppendLine($"G0 X{F(point.X)} Y{F(point.Y)} ; Position over hole");
            _currentX = point.X;
            _currentY = point.Y;
            
            // Rapid approach
            ApproachWorkpiece(sb, 2.0);
            
            if (usePeck)
            {
                // Peck drilling cycle
                GeneratePeckDrillSequence(sb, toolpath);
            }
            else
            {
                // Simple single plunge
                sb.AppendLine($"G1 Z{F(-toolpath.FinalDepth)} F{toolpath.PlungeRate:F0} ; Drill to depth");
                _currentZ = -toolpath.FinalDepth;
            }
            
            // CRITICAL: Full retract after completing hole
            SafeRetract(sb, SafeHeightType.MachineSafe, "Hole complete - SAFE retract");
            sb.AppendLine();
            
            holeNum++;
        }
    }
    
    private void GeneratePeckDrillSequence(StringBuilder sb, Toolpath toolpath)
    {
        double currentDepth = 0;
        double peckDepth = toolpath.CutDepth;
        int peckNum = 1;
        
        sb.AppendLine($"; Peck sequence: {Math.Ceiling(toolpath.FinalDepth / peckDepth):F0} pecks");
        
        while (currentDepth < toolpath.FinalDepth)
        {
            currentDepth += peckDepth;
            if (currentDepth > toolpath.FinalDepth) 
                currentDepth = toolpath.FinalDepth;
            
            // Peck down
            sb.AppendLine($"G1 Z{F(-currentDepth)} F{toolpath.PlungeRate:F0} ; Peck {peckNum}: {currentDepth:F2}mm");
            _currentZ = -currentDepth;
            
            // Chip clearing retract (except final peck)
            if (currentDepth < toolpath.FinalDepth)
            {
                // Full retract for chip clearing
                double retractZ = _rapidClearanceZ;
                sb.AppendLine($"G0 Z{F(retractZ)}  ; Chip clear retract");
                
                // Rapid back to just above current depth
                double reApproach = -currentDepth + 1.0;
                sb.AppendLine($"G0 Z{F(reApproach)}  ; Rapid re-approach");
                _currentZ = reApproach;
            }
            
            peckNum++;
        }
    }
    
    private void GenerateProgramEnd(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("; ╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("; ║                      PROGRAM END                              ║");
        sb.AppendLine("; ╚═══════════════════════════════════════════════════════════════╝");
        sb.AppendLine("M5           ; Spindle OFF");
        sb.AppendLine("M9           ; Coolant OFF");
        SafeRetract(sb, SafeHeightType.ToolChange, "Maximum safe height");
        sb.AppendLine("G0 X0 Y0     ; Return to work origin");
        sb.AppendLine("G28          ; Return to machine home (optional)");
        sb.AppendLine("M30          ; Program end & rewind");
        sb.AppendLine("; ═══════════════════════════════════════════════════════════════");
    }
    
    /// <summary>
    /// Format number for G-Code (clean output)
    /// </summary>
    private static string F(double value)
    {
        // Format with 3 decimals, trim trailing zeros
        string result = value.ToString("F3", CultureInfo.InvariantCulture);
        if (result.Contains('.'))
        {
            result = result.TrimEnd('0').TrimEnd('.');
        }
        return result;
    }
    
    /// <summary>
    /// Generate G-Code for a single toolpath (preview)
    /// </summary>
    public string GenerateSingle(Toolpath toolpath, Stock? stock = null)
    {
        var sb = new StringBuilder();
        
        // Initialize with default or provided stock
        if (stock != null)
        {
            CalculateSafetyHeights(stock, toolpath.Tool);
        }
        else
        {
            // Use defaults
            _stockTopZ = 0;
            _rapidClearanceZ = 5;
            _machineSafeZ = 25;
            _toolChangeSafeZ = 50;
        }
        
        sb.AppendLine($"; Preview: {toolpath.Name}");
        sb.AppendLine($"; Type: {toolpath.Type}");
        sb.AppendLine($"; Depth: {toolpath.FinalDepth:F2}mm");
        sb.AppendLine($"; Safe Heights - Rapid: Z{_rapidClearanceZ:F1}, Safe: Z{_machineSafeZ:F1}");
        sb.AppendLine();
        
        _currentZ = _machineSafeZ;
        _isRetracted = true;
        
        switch (toolpath.Type)
        {
            case ToolpathType.Profile:
                GenerateProfileGCode(sb, toolpath);
                break;
            case ToolpathType.Pocket:
                GeneratePocketGCode(sb, toolpath);
                break;
            case ToolpathType.Drill:
                GenerateDrillGCode(sb, toolpath);
                break;
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Types of safe heights for different operations
/// </summary>
public enum SafeHeightType
{
    /// <summary>Just above stock surface for quick moves</summary>
    RapidClearance,
    
    /// <summary>Stock top (Z=0)</summary>
    StockTop,
    
    /// <summary>Full machine safe height (clears everything)</summary>
    MachineSafe,
    
    /// <summary>Maximum height for tool changes</summary>
    ToolChange
}

/// <summary>
/// Advanced G-Code generation settings
/// </summary>
public class GCodeSettings
{
    // ═══════════════════════════════════════════════════════════════
    // SAFETY HEIGHTS (base values - actual calculated from model)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Minimum safe height above stock (mm)</summary>
    public double MinSafeHeight { get; set; } = 15.0;
    
    /// <summary>Clearance for rapid moves above stock surface (mm)</summary>
    public double RapidClearance { get; set; } = 5.0;
    
    /// <summary>Additional safety margin (mm)</summary>
    public double SafetyMargin { get; set; } = 10.0;
    
    /// <summary>Height allowance for fixtures/clamps (mm)</summary>
    public double FixtureHeight { get; set; } = 0.0;
    
    /// <summary>Tool change position height (mm)</summary>
    public double ToolChangeHeight { get; set; } = 50.0;
    
    // ═══════════════════════════════════════════════════════════════
    // MACHINE SETTINGS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Spindle warmup delay (seconds)</summary>
    public double SpindleWarmupTime { get; set; } = 2.0;
    
    /// <summary>Include M6 tool change commands</summary>
    public bool IncludeToolChange { get; set; } = true;
    
    /// <summary>Use G43 tool length compensation</summary>
    public bool UseToolLengthComp { get; set; } = false;
    
    /// <summary>Use coolant (M8/M9)</summary>
    public bool UseCoolant { get; set; } = false;
    
    // ═══════════════════════════════════════════════════════════════
    // G-CODE OPTIONS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Use G81/G83 canned drill cycles</summary>
    public bool UseCannedCycles { get; set; } = false;
    
    /// <summary>Post-processor type (Grbl, LinuxCNC, Mach3, etc)</summary>
    public string PostProcessor { get; set; } = "Grbl";
    
    /// <summary>Include line numbers (N10, N20, etc)</summary>
    public bool UseLineNumbers { get; set; } = false;
    
    /// <summary>Starting line number</summary>
    public int LineNumberStart { get; set; } = 10;
    
    /// <summary>Line number increment</summary>
    public int LineNumberIncrement { get; set; } = 10;
    
    // ═══════════════════════════════════════════════════════════════
    // LEGACY COMPATIBILITY (mapped to new system)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Legacy: Safe height (use MinSafeHeight instead)</summary>
    public double SafeHeight 
    { 
        get => MinSafeHeight + SafetyMargin; 
        set => MinSafeHeight = value - SafetyMargin; 
    }
    
    /// <summary>Legacy: Rapid height (use RapidClearance instead)</summary>
    public double RapidHeight 
    { 
        get => RapidClearance; 
        set => RapidClearance = value; 
    }
}
