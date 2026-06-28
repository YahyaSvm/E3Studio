using E3Studio.CAM;
using E3Studio.Models;
using Xunit;

namespace E3Studio.Tests;

public class CamEngineTests
{
    [Fact]
    public void TabGenerator_preserves_moves_when_tabs_disabled()
    {
        var generator = new TabGenerator();
        var moves = new List<ToolpathMove>
        {
            new() { Type = MoveType.Rapid, X = 0, Y = 0, Z = 10 },
            new() { Type = MoveType.Linear, X = 10, Y = 0, Z = -2, F = 800 }
        };

        var result = generator.AddTabsToMoves(moves, new TabSettings { TabCount = 0 }, 2);
        Assert.Equal(moves.Count, result.Count);
    }

    [Fact]
    public void NestingEngine_places_single_part()
    {
        var engine = new NestingEngine();
        var part = new NestingPart
        {
            Id = "p1",
            Paths = new List<PolyPath>
            {
                new() { Segments = new List<PathSegment>() }
            },
            Width = 50,
            Height = 30
        };

        var result = engine.Nest(new List<NestingPart> { part }, new NestingSettings
        {
            StockWidth = 200,
            StockHeight = 200
        });

        Assert.Single(result.Placements);
    }

    [Fact]
    public void ToolpathOptimizer_keeps_single_toolpath()
    {
        var optimizer = new ToolpathOptimizer();
        var toolpaths = new List<Toolpath>
        {
            new() { Name = "A", Moves = new List<ToolpathMove>() }
        };

        var result = optimizer.OptimizeOrder(toolpaths, new OptimizationSettings());
        Assert.Single(result);
    }
}
