using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Imports G-Code files and generates toolpaths (reverse engineering)
/// </summary>
public class GCodeImporter
{
    private double _currentX = 0;
    private double _currentY = 0;
    private double _currentZ = 0;
    private double _currentF = 0;
    private int _currentS = 0;
    private bool _isAbsolute = true;
    private bool _isMetric = true;
    
    public List<Toolpath> Import(string filePath)
    {
        var toolpaths = new List<Toolpath>();
        var currentToolpath = new Toolpath { Name = "Imported Toolpath" };
        
        var lines = File.ReadAllLines(filePath);
        
        foreach (var rawLine in lines)
        {
            var line = StripComments(rawLine.Trim().ToUpper());
            if (string.IsNullOrEmpty(line)) continue;
            
            ProcessLine(line, currentToolpath, toolpaths);
        }
        
        // Add final toolpath if it has moves
        if (currentToolpath.Moves.Count > 0)
        {
            toolpaths.Add(currentToolpath);
        }
        
        return toolpaths;
    }
    
    /// <summary>
    /// Import G-Code and return as 2D geometry paths
    /// </summary>
    public List<Layer> ImportAsGeometry(string filePath)
    {
        var layers = new List<Layer>();
        var layer = new Layer { Name = Path.GetFileNameWithoutExtension(filePath) };
        
        _currentX = _currentY = _currentZ = 0;
        _isAbsolute = true;
        _isMetric = true;
        
        var lines = File.ReadAllLines(filePath);
        var currentPath = new List<Point2D>();
        bool cutting = false;
        double lastZ = 0;
        
        foreach (var rawLine in lines)
        {
            var line = StripComments(rawLine.Trim().ToUpper());
            if (string.IsNullOrEmpty(line)) continue;
            
            // Parse G codes
            var gCodes = Regex.Matches(line, @"G(\d+)");
            foreach (Match match in gCodes)
            {
                int g = int.Parse(match.Groups[1].Value);
                switch (g)
                {
                    case 20: _isMetric = false; break;
                    case 21: _isMetric = true; break;
                    case 90: _isAbsolute = true; break;
                    case 91: _isAbsolute = false; break;
                }
            }
            
            // Parse coordinates
            double? newX = ParseValue(line, 'X');
            double? newY = ParseValue(line, 'Y');
            double? newZ = ParseValue(line, 'Z');
            
            if (_isAbsolute)
            {
                if (newX.HasValue) _currentX = newX.Value;
                if (newY.HasValue) _currentY = newY.Value;
                if (newZ.HasValue) _currentZ = newZ.Value;
            }
            else
            {
                if (newX.HasValue) _currentX += newX.Value;
                if (newY.HasValue) _currentY += newY.Value;
                if (newZ.HasValue) _currentZ += newZ.Value;
            }
            
            // Convert to mm if needed
            double scale = _isMetric ? 1.0 : 25.4;
            double x = _currentX * scale;
            double y = _currentY * scale;
            double z = _currentZ * scale;
            
            // Detect cutting (Z below surface)
            bool isCutting = z < 0.5;
            
            if (isCutting && !cutting)
            {
                // Start new path
                if (currentPath.Count > 0)
                {
                    AddPath(layer, currentPath);
                    currentPath.Clear();
                }
                currentPath.Add(new Point2D(x, y));
                cutting = true;
            }
            else if (isCutting && cutting)
            {
                // Continue path
                currentPath.Add(new Point2D(x, y));
            }
            else if (!isCutting && cutting)
            {
                // End path
                if (currentPath.Count > 0)
                {
                    AddPath(layer, currentPath);
                    currentPath.Clear();
                }
                cutting = false;
            }
            
            lastZ = z;
        }
        
        // Final path
        if (currentPath.Count > 0)
        {
            AddPath(layer, currentPath);
        }
        
        layers.Add(layer);
        return layers;
    }
    
