using System;
using System.Collections.Generic;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Generates lead-in and lead-out moves for toolpaths
/// </summary>
public class LeadInOutGenerator
{
    /// <summary>
    /// Add lead-in move at the start of a cut
    /// </summary>
    public List<ToolpathMove> GenerateLeadIn(Point2D entryPoint, Point2D firstCutPoint, 
        LeadType type, LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        switch (type)
        {
            case LeadType.None:
                // Direct plunge
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Rapid,
                    X = firstCutPoint.X,
                    Y = firstCutPoint.Y,
                    Z = safeZ
                });
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Linear,
                    X = firstCutPoint.X,
                    Y = firstCutPoint.Y,
                    Z = cutZ,
                    F = settings.PlungeRate
                });
                break;
                
            case LeadType.Line:
                moves.AddRange(GenerateLinearLeadIn(firstCutPoint, settings, cutZ, safeZ));
                break;
                
            case LeadType.Arc:
                moves.AddRange(GenerateArcLeadIn(firstCutPoint, settings, cutZ, safeZ));
                break;
                
            case LeadType.Tangent:
                moves.AddRange(GenerateTangentLeadIn(entryPoint, firstCutPoint, settings, cutZ, safeZ));
                break;
                
            case LeadType.Ramp:
                moves.AddRange(GenerateRampLeadIn(firstCutPoint, settings, cutZ, safeZ));
                break;
                
            case LeadType.Helix:
                moves.AddRange(GenerateHelixLeadIn(firstCutPoint, settings, cutZ, safeZ));
                break;
        }
        
        return moves;
    }
    
    /// <summary>
    /// Add lead-out move at the end of a cut
    /// </summary>
    public List<ToolpathMove> GenerateLeadOut(Point2D lastCutPoint, Point2D exitDirection,
        LeadType type, LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        switch (type)
        {
            case LeadType.None:
                // Direct retract
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Rapid,
                    X = lastCutPoint.X,
                    Y = lastCutPoint.Y,
                    Z = safeZ
                });
                break;
                
            case LeadType.Line:
                moves.AddRange(GenerateLinearLeadOut(lastCutPoint, exitDirection, settings, cutZ, safeZ));
                break;
                
            case LeadType.Arc:
                moves.AddRange(GenerateArcLeadOut(lastCutPoint, exitDirection, settings, cutZ, safeZ));
                break;
                
            case LeadType.Tangent:
                moves.AddRange(GenerateTangentLeadOut(lastCutPoint, exitDirection, settings, cutZ, safeZ));
                break;
                
            default:
                moves.Add(new ToolpathMove
                {
                    Type = MoveType.Rapid,
                    X = lastCutPoint.X,
                    Y = lastCutPoint.Y,
                    Z = safeZ
                });
                break;
        }
        
        return moves;
    }
    
    private List<ToolpathMove> GenerateLinearLeadIn(Point2D target, LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        // Calculate lead-in start position (perpendicular to cut direction)
        double angle = settings.LeadAngle * Math.PI / 180;
        double offsetX = -settings.LeadLength * Math.Cos(angle);
        double offsetY = -settings.LeadLength * Math.Sin(angle);
        
        // Start position at safe height
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = target.X + offsetX,
            Y = target.Y + offsetY,
            Z = safeZ
        });
        
        // Plunge at lead-in start
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = target.X + offsetX,
            Y = target.Y + offsetY,
            Z = cutZ,
            F = settings.PlungeRate
        });
        
        // Linear approach to target
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = target.X,
            Y = target.Y,
            Z = cutZ,
            F = settings.FeedRate
        });
        
        return moves;
    }
    
    private List<ToolpathMove> GenerateArcLeadIn(Point2D target, LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        double radius = settings.LeadRadius;
        double angle = settings.LeadAngle * Math.PI / 180;
        
        // Arc start point
        double startX = target.X - radius * (1 - Math.Cos(angle));
        double startY = target.Y - radius * Math.Sin(angle);
        
        // Arc center (offset from target)
        double i = -radius;
        double j = 0;
        
        // Move to arc start
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = startX,
            Y = startY,
            Z = safeZ
        });
        
        // Plunge
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = startX,
            Y = startY,
            Z = cutZ,
            F = settings.PlungeRate
        });
        
        // Arc to target (clockwise)
        moves.Add(new ToolpathMove
        {
            Type = settings.ArcClockwise ? MoveType.ArcCW : MoveType.ArcCCW,
            X = target.X,
            Y = target.Y,
            Z = cutZ,
            I = i,
            J = j,
            F = settings.FeedRate
        });
        
        return moves;
    }
    
    private List<ToolpathMove> GenerateTangentLeadIn(Point2D entryPoint, Point2D target, 
        LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        // Calculate tangent direction
        double dx = target.X - entryPoint.X;
        double dy = target.Y - entryPoint.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length < 0.001)
        {
            // Fall back to linear
            return GenerateLinearLeadIn(target, settings, cutZ, safeZ);
        }
        
        // Normalize and offset
        dx /= length;
        dy /= length;
        
        // Start point is offset along tangent
        double startX = target.X - dx * settings.LeadLength;
        double startY = target.Y - dy * settings.LeadLength;
        
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = startX,
            Y = startY,
            Z = safeZ
        });
        
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = startX,
            Y = startY,
            Z = cutZ,
            F = settings.PlungeRate
        });
        
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = target.X,
            Y = target.Y,
            Z = cutZ,
            F = settings.FeedRate
        });
        
        return moves;
    }
    
    private List<ToolpathMove> GenerateRampLeadIn(Point2D target, LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        double rampLength = settings.LeadLength;
        double angle = settings.LeadAngle * Math.PI / 180;
        
        // Start point
        double startX = target.X - rampLength * Math.Cos(angle);
        double startY = target.Y - rampLength * Math.Sin(angle);
        
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = startX,
            Y = startY,
            Z = safeZ
        });
        
        // Move to just above surface
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = startX,
            Y = startY,
            Z = 0.5 // Just above surface
        });
        
        // Ramp down to target at cut depth
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = target.X,
            Y = target.Y,
            Z = cutZ,
            F = settings.RampRate
        });
        
        return moves;
    }
    
    private List<ToolpathMove> GenerateHelixLeadIn(Point2D target, LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        double radius = settings.LeadRadius;
        double currentZ = 0; // Start at surface
        double zPerTurn = settings.HelixPitchPercent / 100 * settings.LeadRadius;
        
        // Move to helix start position
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = target.X + radius,
            Y = target.Y,
            Z = safeZ
        });
        
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = target.X + radius,
            Y = target.Y,
            Z = 0
        });
        
        // Generate helix turns
        int turns = (int)Math.Ceiling(Math.Abs(cutZ) / zPerTurn);
        double zPerMove = cutZ / (turns * 4); // 4 quarter-arcs per turn
        
        for (int t = 0; t < turns; t++)
        {
            // Quarter arc 1
            currentZ += zPerMove;
            moves.Add(new ToolpathMove
            {
                Type = MoveType.ArcCCW,
                X = target.X,
                Y = target.Y + radius,
                Z = Math.Max(currentZ, cutZ),
                I = -radius,
                J = 0,
                F = settings.FeedRate
            });
            
            if (currentZ <= cutZ) break;
            
            // Quarter arc 2
            currentZ += zPerMove;
            moves.Add(new ToolpathMove
            {
                Type = MoveType.ArcCCW,
                X = target.X - radius,
                Y = target.Y,
                Z = Math.Max(currentZ, cutZ),
                I = 0,
                J = -radius,
                F = settings.FeedRate
            });
            
            if (currentZ <= cutZ) break;
            
            // Quarter arc 3
            currentZ += zPerMove;
            moves.Add(new ToolpathMove
            {
                Type = MoveType.ArcCCW,
                X = target.X,
                Y = target.Y - radius,
                Z = Math.Max(currentZ, cutZ),
                I = radius,
                J = 0,
                F = settings.FeedRate
            });
            
            if (currentZ <= cutZ) break;
            
            // Quarter arc 4
            currentZ += zPerMove;
            moves.Add(new ToolpathMove
            {
                Type = MoveType.ArcCCW,
                X = target.X + radius,
                Y = target.Y,
                Z = Math.Max(currentZ, cutZ),
                I = 0,
                J = radius,
                F = settings.FeedRate
            });
            
            if (currentZ <= cutZ) break;
        }
        
        // Move to center at final depth
        moves.Add(new ToolpathMove
        {
            Type = MoveType.ArcCCW,
            X = target.X,
            Y = target.Y,
            Z = cutZ,
            I = -radius,
            J = 0,
            F = settings.FeedRate
        });
        
        return moves;
    }
    
    private List<ToolpathMove> GenerateLinearLeadOut(Point2D start, Point2D direction, 
        LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        double angle = settings.LeadAngle * Math.PI / 180;
        double endX = start.X + settings.LeadLength * Math.Cos(angle);
        double endY = start.Y + settings.LeadLength * Math.Sin(angle);
        
        // Linear exit
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = endX,
            Y = endY,
            Z = cutZ,
            F = settings.FeedRate
        });
        
        // Retract
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = endX,
            Y = endY,
            Z = safeZ
        });
        
        return moves;
    }
    
    private List<ToolpathMove> GenerateArcLeadOut(Point2D start, Point2D direction,
        LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        double radius = settings.LeadRadius;
        double angle = settings.LeadAngle * Math.PI / 180;
        
        double endX = start.X + radius * (1 - Math.Cos(angle));
        double endY = start.Y + radius * Math.Sin(angle);
        
        // Arc exit
        moves.Add(new ToolpathMove
        {
            Type = settings.ArcClockwise ? MoveType.ArcCW : MoveType.ArcCCW,
            X = endX,
            Y = endY,
            Z = cutZ,
            I = radius,
            J = 0,
            F = settings.FeedRate
        });
        
        // Retract
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = endX,
            Y = endY,
            Z = safeZ
        });
        
        return moves;
    }
    
    private List<ToolpathMove> GenerateTangentLeadOut(Point2D start, Point2D direction,
        LeadSettings settings, double cutZ, double safeZ)
    {
        var moves = new List<ToolpathMove>();
        
        double dx = direction.X - start.X;
        double dy = direction.Y - start.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length < 0.001)
        {
            return GenerateLinearLeadOut(start, direction, settings, cutZ, safeZ);
        }
        
        dx /= length;
        dy /= length;
        
        double endX = start.X + dx * settings.LeadLength;
        double endY = start.Y + dy * settings.LeadLength;
        
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Linear,
            X = endX,
            Y = endY,
            Z = cutZ,
            F = settings.FeedRate
        });
        
        moves.Add(new ToolpathMove
        {
            Type = MoveType.Rapid,
            X = endX,
            Y = endY,
            Z = safeZ
        });
        
        return moves;
    }
}

public class LeadSettings
{
    /// <summary>Lead-in/out length (mm)</summary>
    public double LeadLength { get; set; } = 5.0;
    
    /// <summary>Arc radius for arc lead-in/out (mm)</summary>
    public double LeadRadius { get; set; } = 5.0;
    
    /// <summary>Lead angle (degrees)</summary>
    public double LeadAngle { get; set; } = 90;
    
    /// <summary>Arc direction - true for clockwise</summary>
    public bool ArcClockwise { get; set; } = true;
    
    /// <summary>Feed rate during lead moves (mm/min)</summary>
    public double FeedRate { get; set; } = 1000;
    
    /// <summary>Plunge rate (mm/min)</summary>
    public double PlungeRate { get; set; } = 300;
    
    /// <summary>Ramp feed rate (mm/min)</summary>
    public double RampRate { get; set; } = 500;
    
    /// <summary>Helix pitch as percentage of diameter</summary>
    public double HelixPitchPercent { get; set; } = 10;
}
