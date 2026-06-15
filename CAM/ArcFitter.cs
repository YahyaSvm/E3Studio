using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Converts line segments to arcs (G2/G3) for smoother motion
/// </summary>
public class ArcFitter
{
    /// <summary>
    /// Fit arcs to line segments within tolerance
    /// </summary>
    public List<ToolpathMove> FitArcs(List<ToolpathMove> moves, ArcFitSettings settings)
    {
        if (moves.Count < 3) return moves;
        
        var result = new List<ToolpathMove>();
        int i = 0;
        
        while (i < moves.Count)
        {
            var currentMove = moves[i];
            
            // Keep rapids as-is
            if (currentMove.Type == MoveType.Rapid)
            {
                result.Add(currentMove);
                i++;
                continue;
            }
            
            // Try to fit an arc starting from current position
            var arcResult = TryFitArc(moves, i, settings);
            
            if (arcResult.Success && arcResult.SegmentsConsumed > 1)
            {
                result.Add(arcResult.ArcMove);
                i += arcResult.SegmentsConsumed;
            }
            else
            {
                result.Add(currentMove);
                i++;
            }
        }
        
        return result;
    }
    
    private ArcFitResult TryFitArc(List<ToolpathMove> moves, int startIndex, ArcFitSettings settings)
    {
        var result = new ArcFitResult { Success = false };
        
        // Need at least 3 points to fit an arc
        if (startIndex + 2 >= moves.Count) return result;
        
        // Get starting point
        Point3D start;
        if (startIndex > 0)
        {
            start = new Point3D(moves[startIndex - 1].X, moves[startIndex - 1].Y, moves[startIndex - 1].Z);
        }
        else
        {
            start = new Point3D(moves[startIndex].X, moves[startIndex].Y, moves[startIndex].Z);
        }
        
        // Find consecutive linear moves at same Z
        var points = new List<Point3D> { start };
        int endIndex = startIndex;
        double targetZ = moves[startIndex].Z;
        double targetF = moves[startIndex].F;
        
        while (endIndex < moves.Count)
        {
            var move = moves[endIndex];
            if (move.Type == MoveType.Rapid || Math.Abs(move.Z - targetZ) > 0.001)
                break;
            
            points.Add(new Point3D(move.X, move.Y, move.Z));
            endIndex++;
            
            // Limit arc to reasonable number of segments
            if (points.Count > settings.MaxSegmentsPerArc)
                break;
        }
        
        if (points.Count < 3) return result;
        
        // Try to fit circle through first, middle, and last points
        var circleResult = FitCircle(points[0], points[points.Count / 2], points[^1]);
        
        if (!circleResult.Success) return result;
        
        // Check if all points are within tolerance of the arc
        bool allWithinTolerance = true;
        foreach (var p in points)
        {
            double dist = Math.Sqrt((p.X - circleResult.CenterX) * (p.X - circleResult.CenterX) + 
                                   (p.Y - circleResult.CenterY) * (p.Y - circleResult.CenterY));
            if (Math.Abs(dist - circleResult.Radius) > settings.Tolerance)
            {
                allWithinTolerance = false;
                break;
            }
        }
        
        if (!allWithinTolerance) return result;
        
        // Check radius constraints
        if (circleResult.Radius < settings.MinRadius || circleResult.Radius > settings.MaxRadius)
            return result;
        
        // Determine arc direction
        bool isClockwise = DetermineDirection(points[0], points[points.Count / 2], points[^1]);
        
        // Calculate I, J (incremental distance from start to center)
        double i = circleResult.CenterX - start.X;
        double j = circleResult.CenterY - start.Y;
        
        result.Success = true;
        result.SegmentsConsumed = endIndex - startIndex;
        result.ArcMove = new ToolpathMove
        {
            Type = isClockwise ? MoveType.ArcCW : MoveType.ArcCCW,
            X = points[^1].X,
            Y = points[^1].Y,
            Z = targetZ,
            I = i,
            J = j,
            F = targetF
        };
        
        return result;
    }
    
    private CircleFitResult FitCircle(Point3D p1, Point3D p2, Point3D p3)
    {
        var result = new CircleFitResult { Success = false };
        
        // Using the perpendicular bisector method
        double ax = p1.X, ay = p1.Y;
        double bx = p2.X, by = p2.Y;
        double cx = p3.X, cy = p3.Y;
        
        double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        
        if (Math.Abs(d) < 0.0001) return result; // Points are collinear
        
        double ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
        double uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;
        
        double radius = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
        
        result.Success = true;
        result.CenterX = ux;
        result.CenterY = uy;
        result.Radius = radius;
        
        return result;
    }
    
    private bool DetermineDirection(Point3D p1, Point3D p2, Point3D p3)
    {
        // Cross product to determine rotation direction
        double cross = (p2.X - p1.X) * (p3.Y - p1.Y) - (p2.Y - p1.Y) * (p3.X - p1.X);
        return cross < 0; // Negative = clockwise in standard coordinate system
    }
    
    /// <summary>
    /// Split long arcs into smaller segments if they exceed max angle
    /// </summary>
    public List<ToolpathMove> SplitLongArcs(List<ToolpathMove> moves, double maxAngleDegrees)
    {
        var result = new List<ToolpathMove>();
        Point3D? prevPos = null;
        
        foreach (var move in moves)
        {
            if ((move.Type == MoveType.ArcCW || move.Type == MoveType.ArcCCW) && prevPos.HasValue)
            {
                var arcMoves = SplitArc(prevPos.Value, move, maxAngleDegrees);
                result.AddRange(arcMoves);
            }
            else
            {
                result.Add(move);
            }
            
            prevPos = new Point3D(move.X, move.Y, move.Z);
        }
        
        return result;
    }
    
