using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Generates holding tabs for securing workpiece during cutting
/// </summary>
public class TabGenerator
{
    /// <summary>
    /// Add tabs to a toolpath
    /// </summary>
    public List<ToolpathMove> AddTabsToMoves(List<ToolpathMove> originalMoves, TabSettings settings, double cutDepth)
    {
        if (settings.TabCount <= 0 || settings.TabHeight <= 0) return originalMoves;
        
        var result = new List<ToolpathMove>();
        
        // Calculate total path length for tab placement
        double totalLength = CalculatePathLength(originalMoves);
        if (totalLength < settings.TabWidth * settings.TabCount * 2) return originalMoves;
        
        // Calculate tab positions (evenly distributed)
        var tabPositions = CalculateTabPositions(totalLength, settings);
        
        // Process moves and insert tabs
        double accumulatedLength = 0;
        int currentTabIndex = 0;
        bool inTab = false;
        
        for (int i = 0; i < originalMoves.Count; i++)
        {
            var currentMove = originalMoves[i];
            
            if (currentMove.Type == MoveType.Rapid)
            {
                result.Add(currentMove);
                continue;
            }
            
            // Get previous position
            Point3D prevPos;
            if (i > 0)
            {
                prevPos = new Point3D(originalMoves[i - 1].X, originalMoves[i - 1].Y, originalMoves[i - 1].Z);
            }
            else
            {
                prevPos = new Point3D(currentMove.X, currentMove.Y, currentMove.Z);
            }
            
            double segmentLength = Distance(prevPos.X, prevPos.Y, currentMove.X, currentMove.Y);
            double segmentStart = accumulatedLength;
            double segmentEnd = accumulatedLength + segmentLength;
            
            // Check if any tabs fall within this segment
            var segmentMoves = ProcessSegmentWithTabs(
                prevPos, 
                new Point3D(currentMove.X, currentMove.Y, currentMove.Z),
                currentMove,
                segmentStart, 
                segmentEnd,
                tabPositions, 
                settings,
                cutDepth
            );
            
            result.AddRange(segmentMoves);
            accumulatedLength = segmentEnd;
        }
        
        return result;
    }
    
    private List<ToolpathMove> ProcessSegmentWithTabs(
        Point3D start, Point3D end, ToolpathMove originalMove,
        double segmentStart, double segmentEnd,
        List<TabPosition> tabPositions, TabSettings settings, double cutDepth)
    {
        var result = new List<ToolpathMove>();
        var segmentLength = segmentEnd - segmentStart;
        if (segmentLength < 0.001) return result;
        
        // Find tabs that intersect this segment
        var intersectingTabs = tabPositions
            .Where(t => t.EndPosition > segmentStart && t.StartPosition < segmentEnd)
            .OrderBy(t => t.StartPosition)
            .ToList();
        
        if (intersectingTabs.Count == 0)
        {
            result.Add(originalMove);
            return result;
        }
        
        // Direction vector
        double dx = (end.X - start.X) / segmentLength;
        double dy = (end.Y - start.Y) / segmentLength;
        
        // Tab height (Z position for tab top)
        double tabZ = -cutDepth + settings.TabHeight;
        
        double currentPos = segmentStart;
        foreach (var tab in intersectingTabs)
        {
            // Move to tab start (if not already there)
            if (tab.StartPosition > currentPos)
            {
                double distanceToTab = tab.StartPosition - currentPos;
                double newX = start.X + (tab.StartPosition - segmentStart) * dx;
                double newY = start.Y + (tab.StartPosition - segmentStart) * dy;
                
                result.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = newX,
                    Y = newY,
                    Z = originalMove.Z,
                    F = originalMove.F
                });
            }
            
            // Tab ramp up
            double tabStartX = start.X + (Math.Max(tab.StartPosition, segmentStart) - segmentStart) * dx;
            double tabStartY = start.Y + (Math.Max(tab.StartPosition, segmentStart) - segmentStart) * dy;
            
            // Raise to tab height
            result.Add(new ToolpathMove
            {
                Type = MoveType.Linear,
                X = tabStartX,
                Y = tabStartY,
                Z = tabZ,
                F = originalMove.F
            });
            
            // Move across tab at tab height
            double tabEndInSegment = Math.Min(tab.EndPosition, segmentEnd);
            double tabEndX = start.X + (tabEndInSegment - segmentStart) * dx;
            double tabEndY = start.Y + (tabEndInSegment - segmentStart) * dy;
            
