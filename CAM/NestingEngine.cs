using System;
using System.Collections.Generic;
using System.Linq;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Automatic nesting of parts for optimal material usage
/// </summary>
public class NestingEngine
{
    private readonly Random _random = new();
    
    /// <summary>
    /// Nest parts within stock material
    /// </summary>
    public NestingResult Nest(List<NestingPart> parts, NestingSettings settings)
    {
        var result = new NestingResult
        {
            StockWidth = settings.StockWidth,
            StockHeight = settings.StockHeight
        };
        
        // Calculate part bounds and areas
        foreach (var part in parts)
        {
            CalculatePartBounds(part);
        }
        
        // Sort parts by area (largest first) - FFD heuristic
        var sortedParts = parts.OrderByDescending(p => p.Width * p.Height).ToList();
        
        // Add required copies
        var allParts = new List<NestingPart>();
        foreach (var part in sortedParts)
        {
            for (int i = 0; i < part.Quantity; i++)
            {
                allParts.Add(new NestingPart
                {
                    Id = $"{part.Id}_{i + 1}",
                    Paths = part.Paths,
                    Width = part.Width,
                    Height = part.Height,
                    AllowRotation = part.AllowRotation
                });
            }
        }
        
        // Choose nesting algorithm
        var placements = settings.Algorithm switch
        {
            NestingAlgorithm.BottomLeftFill => NestBottomLeftFill(allParts, settings),
            NestingAlgorithm.Genetic => NestGenetic(allParts, settings),
            NestingAlgorithm.NFP => NestNFP(allParts, settings),
            _ => NestBottomLeftFill(allParts, settings)
        };
        
        result.Placements = placements;
        result.Efficiency = CalculateEfficiency(placements, settings);
        result.UsedSheets = CalculateUsedSheets(placements, settings);
        
        return result;
    }
    
    private void CalculatePartBounds(NestingPart part)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        
        foreach (var path in part.Paths)
        {
            foreach (var seg in path.Segments)
            {
                minX = Math.Min(minX, seg.EndPoint.X);
                maxX = Math.Max(maxX, seg.EndPoint.X);
                minY = Math.Min(minY, seg.EndPoint.Y);
                maxY = Math.Max(maxY, seg.EndPoint.Y);
            }
        }
        
