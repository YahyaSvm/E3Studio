using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Optimizes toolpath ordering and rapid movements
/// </summary>
public class ToolpathOptimizer
{
    /// <summary>
    /// Reorder toolpaths to minimize rapid travel
    /// </summary>
    public List<Toolpath> OptimizeOrder(List<Toolpath> toolpaths, OptimizationSettings settings)
    {
        if (toolpaths.Count <= 1) return toolpaths;
        
        // Group by tool to minimize tool changes
        var byTool = toolpaths.GroupBy(t => t.ToolId).ToList();
        var result = new List<Toolpath>();
        
        foreach (var group in byTool)
        {
            var optimized = settings.Algorithm switch
            {
                OptimizationAlgorithm.NearestNeighbor => OptimizeNearestNeighbor(group.ToList()),
                OptimizationAlgorithm.TwoOpt => OptimizeTwoOpt(group.ToList()),
                OptimizationAlgorithm.Genetic => OptimizeGenetic(group.ToList(), settings),
                _ => group.ToList()
            };
            result.AddRange(optimized);
        }
        
        return result;
    }
    
    /// <summary>
    /// Optimize rapid movements within a single toolpath
    /// </summary>
    public Toolpath OptimizeRapids(Toolpath toolpath, OptimizationSettings settings)
    {
        var optimized = new Toolpath
        {
            Name = toolpath.Name,
            Type = toolpath.Type,
            ToolId = toolpath.ToolId,
            Tool = toolpath.Tool,
            CutDepth = toolpath.CutDepth,
            FeedRate = toolpath.FeedRate,
            PlungeRate = toolpath.PlungeRate,
            SpindleRPM = toolpath.SpindleRPM
        };
        
        // Find continuous cut segments
        var segments = ExtractCutSegments(toolpath.Moves);
        
        // Reorder segments to minimize rapids
        var orderedSegments = settings.Algorithm switch
        {
            OptimizationAlgorithm.NearestNeighbor => OrderSegmentsNearestNeighbor(segments),
            OptimizationAlgorithm.TwoOpt => OrderSegmentsTwoOpt(segments),
            _ => segments
        };
        
        // Rebuild moves with optimized order
        Point3D currentPos = new Point3D(0, 0, settings.SafeHeight);
        foreach (var segment in orderedSegments)
        {
            // Add rapid to segment start if not already there
            var firstMove = segment.Moves.First();
            if (Math.Abs(currentPos.X - firstMove.X) > 0.001 || 
                Math.Abs(currentPos.Y - firstMove.Y) > 0.001)
            {
                // Retract if needed
                if (currentPos.Z < settings.SafeHeight)
                {
                    optimized.Moves.Add(new ToolpathMove
                    {
                        Type = MoveType.Rapid,
                        X = currentPos.X,
                        Y = currentPos.Y,
                        Z = settings.SafeHeight
                    });
                }
                
                // Rapid to new position
                optimized.Moves.Add(new ToolpathMove
                {
                    Type = MoveType.Rapid,
                    X = firstMove.X,
                    Y = firstMove.Y,
                    Z = settings.SafeHeight
                });
            }
            
            // Add segment moves
            optimized.Moves.AddRange(segment.Moves);
            
            var lastMove = segment.Moves.Last();
            currentPos = new Point3D(lastMove.X, lastMove.Y, lastMove.Z);
        }
        
        return optimized;
    }
    
    private List<Toolpath> OptimizeNearestNeighbor(List<Toolpath> toolpaths)
    {
        if (toolpaths.Count <= 1) return toolpaths;
        
        var result = new List<Toolpath>();
        var remaining = new List<Toolpath>(toolpaths);
        var currentPos = new Point2D(0, 0);
        
        while (remaining.Count > 0)
        {
            // Find nearest toolpath
            int nearestIdx = 0;
            double nearestDist = double.MaxValue;
            
            for (int i = 0; i < remaining.Count; i++)
            {
                var tp = remaining[i];
                var startPos = GetToolpathStartPosition(tp);
                double dist = Distance(currentPos, startPos);
                
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestIdx = i;
                }
            }
            
            var nearest = remaining[nearestIdx];
            remaining.RemoveAt(nearestIdx);
            result.Add(nearest);
            
            currentPos = GetToolpathEndPosition(nearest);
        }
        
