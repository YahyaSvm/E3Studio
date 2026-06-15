using System;
using System.Collections.Generic;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// V-Carve toolpath generator for engraving and artistic carving
/// </summary>
public class VCarveEngine
{
    /// <summary>
    /// Generate V-Carve toolpath for given paths
    /// </summary>
    public Toolpath GenerateVCarve(List<PolyPath> paths, VCarveSettings settings, Tool tool)
    {
        var toolpath = new Toolpath
        {
            Name = $"V-Carve {settings.MaxDepth:F2}mm",
            Type = ToolpathType.VCarve,
            ToolId = tool.Id,
            Tool = tool,
            CutDepth = settings.MaxDepth,
            SpindleRPM = settings.SpindleRPM,
            FeedRate = settings.FeedRate,
            PlungeRate = settings.PlungeRate
        };
        
        // V-bit angle in radians
        double halfAngle = (tool.Angle / 2) * Math.PI / 180;
        double tipRadius = tool.TipDiameter / 2;
        
        foreach (var path in paths)
        {
            if (path.Segments.Count < 2) continue;
            
            // Calculate centerline with variable depth based on path width
            var vCarveSegments = CalculateVCarvePath(path, settings, halfAngle, tipRadius);
            
            foreach (var segment in vCarveSegments)
            {
                // Rapid to start position
                toolpath.Moves.Add(new ToolpathMove
                {
                    Type = MoveType.Rapid,
                    X = segment.StartX,
                    Y = segment.StartY,
                    Z = settings.SafeHeight
                });
                
                // Plunge to start depth
                toolpath.Moves.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = segment.StartX,
                    Y = segment.StartY,
                    Z = segment.StartZ,
                    F = settings.PlungeRate
                });
                
                // Cut along segment with variable depth
                if (segment.IsArc)
                {
                    toolpath.Moves.Add(new ToolpathMove
                    {
                        Type = segment.Clockwise ? MoveType.ArcCW : MoveType.ArcCCW,
                        X = segment.EndX,
                        Y = segment.EndY,
                        Z = segment.EndZ,
                        I = segment.I,
                        J = segment.J,
                        F = settings.FeedRate
                    });
                }
                else
                {
                    toolpath.Moves.Add(new ToolpathMove
                    {
                        Type = MoveType.Linear,
                        X = segment.EndX,
                        Y = segment.EndY,
                        Z = segment.EndZ,
                        F = settings.FeedRate
                    });
                }
            }
        }
        
        // Retract to safe height
        if (toolpath.Moves.Count > 0)
        {
            var lastMove = toolpath.Moves[^1];
            toolpath.Moves.Add(new ToolpathMove
            {
                Type = MoveType.Rapid,
                X = lastMove.X,
                Y = lastMove.Y,
                Z = settings.SafeHeight
            });
        }
        
        return toolpath;
    }
    
    private List<VCarveSegment> CalculateVCarvePath(PolyPath path, VCarveSettings settings, double halfAngle, double tipRadius)
    {
        var segments = new List<VCarveSegment>();
        var points = GetPathPoints(path);
        
        if (points.Count < 2) return segments;
        
        // For each point, calculate the width at that location
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            
            // Calculate perpendicular distance to neighboring paths (simplified)
            // In a full implementation, this would use Voronoi diagram or medial axis
            double width1 = EstimatePathWidth(p1, path, points, i);
            double width2 = EstimatePathWidth(p2, path, points, i + 1);
            
            // Calculate depth based on width and V-bit angle
            double depth1 = CalculateDepth(width1, halfAngle, tipRadius, settings.MaxDepth);
            double depth2 = CalculateDepth(width2, halfAngle, tipRadius, settings.MaxDepth);
            
            // Apply flat bottom if specified
            if (settings.FlatDepth > 0)
            {
                depth1 = Math.Min(depth1, settings.FlatDepth);
                depth2 = Math.Min(depth2, settings.FlatDepth);
            }
            
            segments.Add(new VCarveSegment
            {
                StartX = p1.X,
                StartY = p1.Y,
                StartZ = -depth1,
                EndX = p2.X,
                EndY = p2.Y,
                EndZ = -depth2,
                IsArc = false
            });
        }
        
        return segments;
    }
    
    private double EstimatePathWidth(Point2D point, PolyPath currentPath, List<Point2D> points, int index)
    {
        // Simple width estimation based on segment angles
        // Full implementation would use proper medial axis transform
        
        double minWidth = 2.0; // Default minimum width
        
        if (points.Count < 3) return minWidth;
        
        // Calculate angle at corner
        if (index > 0 && index < points.Count - 1)
        {
            var prev = points[index - 1];
            var curr = points[index];
            var next = points[index + 1];
            
            double angle1 = Math.Atan2(curr.Y - prev.Y, curr.X - prev.X);
            double angle2 = Math.Atan2(next.Y - curr.Y, next.X - curr.X);
            double cornerAngle = Math.Abs(angle2 - angle1);
            
            // Sharper corners = narrower effective width
            if (cornerAngle > Math.PI) cornerAngle = 2 * Math.PI - cornerAngle;
            minWidth = Math.Max(0.5, minWidth * Math.Sin(cornerAngle / 2));
        }
        
        return minWidth;
    }
    
    private double CalculateDepth(double width, double halfAngle, double tipRadius, double maxDepth)
    {
        // Calculate depth needed to achieve the given width with V-bit
        // width = 2 * (depth * tan(halfAngle) + tipRadius)
        // depth = (width/2 - tipRadius) / tan(halfAngle)
        
        double effectiveWidth = width / 2 - tipRadius;
        if (effectiveWidth <= 0) return 0.1; // Minimum depth
        
        double depth = effectiveWidth / Math.Tan(halfAngle);
        return Math.Min(depth, maxDepth);
    }
    
    private List<Point2D> GetPathPoints(PolyPath path)
    {
        var points = new List<Point2D>();
        foreach (var segment in path.Segments)
        {
            points.Add(segment.EndPoint);
        }
        return points;
    }
    
    /// <summary>
    /// Generate flat-bottom V-Carve for larger areas
    /// </summary>
    public Toolpath GenerateFlatBottomVCarve(List<PolyPath> paths, VCarveSettings settings, Tool vBit, Tool flatTool)
    {
        var toolpath = new Toolpath
        {
            Name = $"Flat-Bottom V-Carve {settings.FlatDepth:F2}mm",
            Type = ToolpathType.VCarve,
            ToolId = vBit.Id,
            Tool = vBit,
            CutDepth = settings.FlatDepth
        };
        
        // First pass: V-bit for edges
        var vCarve = GenerateVCarve(paths, settings, vBit);
        toolpath.Moves.AddRange(vCarve.Moves);
        
        // Second pass: Flat endmill for clearing center (would need pocket toolpath)
        // This is a simplified version - full implementation would create proper pocket
        
        return toolpath;
    }
}

public class VCarveSettings
{
    /// <summary>Maximum carve depth (mm)</summary>
    public double MaxDepth { get; set; } = 5.0;
    
    /// <summary>Safe height for rapids (mm)</summary>
    public double SafeHeight { get; set; } = 5.0;
    
    /// <summary>Flat bottom depth - 0 for pure V-carve (mm)</summary>
    public double FlatDepth { get; set; } = 0;
    
    /// <summary>Cutting feed rate (mm/min)</summary>
    public double FeedRate { get; set; } = 1000;
    
    /// <summary>Plunge rate (mm/min)</summary>
    public double PlungeRate { get; set; } = 300;
    
    /// <summary>Spindle RPM</summary>
    public int SpindleRPM { get; set; } = 18000;
    
    /// <summary>Stepover percentage for clearing passes</summary>
    public double StepoverPercent { get; set; } = 40;
}

public class VCarveSegment
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    public bool IsArc { get; set; }
    public bool Clockwise { get; set; }
    public double I { get; set; }
    public double J { get; set; }
}