        part.Width = maxX - minX + 2 * part.Margin;
        part.Height = maxY - minY + 2 * part.Margin;
        part.OffsetX = minX - part.Margin;
        part.OffsetY = minY - part.Margin;
    }
    
    private List<PartPlacement> NestBottomLeftFill(List<NestingPart> parts, NestingSettings settings)
    {
        var placements = new List<PartPlacement>();
        var skyline = new List<(double X, double Y, double Width)>
        {
            (0, 0, settings.StockWidth)
        };
        
        int sheetIndex = 0;
        
        foreach (var part in parts)
        {
            bool placed = false;
            
            // Try each rotation
            var rotations = part.AllowRotation ? new[] { 0.0, 90.0 } : new[] { 0.0 };
            
            foreach (var rotation in rotations)
            {
                double partW = rotation == 0 ? part.Width : part.Height;
                double partH = rotation == 0 ? part.Height : part.Width;
                
                // Find position using bottom-left fill
                var position = FindBottomLeftPosition(skyline, partW, partH, settings);
                
                if (position.HasValue)
                {
                    placements.Add(new PartPlacement
                    {
                        Part = part,
                        X = position.Value.X,
                        Y = position.Value.Y,
                        Rotation = rotation,
                        SheetIndex = sheetIndex
                    });
                    
                    UpdateSkyline(skyline, position.Value.X, position.Value.Y, partW, partH);
                    placed = true;
                    break;
                }
            }
            
            if (!placed)
            {
                // Start new sheet
                sheetIndex++;
                skyline = new List<(double X, double Y, double Width)>
                {
                    (0, 0, settings.StockWidth)
                };
                
                placements.Add(new PartPlacement
                {
                    Part = part,
                    X = settings.Margin,
                    Y = settings.Margin,
                    Rotation = 0,
                    SheetIndex = sheetIndex
                });
                
                UpdateSkyline(skyline, settings.Margin, settings.Margin, part.Width, part.Height);
            }
        }
        
        return placements;
    }
    
    private (double X, double Y)? FindBottomLeftPosition(
        List<(double X, double Y, double Width)> skyline, 
        double partW, double partH, 
        NestingSettings settings)
    {
        double bestY = double.MaxValue;
        double? bestX = null;
        
        // Try each skyline position
        foreach (var seg in skyline)
        {
            if (seg.X + partW <= settings.StockWidth - settings.Margin &&
                seg.Y + partH <= settings.StockHeight - settings.Margin)
            {
                // Check if position is valid (no overlap)
                double maxY = GetMaxYInRange(skyline, seg.X, seg.X + partW);
                
                if (maxY + partH <= settings.StockHeight - settings.Margin && maxY < bestY)
                {
                    bestY = maxY;
                    bestX = seg.X;
                }
            }
        }
        
        if (bestX.HasValue)
        {
            return (bestX.Value + settings.Margin, bestY + settings.Margin);
        }
        
        return null;
    }
    
    private double GetMaxYInRange(List<(double X, double Y, double Width)> skyline, double x1, double x2)
    {
        double maxY = 0;
        foreach (var seg in skyline)
        {
            if (seg.X < x2 && seg.X + seg.Width > x1)
            {
                maxY = Math.Max(maxY, seg.Y);
            }
        }
        return maxY;
    }
    
    private void UpdateSkyline(List<(double X, double Y, double Width)> skyline, 
        double x, double y, double w, double h)
    {
        // Add new segment for placed part
        var newSegments = new List<(double X, double Y, double Width)>();
        
        foreach (var seg in skyline)
        {
            if (seg.X + seg.Width <= x || seg.X >= x + w)
            {
                // No overlap
                newSegments.Add(seg);
            }
            else
            {
                // Partial overlap - split segment
                if (seg.X < x)
                {
                    newSegments.Add((seg.X, seg.Y, x - seg.X));
                }
                if (seg.X + seg.Width > x + w)
                {
                    newSegments.Add((x + w, seg.Y, seg.X + seg.Width - x - w));
                }
            }
        }
        
        // Add new segment at top of placed part
        newSegments.Add((x, y + h, w));
        
        // Sort and merge adjacent segments
        skyline.Clear();
        skyline.AddRange(MergeSkyline(newSegments));
    }
    
    private List<(double X, double Y, double Width)> MergeSkyline(
        List<(double X, double Y, double Width)> segments)
    {
        var sorted = segments.OrderBy(s => s.X).ToList();
        var merged = new List<(double X, double Y, double Width)>();
        
        foreach (var seg in sorted)
        {
            if (merged.Count > 0)
            {
                var last = merged[^1];
                if (Math.Abs(last.X + last.Width - seg.X) < 0.001 && Math.Abs(last.Y - seg.Y) < 0.001)
                {
                    // Merge with previous
                    merged[^1] = (last.X, last.Y, last.Width + seg.Width);
                    continue;
                }
            }
            merged.Add(seg);
        }
        
        return merged;
    }
    
    private List<PartPlacement> NestGenetic(List<NestingPart> parts, NestingSettings settings)
    {
        // Genetic algorithm for nesting
        int populationSize = 30;
        int generations = 100;
        
        // Initialize population with different orderings
        var population = new List<List<NestingPart>>();
        for (int i = 0; i < populationSize; i++)
        {
            population.Add(ShuffleParts(parts));
        }
        
        // Sort by area descending as first solution
        population[0] = parts.OrderByDescending(p => p.Width * p.Height).ToList();
        
        List<PartPlacement>? bestPlacement = null;
        double bestEfficiency = 0;
        
        for (int gen = 0; gen < generations; gen++)
        {
            // Evaluate all solutions
            var evaluated = population.Select(p => 
            {
                var placement = NestBottomLeftFill(p, settings);
                var efficiency = CalculateEfficiency(placement, settings);
                return (Ordering: p, Placement: placement, Efficiency: efficiency);
            }).OrderByDescending(e => e.Efficiency).ToList();
            
            if (evaluated[0].Efficiency > bestEfficiency)
            {
                bestEfficiency = evaluated[0].Efficiency;
                bestPlacement = evaluated[0].Placement;
            }
            
            // Selection and crossover
            var newPopulation = new List<List<NestingPart>>();
            
            // Keep best solutions
            for (int i = 0; i < populationSize / 4; i++)
            {
                newPopulation.Add(evaluated[i].Ordering);
            }
            
            // Crossover
            while (newPopulation.Count < populationSize)
            {
                var p1 = evaluated[_random.Next(populationSize / 2)].Ordering;
                var p2 = evaluated[_random.Next(populationSize / 2)].Ordering;
                var child = CrossoverParts(p1, p2);
                
                // Mutation
                if (_random.NextDouble() < 0.1)
                {
                    child = MutateParts(child);
                }
                
                newPopulation.Add(child);
            }
            
            population = newPopulation;
        }
        
        return bestPlacement ?? NestBottomLeftFill(parts, settings);
    }
    
    private List<NestingPart> ShuffleParts(List<NestingPart> parts)
    {
        return parts.OrderBy(_ => _random.Next()).ToList();
    }
    
    private List<NestingPart> CrossoverParts(List<NestingPart> p1, List<NestingPart> p2)
    {
        int n = p1.Count;
        int start = _random.Next(n);
        int end = _random.Next(n);
        if (start > end) (start, end) = (end, start);
        
        var result = new List<NestingPart>();
        var used = new HashSet<string>();
        
        for (int i = start; i <= end; i++)
        {
            result.Add(p1[i]);
            used.Add(p1[i].Id);
        }
        
        foreach (var part in p2)
        {
            if (!used.Contains(part.Id))
            {
                result.Add(part);
            }
        }
        
        return result;
    }
    
    private List<NestingPart> MutateParts(List<NestingPart> parts)
    {
        var result = new List<NestingPart>(parts);
        int i = _random.Next(result.Count);
        int j = _random.Next(result.Count);
        (result[i], result[j]) = (result[j], result[i]);
        return result;
    }
    
    private List<PartPlacement> NestNFP(List<NestingPart> parts, NestingSettings settings)
    {
        // No-Fit Polygon algorithm (simplified version)
        // Full implementation would compute actual NFPs between parts
        return NestBottomLeftFill(parts, settings);
    }
    
    private double CalculateEfficiency(List<PartPlacement> placements, NestingSettings settings)
    {
        if (placements.Count == 0) return 0;
        
        double totalPartArea = placements.Sum(p => p.Part.Width * p.Part.Height);
        int sheets = placements.Max(p => p.SheetIndex) + 1;
        double totalStockArea = sheets * settings.StockWidth * settings.StockHeight;
        
        return totalPartArea / totalStockArea * 100;
    }
    
    private int CalculateUsedSheets(List<PartPlacement> placements, NestingSettings settings)
    {
        if (placements.Count == 0) return 0;
        return placements.Max(p => p.SheetIndex) + 1;
    }
}