    private void ProcessLine(string line, Toolpath currentToolpath, List<Toolpath> toolpaths)
    {
        // Parse G codes
        var gCodes = Regex.Matches(line, @"G(\d+)");
        MoveType moveType = MoveType.Linear;
        bool hasMove = false;
        
        foreach (Match match in gCodes)
        {
            int g = int.Parse(match.Groups[1].Value);
            switch (g)
            {
                case 0: moveType = MoveType.Rapid; hasMove = true; break;
                case 1: moveType = MoveType.Linear; hasMove = true; break;
                case 2: moveType = MoveType.ArcCW; hasMove = true; break;
                case 3: moveType = MoveType.ArcCCW; hasMove = true; break;
                case 20: _isMetric = false; break;
                case 21: _isMetric = true; break;
                case 90: _isAbsolute = true; break;
                case 91: _isAbsolute = false; break;
            }
        }
        
        // Parse M codes
        var mCodes = Regex.Matches(line, @"M(\d+)");
        foreach (Match match in mCodes)
        {
            int m = int.Parse(match.Groups[1].Value);
            switch (m)
            {
                case 3: // Spindle on CW
                case 4: // Spindle on CCW
                    var sVal = ParseValue(line, 'S');
                    if (sVal.HasValue) _currentS = (int)sVal.Value;
                    currentToolpath.SpindleRPM = _currentS;
                    break;
                case 6: // Tool change - new toolpath
                    if (currentToolpath.Moves.Count > 0)
                    {
                        toolpaths.Add(currentToolpath);
                        currentToolpath = new Toolpath { Name = $"Toolpath {toolpaths.Count + 1}" };
                    }
                    break;
            }
        }
        
        // Parse coordinates
        double? newX = ParseValue(line, 'X');
        double? newY = ParseValue(line, 'Y');
        double? newZ = ParseValue(line, 'Z');
        double? newF = ParseValue(line, 'F');
        double? newI = ParseValue(line, 'I');
        double? newJ = ParseValue(line, 'J');
        
        if (newF.HasValue) _currentF = newF.Value;
        
        // Check if this line has movement
        if (newX.HasValue || newY.HasValue || newZ.HasValue || hasMove)
        {
            double scale = _isMetric ? 1.0 : 25.4;
            
            if (_isAbsolute)
            {
                if (newX.HasValue) _currentX = newX.Value;
                if (newY.HasValue) _currentY = newY.Value;
                if (newZ.HasValue) _currentZ = newZ.Value;
            }
            else
            {
                if (newX.HasValue) _currentX += newX.Value;
                if (newY.HasValue) _currentY += newY.Value;
                if (newZ.HasValue) _currentZ += newZ.Value;
            }
            
            var move = new ToolpathMove
            {
                Type = moveType,
                X = _currentX * scale,
                Y = _currentY * scale,
                Z = _currentZ * scale,
                F = _currentF * scale,
                I = (newI ?? 0) * scale,
                J = (newJ ?? 0) * scale
            };
            
            currentToolpath.Moves.Add(move);
        }
    }
    
    private double? ParseValue(string line, char code)
    {
        var match = Regex.Match(line, $@"{code}(-?\d*\.?\d+)");
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        return null;
    }
    
    private string StripComments(string line)
    {
        // Remove parentheses comments
        line = Regex.Replace(line, @"\([^)]*\)", "");
        // Remove semicolon comments
        int semicolon = line.IndexOf(';');
        if (semicolon >= 0) line = line.Substring(0, semicolon);
        return line.Trim();
    }
    
    private void AddPath(Layer layer, List<Point2D> points)
    {
        if (points.Count < 2) return;
        
        var path = new PolyPath();
        foreach (var pt in points)
        {
            path.Segments.Add(new LineSegment { EndPoint = pt });
        }
        
        // Check if closed
        if (points.Count > 2)
        {
            var first = points[0];
            var last = points[^1];
            if (Math.Abs(first.X - last.X) < 0.1 && Math.Abs(first.Y - last.Y) < 0.1)
            {
                path.IsClosed = true;
            }
        }
        
        layer.Paths.Add(path);
    }
}
