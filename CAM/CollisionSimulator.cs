using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Collision Detection and Interference Checking for CNC Simulation
/// Detects tool/holder collisions with stock, fixtures, and machine components
/// </summary>
public class CollisionSimulator
{
    public class CollisionResult
    {
        public bool HasCollision { get; set; }
        public List<CollisionEvent> Collisions { get; set; } = new();
        public double TotalCollisionTime { get; set; }
        public int CollisionCount => Collisions.Count;
        public CollisionSeverity MaxSeverity => Collisions.Count > 0 
            ? Collisions.Max(c => c.Severity) 
            : CollisionSeverity.None;
    }
    
    public class CollisionEvent
    {
        public int MoveIndex { get; set; }
        public Point3D Position { get; set; }
        public double Time { get; set; }
        public CollisionType Type { get; set; }
        public CollisionSeverity Severity { get; set; }
        public string Description { get; set; } = "";
        public double PenetrationDepth { get; set; }
        public string CollidingObject { get; set; } = "";
    }
    
    public enum CollisionType
    {
        ToolToStock,
        HolderToStock,
        ToolToFixture,
        HolderToFixture,
        ToolToClamp,
        ShankToStock,
        SpindleToStock,
        RapidIntoStock,
        PlungeIntoStock
    }
    
    public enum CollisionSeverity
    {
        None,
        Warning,    // Potential issue
        Minor,      // Light touch
        Major,      // Significant interference
        Critical    // Crash - immediate stop required
    }
    
    private readonly Tool _tool;
    private readonly Stock _stock;
    private readonly List<Fixture>? _fixtures;
    private readonly List<Clamp>? _clamps;
    
    private double[,] _heightMap;
    private int _heightMapResolution;
    private double _cellSize;
    
    public CollisionSimulator(Tool tool, Stock stock, 
        List<Fixture>? fixtures = null, List<Clamp>? clamps = null)
    {
        _tool = tool;
        _stock = stock;
        _fixtures = fixtures;
        _clamps = clamps;
        
        InitializeHeightMap(100); // 100x100 grid
    }
    