    private List<ToolpathMove> SplitArc(Point3D start, ToolpathMove arcMove, double maxAngle)
    {
        var moves = new List<ToolpathMove>();
        
        double centerX = start.X + arcMove.I;
        double centerY = start.Y + arcMove.J;
        double radius = Math.Sqrt(arcMove.I * arcMove.I + arcMove.J * arcMove.J);
        
        double startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
        double endAngle = Math.Atan2(arcMove.Y - centerY, arcMove.X - centerX);
        
        double sweep;
        if (arcMove.Type == MoveType.ArcCW)
        {
            sweep = startAngle - endAngle;
            if (sweep <= 0) sweep += 2 * Math.PI;
        }
        else
        {
            sweep = endAngle - startAngle;
            if (sweep <= 0) sweep += 2 * Math.PI;
        }
        
        double maxAngleRad = maxAngle * Math.PI / 180;
        int segments = (int)Math.Ceiling(sweep / maxAngleRad);
        
        if (segments <= 1)
        {
            moves.Add(arcMove);
            return moves;
        }
        
        double angleStep = sweep / segments;
        if (arcMove.Type == MoveType.ArcCW) angleStep = -angleStep;
        
        double currentAngle = startAngle;
        double zStep = (arcMove.Z - start.Z) / segments;
        double currentZ = start.Z;
        double prevX = start.X, prevY = start.Y;
        
        for (int i = 0; i < segments; i++)
        {
            currentAngle += angleStep;
            currentZ += zStep;
            
            double newX = centerX + radius * Math.Cos(currentAngle);
            double newY = centerY + radius * Math.Sin(currentAngle);
            
            moves.Add(new ToolpathMove
            {
                Type = arcMove.Type,
                X = newX,
                Y = newY,
                Z = currentZ,
                I = centerX - prevX,
                J = centerY - prevY,
                F = arcMove.F
            });
            
            prevX = newX;
            prevY = newY;
        }
        
        return moves;
    }
    
    /// <summary>
    /// Convert all arcs to line segments (linearize)
    /// </summary>
    public List<ToolpathMove> LinearizeArcs(List<ToolpathMove> moves, double tolerance)
    {
        var result = new List<ToolpathMove>();
        Point3D? prevPos = null;
        
        foreach (var move in moves)
        {
            if ((move.Type == MoveType.ArcCW || move.Type == MoveType.ArcCCW) && prevPos.HasValue)
            {
                var linearMoves = LinearizeArc(prevPos.Value, move, tolerance);
                result.AddRange(linearMoves);
            }
            else
            {
                result.Add(move);
            }
            
            prevPos = new Point3D(move.X, move.Y, move.Z);
        }
        
        return result;
    }
    
    private List<ToolpathMove> LinearizeArc(Point3D start, ToolpathMove arcMove, double tolerance)
    {
        var moves = new List<ToolpathMove>();
        
        double centerX = start.X + arcMove.I;
        double centerY = start.Y + arcMove.J;
        double radius = Math.Sqrt(arcMove.I * arcMove.I + arcMove.J * arcMove.J);
        
        // Calculate number of segments based on tolerance
        // chord error ≈ radius * (1 - cos(angle/2))
        // For small angles: chord error ≈ radius * angle² / 8
        // segments = sweep / angle where angle = sqrt(8 * tolerance / radius)
        
        double startAngle = Math.Atan2(start.Y - centerY, start.X - centerX);
        double endAngle = Math.Atan2(arcMove.Y - centerY, arcMove.X - centerX);
        
        double sweep;
        if (arcMove.Type == MoveType.ArcCW)
        {
            sweep = startAngle - endAngle;
            if (sweep <= 0) sweep += 2 * Math.PI;
        }
        else
        {
            sweep = endAngle - startAngle;
            if (sweep <= 0) sweep += 2 * Math.PI;
        }
        
        double maxSegmentAngle = Math.Sqrt(8 * tolerance / radius);
        int segments = Math.Max(1, (int)Math.Ceiling(sweep / maxSegmentAngle));
        
        double angleStep = sweep / segments;
        if (arcMove.Type == MoveType.ArcCW) angleStep = -angleStep;
        
        double zStep = (arcMove.Z - start.Z) / segments;
        double currentAngle = startAngle;
        double currentZ = start.Z;
        
        for (int i = 0; i < segments; i++)
        {
            currentAngle += angleStep;
            currentZ += zStep;
            
            moves.Add(new ToolpathMove
            {
                Type = MoveType.Linear,
                X = centerX + radius * Math.Cos(currentAngle),
                Y = centerY + radius * Math.Sin(currentAngle),
                Z = currentZ,
                F = arcMove.F
            });
        }
        
        return moves;
    }
}

public class ArcFitSettings
{
    /// <summary>Maximum deviation from arc (mm)</summary>
    public double Tolerance { get; set; } = 0.01;
    
    /// <summary>Minimum arc radius (mm)</summary>
    public double MinRadius { get; set; } = 0.1;
    
    /// <summary>Maximum arc radius (mm)</summary>
    public double MaxRadius { get; set; } = 1000;
    
    /// <summary>Maximum segments to consider for single arc</summary>
    public int MaxSegmentsPerArc { get; set; } = 50;
}

public class ArcFitResult
{
    public bool Success { get; set; }
    public int SegmentsConsumed { get; set; }
    public ToolpathMove ArcMove { get; set; } = new();
}

public class CircleFitResult
{
    public bool Success { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius { get; set; }
}