        return result;
    }
    
    private List<Toolpath> OptimizeTwoOpt(List<Toolpath> toolpaths)
    {
        var current = OptimizeNearestNeighbor(toolpaths);
        bool improved = true;
        int maxIterations = 1000;
        int iteration = 0;
        
        while (improved && iteration < maxIterations)
        {
            improved = false;
            iteration++;
            
            for (int i = 0; i < current.Count - 1; i++)
            {
                for (int j = i + 2; j < current.Count; j++)
                {
                    double currentDist = CalculateTotalDistance(current);
                    
                    // Try reversing segment between i and j
                    var newOrder = TwoOptSwap(current, i, j);
                    double newDist = CalculateTotalDistance(newOrder);
                    
                    if (newDist < currentDist - 0.001)
                    {
                        current = newOrder;
                        improved = true;
                    }
                }
            }
        }
        
        return current;
    }
    
    private List<Toolpath> TwoOptSwap(List<Toolpath> toolpaths, int i, int j)
    {
        var result = new List<Toolpath>();
        
        // Add elements before i
        for (int k = 0; k < i; k++)
            result.Add(toolpaths[k]);
        
        // Add reversed segment from i to j
        for (int k = j; k >= i; k--)
            result.Add(toolpaths[k]);
        
        // Add elements after j
        for (int k = j + 1; k < toolpaths.Count; k++)
            result.Add(toolpaths[k]);
        
        return result;
    }
    
    private List<Toolpath> OptimizeGenetic(List<Toolpath> toolpaths, OptimizationSettings settings)
    {
        // Simple genetic algorithm
        int populationSize = 50;
        int generations = 100;
        double mutationRate = 0.1;
        
        // Initialize population with random permutations
        var population = new List<List<Toolpath>>();
        for (int i = 0; i < populationSize; i++)
        {
            population.Add(ShuffleToolpaths(toolpaths));
        }
        
        // Add nearest neighbor solution
        population[0] = OptimizeNearestNeighbor(toolpaths);
        
        for (int gen = 0; gen < generations; gen++)
        {
            // Sort by fitness (total distance)
            population = population.OrderBy(p => CalculateTotalDistance(p)).ToList();
            
            // Keep best half
            var newPopulation = population.Take(populationSize / 2).ToList();
            
            // Crossover to fill rest
            while (newPopulation.Count < populationSize)
            {
                var parent1 = population[Random.Shared.Next(populationSize / 2)];
                var parent2 = population[Random.Shared.Next(populationSize / 2)];
                var child = Crossover(parent1, parent2);
                
                // Mutation
                if (Random.Shared.NextDouble() < mutationRate)
                {
                    child = Mutate(child);
                }
                
                newPopulation.Add(child);
            }
            
            population = newPopulation;
        }
        
        // Return best solution
        return population.OrderBy(p => CalculateTotalDistance(p)).First();
    }
    
    private List<Toolpath> ShuffleToolpaths(List<Toolpath> toolpaths)
    {
        var list = new List<Toolpath>(toolpaths);
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Shared.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
        return list;
    }
    
    private List<Toolpath> Crossover(List<Toolpath> parent1, List<Toolpath> parent2)
    {
        // Order crossover (OX)
        int n = parent1.Count;
        int start = Random.Shared.Next(n);
        int end = Random.Shared.Next(n);
        if (start > end) (start, end) = (end, start);
        
        var child = new Toolpath[n];
        var used = new HashSet<int>();
        
        // Copy segment from parent1
        for (int i = start; i <= end; i++)
        {
            child[i] = parent1[i];
            used.Add(parent1[i].GetHashCode());
        }
        
        // Fill rest from parent2 in order
        int childIdx = (end + 1) % n;
        for (int i = 0; i < n; i++)
        {
            int p2Idx = (end + 1 + i) % n;
            if (!used.Contains(parent2[p2Idx].GetHashCode()))
            {
                child[childIdx] = parent2[p2Idx];
                childIdx = (childIdx + 1) % n;
            }
        }
        
        return child.ToList();
    }
    
    private List<Toolpath> Mutate(List<Toolpath> toolpaths)
    {
        var list = new List<Toolpath>(toolpaths);
        int i = Random.Shared.Next(list.Count);
        int j = Random.Shared.Next(list.Count);
        (list[i], list[j]) = (list[j], list[i]);
        return list;
    }
    
    private List<CutSegment> ExtractCutSegments(List<ToolpathMove> moves)
    {
        var segments = new List<CutSegment>();
        var currentSegment = new CutSegment();
        
        foreach (var move in moves)
        {
            if (move.Type == MoveType.Rapid)
            {
                if (currentSegment.Moves.Count > 0)
                {
                    segments.Add(currentSegment);
                    currentSegment = new CutSegment();
                }
            }
            else
            {
                currentSegment.Moves.Add(move);
            }
        }
        
        if (currentSegment.Moves.Count > 0)
        {
            segments.Add(currentSegment);
        }
        
        return segments;
    }
    
    private List<CutSegment> OrderSegmentsNearestNeighbor(List<CutSegment> segments)
    {
        if (segments.Count <= 1) return segments;
        
        var result = new List<CutSegment>();
        var remaining = new List<CutSegment>(segments);
        var currentPos = new Point2D(0, 0);
        
        while (remaining.Count > 0)
        {
            int nearestIdx = 0;
            double nearestDist = double.MaxValue;
            bool reverseNearest = false;
            
            for (int i = 0; i < remaining.Count; i++)
            {
                var seg = remaining[i];
                if (seg.Moves.Count == 0) continue;
                
                var startMove = seg.Moves.First();
                var endMove = seg.Moves.Last();
                
                double distToStart = Distance(currentPos.X, currentPos.Y, startMove.X, startMove.Y);
                double distToEnd = Distance(currentPos.X, currentPos.Y, endMove.X, endMove.Y);
                
                if (distToStart < nearestDist)
                {
                    nearestDist = distToStart;
                    nearestIdx = i;
                    reverseNearest = false;
                }
                
                if (distToEnd < nearestDist)
                {
                    nearestDist = distToEnd;
                    nearestIdx = i;
                    reverseNearest = true;
                }
            }
            
            var nearest = remaining[nearestIdx];
            remaining.RemoveAt(nearestIdx);
            
            if (reverseNearest && CanReverseSegment(nearest))
            {
                nearest = ReverseSegment(nearest);
            }
            
            result.Add(nearest);
            
            var lastMove = nearest.Moves.Last();
            currentPos = new Point2D(lastMove.X, lastMove.Y);
        }
        
        return result;
    }
    
    private List<CutSegment> OrderSegmentsTwoOpt(List<CutSegment> segments)
    {
        var current = OrderSegmentsNearestNeighbor(segments);
        // Similar 2-opt implementation for segments...
        return current;
    }
    
    private bool CanReverseSegment(CutSegment segment)
    {
        // Can't reverse if contains arcs (would change direction)
        return !segment.Moves.Any(m => m.Type == MoveType.ArcCW || m.Type == MoveType.ArcCCW);
    }
    
    private CutSegment ReverseSegment(CutSegment segment)
    {
        var reversed = new CutSegment();
        reversed.Moves = segment.Moves.AsEnumerable().Reverse().ToList();
        return reversed;
    }
    
    private double CalculateTotalDistance(List<Toolpath> toolpaths)
    {
        double total = 0;
        var currentPos = new Point2D(0, 0);
        
        foreach (var tp in toolpaths)
        {
            var start = GetToolpathStartPosition(tp);
            total += Distance(currentPos, start);
            currentPos = GetToolpathEndPosition(tp);
        }
        
        return total;
    }
    
    private Point2D GetToolpathStartPosition(Toolpath tp)
    {
        if (tp.Moves.Count == 0) return new Point2D(0, 0);
        var first = tp.Moves.First();
        return new Point2D(first.X, first.Y);
    }
    
    private Point2D GetToolpathEndPosition(Toolpath tp)
    {
        if (tp.Moves.Count == 0) return new Point2D(0, 0);
        var last = tp.Moves.Last();
        return new Point2D(last.X, last.Y);
    }
    
    private double Distance(Point2D a, Point2D b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    
    private double Distance(double x1, double y1, double x2, double y2) =>
        Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
}

public class OptimizationSettings
{
    public OptimizationAlgorithm Algorithm { get; set; } = OptimizationAlgorithm.NearestNeighbor;
    public double SafeHeight { get; set; } = 5.0;
    public bool AllowPathReversal { get; set; } = true;
    public int MaxIterations { get; set; } = 1000;
}

public enum OptimizationAlgorithm
{
    None,
    NearestNeighbor,
    TwoOpt,
    Genetic
}

public class CutSegment
{
    public List<ToolpathMove> Moves { get; set; } = new();
}
