using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Simulates stock material removal by creating a heightmap-based 3D model
/// The stock is represented as a grid of heights that get "cut" by the tool
/// </summary>
public class StockRemovalSimulator
{
    private double[,] _heightMap;
    private int _gridWidth;
    private int _gridHeight;
    private double _cellSize;
    private double _stockWidth;
    private double _stockHeight;
    private double _stockThickness;
    
    // Material colors
    private static readonly Color StockColor = Color.FromRgb(200, 180, 130);     // Wood color
    private static readonly Color CutColor = Color.FromRgb(180, 160, 110);       // Cut surface
    private static readonly Color FloorColor = Color.FromRgb(140, 120, 80);      // Bottom floor
    
    public StockRemovalSimulator()
    {
        _gridWidth = 100;
        _gridHeight = 100;
        _cellSize = 1.0;
        _heightMap = new double[_gridWidth, _gridHeight];
    }
    
    /// <summary>
    /// Initialize the stock with given dimensions
    /// </summary>
    public void InitializeStock(double width, double height, double thickness)
    {
        _stockWidth = width;
        _stockHeight = height;
        _stockThickness = thickness;
        
        // Determine grid resolution - aim for ~1mm cells
        _cellSize = Math.Max(0.5, Math.Min(width, height) / 100);
        _gridWidth = (int)Math.Ceiling(width / _cellSize) + 1;
        _gridHeight = (int)Math.Ceiling(height / _cellSize) + 1;
        
        _heightMap = new double[_gridWidth, _gridHeight];
        
        // Initialize all heights to stock thickness (top surface)
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                _heightMap[x, y] = thickness;
            }
        }
    }
    
    /// <summary>
    /// Apply tool cut at given position
    /// </summary>
    public void CutAt(double x, double y, double z, double toolDiameter)
    {
        if (z >= _stockThickness) return; // Above stock, no cut
        
        double toolRadius = toolDiameter / 2;
        double cutDepth = _stockThickness - Math.Max(0, z); // How deep to cut (z is negative for depth)
        double actualZ = _stockThickness + z; // Convert to actual Z height (z is already negative)
        if (actualZ < 0) actualZ = 0;
        
        // Calculate grid cells affected by tool
        int minCellX = Math.Max(0, (int)Math.Floor((x - toolRadius) / _cellSize));
        int maxCellX = Math.Min(_gridWidth - 1, (int)Math.Ceiling((x + toolRadius) / _cellSize));
        int minCellY = Math.Max(0, (int)Math.Floor((y - toolRadius) / _cellSize));
        int maxCellY = Math.Min(_gridHeight - 1, (int)Math.Ceiling((y + toolRadius) / _cellSize));
        
        // Apply cut to affected cells
        for (int cx = minCellX; cx <= maxCellX; cx++)
        {
            for (int cy = minCellY; cy <= maxCellY; cy++)
            {
                double cellX = cx * _cellSize;
                double cellY = cy * _cellSize;
                double dist = Math.Sqrt((cellX - x) * (cellX - x) + (cellY - y) * (cellY - y));
                
                if (dist <= toolRadius)
                {
                    // Inside tool radius - cut to Z height
                    _heightMap[cx, cy] = Math.Min(_heightMap[cx, cy], actualZ);
                }
            }
        }
    }
    
    /// <summary>
    /// Apply a line cut between two points
    /// </summary>
    public void CutLine(double x1, double y1, double z1, double x2, double y2, double z2, double toolDiameter)
    {
        double length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        int steps = Math.Max(1, (int)Math.Ceiling(length / (_cellSize * 0.5)));
        
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            double x = x1 + t * (x2 - x1);
            double y = y1 + t * (y2 - y1);
            double z = z1 + t * (z2 - z1);
            CutAt(x, y, z, toolDiameter);
        }
    }
    
    /// <summary>
    /// Apply all toolpath moves up to a certain move index
    /// </summary>
    public void ApplyToolpaths(List<Toolpath> toolpaths, int upToMoveIndex = -1)
    {
        int moveCounter = 0;
        Point3D lastPos = new Point3D(0, 0, _stockThickness + 10);
        
        foreach (var toolpath in toolpaths)
        {
            double toolDiameter = toolpath.Tool?.Diameter ?? 6.0;
            
            foreach (var move in toolpath.Moves)
            {
                if (upToMoveIndex >= 0 && moveCounter > upToMoveIndex)
                    return;
                
                Point3D currentPos = new Point3D(move.X, move.Y, _stockThickness + move.Z);
                
                // Only apply linear/feed moves (not rapids above stock)
                if (move.Type != MoveType.Rapid && move.Z < 0)
                {
                    CutLine(lastPos.X, lastPos.Y, lastPos.Z - _stockThickness, 
                            currentPos.X, currentPos.Y, currentPos.Z - _stockThickness, 
                            toolDiameter);
                }
                
                lastPos = currentPos;
                moveCounter++;
            }
        }
    }
    
    /// <summary>
    /// Create 3D mesh geometry from current heightmap
    /// </summary>
    public MeshGeometry3D CreateStockMesh()
    {
        var mesh = new MeshGeometry3D();
        
        // Add vertices for top surface (from heightmap)
        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                mesh.Positions.Add(new Point3D(x * _cellSize, y * _cellSize, _heightMap[x, y]));
            }
        }
        
        // Create triangles for top surface
        for (int y = 0; y < _gridHeight - 1; y++)
        {
            for (int x = 0; x < _gridWidth - 1; x++)
            {
                int i00 = y * _gridWidth + x;
                int i10 = y * _gridWidth + x + 1;
                int i01 = (y + 1) * _gridWidth + x;
                int i11 = (y + 1) * _gridWidth + x + 1;
                
                // Two triangles per cell
                mesh.TriangleIndices.Add(i00);
                mesh.TriangleIndices.Add(i10);
                mesh.TriangleIndices.Add(i11);
                
                mesh.TriangleIndices.Add(i00);
                mesh.TriangleIndices.Add(i11);
                mesh.TriangleIndices.Add(i01);
            }
        }
        
        // Add bottom vertices (Z = 0)
        int bottomOffset = mesh.Positions.Count;
        for (int y = 0; y < _gridHeight; y++)
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                mesh.Positions.Add(new Point3D(x * _cellSize, y * _cellSize, 0));
            }
        }
        
        // Create side walls (X = 0)
        for (int y = 0; y < _gridHeight - 1; y++)
        {
            int topIdx = y * _gridWidth;
            int topNextIdx = (y + 1) * _gridWidth;
            int botIdx = bottomOffset + y * _gridWidth;
            int botNextIdx = bottomOffset + (y + 1) * _gridWidth;
            
            mesh.TriangleIndices.Add(topIdx);
            mesh.TriangleIndices.Add(botIdx);
            mesh.TriangleIndices.Add(botNextIdx);
            
            mesh.TriangleIndices.Add(topIdx);
            mesh.TriangleIndices.Add(botNextIdx);
            mesh.TriangleIndices.Add(topNextIdx);
        }
        
        // Side walls (X = max)
        for (int y = 0; y < _gridHeight - 1; y++)
        {
            int topIdx = y * _gridWidth + (_gridWidth - 1);
            int topNextIdx = (y + 1) * _gridWidth + (_gridWidth - 1);
            int botIdx = bottomOffset + y * _gridWidth + (_gridWidth - 1);
            int botNextIdx = bottomOffset + (y + 1) * _gridWidth + (_gridWidth - 1);
            
            mesh.TriangleIndices.Add(topIdx);
            mesh.TriangleIndices.Add(topNextIdx);
            mesh.TriangleIndices.Add(botNextIdx);
            
            mesh.TriangleIndices.Add(topIdx);
            mesh.TriangleIndices.Add(botNextIdx);
            mesh.TriangleIndices.Add(botIdx);
        }
        
        // Side walls (Y = 0)
        for (int x = 0; x < _gridWidth - 1; x++)
        {
            int topIdx = x;
            int topNextIdx = x + 1;
            int botIdx = bottomOffset + x;
            int botNextIdx = bottomOffset + x + 1;
            
            mesh.TriangleIndices.Add(topIdx);
            mesh.TriangleIndices.Add(topNextIdx);
            mesh.TriangleIndices.Add(botNextIdx);
            
            mesh.TriangleIndices.Add(topIdx);
            mesh.TriangleIndices.Add(botNextIdx);
            mesh.TriangleIndices.Add(botIdx);
        }
        
        // Side walls (Y = max)
        for (int x = 0; x < _gridWidth - 1; x++)
        {
            int topIdx = (_gridHeight - 1) * _gridWidth + x;
            int topNextIdx = (_gridHeight - 1) * _gridWidth + x + 1;
            int botIdx = bottomOffset + (_gridHeight - 1) * _gridWidth + x;
            int botNextIdx = bottomOffset + (_gridHeight - 1) * _gridWidth + x + 1;
            
            mesh.TriangleIndices.Add(topIdx);
            mesh.TriangleIndices.Add(botIdx);
            mesh.TriangleIndices.Add(botNextIdx);
            
            mesh.TriangleIndices.Add(topIdx);
            mesh.TriangleIndices.Add(botNextIdx);
            mesh.TriangleIndices.Add(topNextIdx);
        }
        
        // Add bottom face
        for (int y = 0; y < _gridHeight - 1; y++)
        {
            for (int x = 0; x < _gridWidth - 1; x++)
            {
                int i00 = bottomOffset + y * _gridWidth + x;
                int i10 = bottomOffset + y * _gridWidth + x + 1;
                int i01 = bottomOffset + (y + 1) * _gridWidth + x;
                int i11 = bottomOffset + (y + 1) * _gridWidth + x + 1;
                
                mesh.TriangleIndices.Add(i00);
                mesh.TriangleIndices.Add(i11);
                mesh.TriangleIndices.Add(i10);
                
                mesh.TriangleIndices.Add(i00);
                mesh.TriangleIndices.Add(i01);
                mesh.TriangleIndices.Add(i11);
            }
        }
        
        return mesh;
    }
    
    /// <summary>
    /// Create a complete ModelVisual3D with the stock mesh
    /// </summary>
    public ModelVisual3D CreateStockVisual()
    {
        var visual = new ModelVisual3D();
        var mesh = CreateStockMesh();
        
        // Create material - wood-like appearance
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(StockColor)));
        material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 30));
        
        var model = new GeometryModel3D(mesh, material);
        model.BackMaterial = new DiffuseMaterial(new SolidColorBrush(FloorColor));
        
        visual.Content = model;
        return visual;
    }
    
    /// <summary>
    /// Reset stock to original state
    /// </summary>
    public void Reset()
    {
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                _heightMap[x, y] = _stockThickness;
            }
        }
    }
    
    /// <summary>
    /// Get current heightmap (for advanced visualization)
    /// </summary>
    public double[,] GetHeightMap() => _heightMap;
    
    /// <summary>
    /// Get grid dimensions
    /// </summary>
    public (int width, int height, double cellSize) GetGridInfo() => (_gridWidth, _gridHeight, _cellSize);
}