            result.Add(new ToolpathMove
            {
                Type = MoveType.Linear,
                X = tabEndX,
                Y = tabEndY,
                Z = tabZ,
                F = originalMove.F
            });
            
            // Plunge back to cut depth
            result.Add(new ToolpathMove
            {
                Type = MoveType.Linear,
                X = tabEndX,
                Y = tabEndY,
                Z = originalMove.Z,
                F = originalMove.F / 2 // Slower plunge
            });
            
            currentPos = tabEndInSegment;
        }
        
        // Complete remaining segment if any
        if (currentPos < segmentEnd - 0.001)
        {
            result.Add(new ToolpathMove
            {
                Type = MoveType.Linear,
                X = end.X,
                Y = end.Y,
                Z = originalMove.Z,
                F = originalMove.F
            });
        }
        
        return result;
    }
    
    private List<TabPosition> CalculateTabPositions(double totalLength, TabSettings settings)
    {
        var positions = new List<TabPosition>();
        
        // Calculate spacing
        double spacing = totalLength / settings.TabCount;
        
        // Place tabs evenly
        for (int i = 0; i < settings.TabCount; i++)
        {
            double center = spacing * (i + 0.5);
            positions.Add(new TabPosition
            {
                StartPosition = center - settings.TabWidth / 2,
                EndPosition = center + settings.TabWidth / 2,
                CenterPosition = center
            });
        }
        
        return positions;
    }
    
    private double CalculatePathLength(List<ToolpathMove> moves)
    {
        double length = 0;
        for (int i = 1; i < moves.Count; i++)
        {
            if (moves[i].Type != MoveType.Rapid)
            {
                length += Distance(moves[i - 1].X, moves[i - 1].Y, moves[i].X, moves[i].Y);
            }
        }
        return length;
    }
    
    private double Distance(double x1, double y1, double x2, double y2) =>
        Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
    
    /// <summary>
    /// Calculate automatic tab positions based on geometry
    /// </summary>
    public List<TabPosition> CalculateAutoTabPositions(PolyPath path, TabSettings settings)
    {
        var positions = new List<TabPosition>();
        
        // Get path points
        var points = path.Segments.Select(s => s.EndPoint).ToList();
        if (points.Count < 2) return positions;
        
        // Calculate total length
        double totalLength = 0;
        for (int i = 1; i < points.Count; i++)
        {
            totalLength += Distance(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y);
        }
        
        // Distribute tabs evenly, preferring straight sections over corners
        double spacing = totalLength / settings.TabCount;
        
        for (int i = 0; i < settings.TabCount; i++)
        {
            double targetPos = spacing * (i + 0.5);
            
            // Find closest position that's not near a corner
            double bestPos = targetPos;
            double accum = 0;
            
            for (int j = 1; j < points.Count; j++)
            {
                double segLen = Distance(points[j - 1].X, points[j - 1].Y, points[j].X, points[j].Y);
                if (accum <= targetPos && targetPos <= accum + segLen)
                {
                    // Target is in this segment - check if near corner
                    double posInSegment = targetPos - accum;
                    double minDist = Math.Min(posInSegment, segLen - posInSegment);
                    
                    // Avoid placing too close to corners (within 10% of segment)
                    if (minDist < segLen * 0.1 && segLen > settings.TabWidth * 2)
                    {
                        bestPos = accum + segLen / 2; // Center of segment instead
                    }
                    break;
                }
                accum += segLen;
            }
            
            positions.Add(new TabPosition
            {
                CenterPosition = bestPos,
                StartPosition = bestPos - settings.TabWidth / 2,
                EndPosition = bestPos + settings.TabWidth / 2
            });
        }
        
        return positions;
    }
}

public class TabSettings
{
    /// <summary>Number of tabs to generate</summary>
    public int TabCount { get; set; } = 4;
    
    /// <summary>Tab width in mm</summary>
    public double TabWidth { get; set; } = 5.0;
    
    /// <summary>Tab height in mm</summary>
    public double TabHeight { get; set; } = 1.0;
    
    /// <summary>Use triangular (ramped) tabs instead of rectangular</summary>
    public bool TriangularTabs { get; set; } = true;
    
    /// <summary>Use 3D tabs (follow surface) instead of flat</summary>
    public bool Use3DTabs { get; set; } = false;
}

public class TabPosition
{
    public double StartPosition { get; set; }
    public double EndPosition { get; set; }
    public double CenterPosition { get; set; }
}