public class NestingPart
{
    public string Id { get; set; } = "";
    public List<PolyPath> Paths { get; set; } = new();
    public int Quantity { get; set; } = 1;
    public double Width { get; set; }
    public double Height { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double Margin { get; set; } = 2.0;
    public bool AllowRotation { get; set; } = true;
}

public class PartPlacement
{
    public NestingPart Part { get; set; } = new();
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public int SheetIndex { get; set; }
}

public class NestingResult
{
    public List<PartPlacement> Placements { get; set; } = new();
    public double StockWidth { get; set; }
    public double StockHeight { get; set; }
    public double Efficiency { get; set; }
    public int UsedSheets { get; set; }
}

public class NestingSettings
{
    public double StockWidth { get; set; } = 1220; // Standard sheet width
    public double StockHeight { get; set; } = 2440; // Standard sheet height
    public double Margin { get; set; } = 5.0; // Edge margin
    public double PartSpacing { get; set; } = 2.0; // Space between parts
    public NestingAlgorithm Algorithm { get; set; } = NestingAlgorithm.BottomLeftFill;
    public bool AllowRotation { get; set; } = true;
    public int MaxSheets { get; set; } = 10;
}

public enum NestingAlgorithm
{
    BottomLeftFill,
    Genetic,
    NFP // No-Fit Polygon
}