    private void InitializeHeightMap(int resolution)
    {
        _heightMapResolution = resolution;
        _heightMap = new double[resolution, resolution];
        _cellSize = Math.Max(_stock.Width, _stock.Height) / resolution;
        
        // Initialize with stock top surface (Thickness is Z dimension)
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                _heightMap[i, j] = _stock.Thickness;
            }
        }
    }
    
    /// <summary>
    /// Check for collisions along entire toolpath
    /// </summary>
    public CollisionResult CheckToolpath(List<ToolpathMove> moves)
    {
        var result = new CollisionResult();
        double currentTime = 0;
        Point3D? lastPosition = null;
        
        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            
            if (lastPosition.HasValue)
            {
                // Check collision along move
                var moveCollisions = CheckMoveCollision(lastPosition.Value, move, i, currentTime);
                result.Collisions.AddRange(moveCollisions);
                
                // Update time
                var distance = Distance3D(lastPosition.Value, new Point3D(move.X, move.Y, move.Z));
                var feedRate = move.Type == MoveType.Rapid ? 5000 : move.FeedRate;
                currentTime += distance / Math.Max(feedRate, 1) * 60;
            }
            
            lastPosition = new Point3D(move.X, move.Y, move.Z);
            
            // Update height map for cutting moves
            if (move.Type == MoveType.Cut)
            {
                UpdateHeightMap(move);
            }
        }
        
        result.HasCollision = result.Collisions.Count > 0;
        result.TotalCollisionTime = result.Collisions.Sum(c => 0.1); // Approximate
        
        return result;
    }
    
    private List<CollisionEvent> CheckMoveCollision(Point3D start, ToolpathMove end, 
        int moveIndex, double time)
    {
        var collisions = new List<CollisionEvent>();
        var endPos = new Point3D(end.X, end.Y, end.Z);
        
        // Interpolate along move
        int steps = Math.Max(1, (int)(Distance3D(start, endPos) / 0.5)); // 0.5mm steps
        
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            var pos = Lerp(start, endPos, t);
            
            // 1. Check rapid moves into stock
            if (end.Type == MoveType.Rapid)
            {
                var rapidCollision = CheckRapidCollision(pos, moveIndex, time);
                if (rapidCollision != null && !collisions.Any(c => 
                    c.MoveIndex == moveIndex && c.Type == CollisionType.RapidIntoStock))
                {
                    collisions.Add(rapidCollision);
                }
            }
            
            // 2. Check holder collision
            var holderCollision = CheckHolderCollision(pos, moveIndex, time);
            if (holderCollision != null)
            {
                collisions.Add(holderCollision);
            }
            
            // 3. Check fixture collision
            if (_fixtures != null)
            {
                var fixtureCollision = CheckFixtureCollision(pos, moveIndex, time);
                if (fixtureCollision != null)
                {
                    collisions.Add(fixtureCollision);
                }
            }
            
            // 4. Check clamp collision
            if (_clamps != null)
            {
                var clampCollision = CheckClampCollision(pos, moveIndex, time);
                if (clampCollision != null)
                {
                    collisions.Add(clampCollision);
                }
            }
            
            // 5. Check plunge moves
            if (i > 0 && end.Type == MoveType.Cut)
            {
                var prevPos = Lerp(start, endPos, (i - 1) / (double)steps);
                if (pos.Z < prevPos.Z - 0.01) // Significant Z drop
                {
                    var plungeCollision = CheckPlungeCollision(pos, moveIndex, time);
                    if (plungeCollision != null)
                    {
                        collisions.Add(plungeCollision);
                    }
                }
            }
        }
        
        return collisions;
    }
    
    private CollisionEvent? CheckRapidCollision(Point3D pos, int moveIndex, double time)
    {
        // Get current stock height at this XY
        var stockHeight = GetStockHeightAt(pos.X, pos.Y);
        
        // Check if tool tip would be in stock during rapid
        if (pos.Z < stockHeight)
        {
            var depth = stockHeight - pos.Z;
            return new CollisionEvent
            {
                MoveIndex = moveIndex,
                Position = pos,
                Time = time,
                Type = CollisionType.RapidIntoStock,
                Severity = depth > 1 ? CollisionSeverity.Critical : CollisionSeverity.Major,
                Description = $"Rapid move into stock at Z={pos.Z:F2}, stock height={stockHeight:F2}",
                PenetrationDepth = depth,
                CollidingObject = "Stock"
            };
        }
        
        return null;
    }
    
    private CollisionEvent? CheckHolderCollision(Point3D pos, int moveIndex, double time)
    {
        if (_tool == null) return null;
        
        // Tool holder starts at tool length above tip
        var holderStartZ = pos.Z + _tool.TotalLength;
        var holderEndZ = holderStartZ + GetHolderLength();
        var holderRadius = GetHolderRadius();
        
        // Check if holder intersects with stock
        var stockHeight = GetStockHeightAt(pos.X, pos.Y);
        
        // Check area around holder (it's wider than tool)
        for (double angle = 0; angle < 360; angle += 30)
        {
            var rad = angle * Math.PI / 180;
            var checkX = pos.X + Math.Cos(rad) * holderRadius;
            var checkY = pos.Y + Math.Sin(rad) * holderRadius;
            
            var heightAtEdge = GetStockHeightAt(checkX, checkY);
            
            if (holderStartZ < heightAtEdge)
            {
                return new CollisionEvent
                {
                    MoveIndex = moveIndex,
                    Position = pos,
                    Time = time,
                    Type = CollisionType.HolderToStock,
                    Severity = CollisionSeverity.Major,
                    Description = $"Tool holder collision at Z={holderStartZ:F2}",
                    PenetrationDepth = heightAtEdge - holderStartZ,
                    CollidingObject = "Stock (holder interference)"
                };
            }
        }
        
        return null;
    }
    
    private CollisionEvent? CheckFixtureCollision(Point3D pos, int moveIndex, double time)
    {
        if (_fixtures == null) return null;
        
        var toolRadius = _tool.Diameter / 2;
        
        foreach (var fixture in _fixtures)
        {
            // Simple bounding box check
            if (pos.X - toolRadius < fixture.MaxX && pos.X + toolRadius > fixture.MinX &&
                pos.Y - toolRadius < fixture.MaxY && pos.Y + toolRadius > fixture.MinY &&
                pos.Z < fixture.MaxZ)
            {
                return new CollisionEvent
                {
                    MoveIndex = moveIndex,
                    Position = pos,
                    Time = time,
                    Type = CollisionType.ToolToFixture,
                    Severity = CollisionSeverity.Critical,
                    Description = $"Tool collision with fixture '{fixture.Name}'",
                    CollidingObject = fixture.Name ?? "Fixture"
                };
            }
        }
        
        return null;
    }
    
    private CollisionEvent? CheckClampCollision(Point3D pos, int moveIndex, double time)
    {
        if (_clamps == null) return null;
        
        var toolRadius = _tool.Diameter / 2;
        
        foreach (var clamp in _clamps)
        {
            // Check tool body against clamp
            var dist = Math.Sqrt(
                Math.Pow(pos.X - clamp.X, 2) + 
                Math.Pow(pos.Y - clamp.Y, 2));
            
            if (dist < toolRadius + clamp.Radius && pos.Z < clamp.Height)
            {
                return new CollisionEvent
                {
                    MoveIndex = moveIndex,
                    Position = pos,
                    Time = time,
                    Type = CollisionType.ToolToClamp,
                    Severity = CollisionSeverity.Critical,
                    Description = $"Tool collision with clamp at ({clamp.X:F1}, {clamp.Y:F1})",
                    CollidingObject = "Clamp"
                };
            }
        }
        
        return null;
    }
    
    private CollisionEvent? CheckPlungeCollision(Point3D pos, int moveIndex, double time)
    {
        // Check if plunging too fast or into solid material
        var stockHeight = GetStockHeightAt(pos.X, pos.Y);
        
        // If plunging into material that's more than safe ramp depth
        var maxPlungeDepth = _tool.Diameter * 0.5; // Typically half diameter
        
        if (pos.Z < stockHeight - maxPlungeDepth)
        {
            return new CollisionEvent
            {
                MoveIndex = moveIndex,
                Position = pos,
                Time = time,
                Type = CollisionType.PlungeIntoStock,
                Severity = CollisionSeverity.Warning,
                Description = $"Deep plunge detected: Z={pos.Z:F2}, recommended max={stockHeight - maxPlungeDepth:F2}",
                PenetrationDepth = stockHeight - maxPlungeDepth - pos.Z,
                CollidingObject = "Stock"
            };
        }
        
        return null;
    }
    
    private double GetStockHeightAt(double x, double y)
    {
        // Convert to heightmap coordinates
        var i = (int)((x + _stock.Width / 2) / _cellSize);
        var j = (int)((y + _stock.Height / 2) / _cellSize);
        
        // Clamp to bounds
        i = Math.Clamp(i, 0, _heightMapResolution - 1);
        j = Math.Clamp(j, 0, _heightMapResolution - 1);
        
        return _heightMap[i, j];
    }
    
    private void UpdateHeightMap(ToolpathMove move)
    {
        var toolRadius = _tool.Diameter / 2;
        var radiusCells = (int)(toolRadius / _cellSize) + 1;
        
        var centerI = (int)((move.X + _stock.Width / 2) / _cellSize);
        var centerJ = (int)((move.Y + _stock.Height / 2) / _cellSize);
        
        for (int di = -radiusCells; di <= radiusCells; di++)
        {
            for (int dj = -radiusCells; dj <= radiusCells; dj++)
            {
                var i = centerI + di;
                var j = centerJ + dj;
                
                if (i < 0 || i >= _heightMapResolution || 
                    j < 0 || j >= _heightMapResolution)
                    continue;
                
                var cellX = i * _cellSize - _stock.Width / 2;
                var cellY = j * _cellSize - _stock.Height / 2;
                
                var dist = Math.Sqrt(Math.Pow(cellX - move.X, 2) + Math.Pow(cellY - move.Y, 2));
                
                if (dist <= toolRadius)
                {
                    // Ball nose: account for tool shape
                    double cutHeight;
                    if (_tool.Type == ToolType.BallNose)
                    {
                        var ballOffset = toolRadius - Math.Sqrt(toolRadius * toolRadius - dist * dist);
                        cutHeight = move.Z + ballOffset;
                    }
                    else
                    {
                        cutHeight = move.Z;
                    }
                    
                    _heightMap[i, j] = Math.Min(_heightMap[i, j], cutHeight);
                }
            }
        }
    }
    
    private double GetHolderLength() => 50; // mm - typical holder length
    private double GetHolderRadius() => _tool.Diameter * 1.5; // Holder is wider than tool
    
    private Point3D Lerp(Point3D a, Point3D b, double t)
    {
        return new Point3D(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t);
    }
    
    private double Distance3D(Point3D a, Point3D b)
    {
        return Math.Sqrt(
            Math.Pow(b.X - a.X, 2) +
            Math.Pow(b.Y - a.Y, 2) +
            Math.Pow(b.Z - a.Z, 2));
    }
    
    /// <summary>
    /// Get visualization points for collision markers
    /// </summary>
    public List<Point3D> GetCollisionMarkers(CollisionResult result)
    {
        return result.Collisions.Select(c => c.Position).ToList();
    }
    
    /// <summary>
    /// Generate collision report as text
    /// </summary>
    public string GenerateReport(CollisionResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Collision Detection Report ===");
        sb.AppendLine($"Total Collisions: {result.CollisionCount}");
        sb.AppendLine($"Max Severity: {result.MaxSeverity}");
        sb.AppendLine();
        
        if (result.Collisions.Count == 0)
        {
            sb.AppendLine("No collisions detected. Toolpath is safe.");
        }
        else
        {
            var grouped = result.Collisions.GroupBy(c => c.Type);
            foreach (var group in grouped)
            {
                sb.AppendLine($"--- {group.Key} ({group.Count()}) ---");
                foreach (var collision in group.Take(10)) // Limit output
                {
                    sb.AppendLine($"  Move {collision.MoveIndex}: {collision.Description}");
                    sb.AppendLine($"    Position: ({collision.Position.X:F2}, {collision.Position.Y:F2}, {collision.Position.Z:F2})");
                    sb.AppendLine($"    Severity: {collision.Severity}");
                }
                if (group.Count() > 10)
                {
                    sb.AppendLine($"  ... and {group.Count() - 10} more");
                }
            }
        }
        
        return sb.ToString();
    }
}

// Supporting classes for collision detection
public class Fixture
{
    public string? Name { get; set; }
    public double MinX { get; set; }
    public double MaxX { get; set; }
    public double MinY { get; set; }
    public double MaxY { get; set; }
    public double MinZ { get; set; }
    public double MaxZ { get; set; }
}

public class Clamp
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; }
    public double Height { get; set; }
}
