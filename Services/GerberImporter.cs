using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Imports Gerber files (RS-274X format) for PCB manufacturing
/// </summary>
public class GerberImporter
{
    private readonly List<Layer> _layers = new();
    private readonly Dictionary<string, GerberAperture> _apertures = new();
    
    // State
    private double _currentX = 0;
    private double _currentY = 0;
    private GerberAperture? _currentAperture;
    private bool _darkPolarity = true;  // true = draw, false = clear
    private GerberInterpolation _interpolation = GerberInterpolation.Linear;
    private bool _regionMode = false;
    private bool _multiQuadrant = true;
    
    // Units (default inches, converted to mm)
    private double _scale = 25.4; // inches to mm
    private int _xDecimals = 4;
    private int _yDecimals = 4;
    
    public List<Layer> Import(string filePath)
    {
        _layers.Clear();
        _apertures.Clear();
        _currentX = 0;
        _currentY = 0;
        
        var layer = new Layer { Name = Path.GetFileNameWithoutExtension(filePath), Color = "#00D4AA" };
        _layers.Add(layer);
        
        var lines = File.ReadAllLines(filePath);
        var currentPath = new List<Point2D>();
        
        foreach (var line in lines)
        {
            ProcessLine(line.Trim(), layer, currentPath);
        }
        
        // Finalize any open path
        if (currentPath.Count > 0)
        {
            AddPath(layer, currentPath, false);
        }
        
        return _layers;
    }
    
    private void ProcessLine(string line, Layer layer, List<Point2D> currentPath)
    {
        if (string.IsNullOrEmpty(line) || line.StartsWith("G04")) return; // Comment
        
        // Format specification
        if (line.StartsWith("%FS"))
        {
            ParseFormatSpec(line);
            return;
        }
        
        // Units
        if (line.StartsWith("%MO"))
        {
            _scale = line.Contains("IN") ? 25.4 : 1.0;
            return;
        }
        
        // Aperture definition
        if (line.StartsWith("%AD"))
        {
            ParseApertureDefinition(line);
            return;
        }
        
        // Polarity
        if (line.StartsWith("%LP"))
        {
            _darkPolarity = line.Contains("D");
            return;
        }
        
        // Interpolation mode
        if (line.StartsWith("G01") || line.Contains("G01"))
        {
            _interpolation = GerberInterpolation.Linear;
        }
        else if (line.StartsWith("G02") || line.Contains("G02"))
        {
            _interpolation = GerberInterpolation.ClockwiseArc;
        }
        else if (line.StartsWith("G03") || line.Contains("G03"))
        {
            _interpolation = GerberInterpolation.CounterClockwiseArc;
        }
        
        // Region mode
        if (line.StartsWith("G36") || line.Contains("G36"))
        {
            _regionMode = true;
            currentPath.Clear();
        }
        else if (line.StartsWith("G37") || line.Contains("G37"))
        {
            if (currentPath.Count > 0)
            {
                AddPath(layer, currentPath, true);
                currentPath.Clear();
            }
            _regionMode = false;
        }
        
        // Quadrant mode
        if (line.Contains("G74"))
        {
            _multiQuadrant = false;
        }
        else if (line.Contains("G75"))
        {
            _multiQuadrant = true;
        }
        
        // Aperture selection
        var apertureMatch = Regex.Match(line, @"D(\d+)\*?$");
        if (apertureMatch.Success && int.Parse(apertureMatch.Groups[1].Value) >= 10)
        {
            var apNum = apertureMatch.Groups[1].Value;
            if (_apertures.TryGetValue(apNum, out var ap))
            {
                _currentAperture = ap;
            }
        }
        
        // Coordinate data
        ParseCoordinates(line, layer, currentPath);
    }
    
    private void ParseFormatSpec(string line)
    {
        // %FSLAX24Y24*% - Leading zeros omitted, Absolute, X format 2.4, Y format 2.4
        var match = Regex.Match(line, @"%FS([LA])([AI])X(\d)(\d)Y(\d)(\d)");
        if (match.Success)
        {
            _xDecimals = int.Parse(match.Groups[4].Value);
            _yDecimals = int.Parse(match.Groups[6].Value);
        }
    }
    
    private void ParseApertureDefinition(string line)
    {
        // %ADD10C,0.01*% - Aperture D10, Circle, diameter 0.01
        // %ADD11R,0.06X0.06*% - Aperture D11, Rectangle, 0.06x0.06
        var match = Regex.Match(line, @"%ADD(\d+)([A-Z]),([^*]+)\*%?");
        if (match.Success)
        {
            var number = match.Groups[1].Value;
            var shape = match.Groups[2].Value;
            var paramsStr = match.Groups[3].Value;
            var parameters = paramsStr.Split('X').Select(p => ParseDouble(p)).ToArray();
            
            _apertures[number] = new GerberAperture
            {
                Number = number,
                Shape = shape switch
                {
                    "C" => ApertureShape.Circle,
                    "R" => ApertureShape.Rectangle,
                    "O" => ApertureShape.Obround,
                    "P" => ApertureShape.Polygon,
                    _ => ApertureShape.Circle
                },
                Parameters = parameters
            };
        }
    }
    
    private void ParseCoordinates(string line, Layer layer, List<Point2D> currentPath)
    {
        // Parse X, Y, I, J coordinates and D codes
        double? newX = null, newY = null, i = null, j = null;
        int? dCode = null;
        
        var xMatch = Regex.Match(line, @"X(-?\d+)");
        if (xMatch.Success)
        {
            newX = ParseCoordinate(xMatch.Groups[1].Value, _xDecimals);
        }
        
        var yMatch = Regex.Match(line, @"Y(-?\d+)");
        if (yMatch.Success)
        {
            newY = ParseCoordinate(yMatch.Groups[1].Value, _yDecimals);
        }
        
        var iMatch = Regex.Match(line, @"I(-?\d+)");
        if (iMatch.Success)
        {
            i = ParseCoordinate(iMatch.Groups[1].Value, _xDecimals);
        }
        
        var jMatch = Regex.Match(line, @"J(-?\d+)");
        if (jMatch.Success)
        {
            j = ParseCoordinate(jMatch.Groups[1].Value, _yDecimals);
        }
        
        var dMatch = Regex.Match(line, @"D0([123])\*?");
        if (dMatch.Success)
        {
            dCode = int.Parse(dMatch.Groups[1].Value);
        }
        
        // Apply coordinates
        var targetX = (newX ?? _currentX) * _scale;
        var targetY = (newY ?? _currentY) * _scale;
        
        if (dCode == 1) // Interpolate (draw)
        {
            if (_regionMode)
            {
                currentPath.Add(new Point2D(targetX, targetY));
            }
            else
            {
                // Single line/arc segment
                var path = new PolyPath { IsClosed = false };
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(_currentX * _scale, _currentY * _scale) });
                
                if (_interpolation == GerberInterpolation.Linear)
                {
                    path.Segments.Add(new LineSegment { EndPoint = new Point2D(targetX, targetY) });
                }
                else
                {
                    // Arc - linearize
                    var arcPoints = LinearizeArc(
                        _currentX * _scale, _currentY * _scale,
                        targetX, targetY,
                        (i ?? 0) * _scale, (j ?? 0) * _scale,
                        _interpolation == GerberInterpolation.ClockwiseArc
                    );
                    foreach (var pt in arcPoints)
                    {
                        path.Segments.Add(new LineSegment { EndPoint = pt });
                    }
                }
                
                layer.Paths.Add(path);
            }
        }
        else if (dCode == 2) // Move (pen up)
        {
            if (_regionMode && currentPath.Count > 0)
            {
                // Start new contour in region
            }
        }
        else if (dCode == 3) // Flash
        {
            if (_currentAperture != null)
            {
                // Create aperture shape at position
                var aperturePath = CreateApertureShape(_currentAperture, targetX, targetY);
                if (aperturePath != null)
                {
                    layer.Paths.Add(aperturePath);
                }
            }
        }
        
        if (newX.HasValue) _currentX = newX.Value;
        if (newY.HasValue) _currentY = newY.Value;
    }
    
    private double ParseCoordinate(string value, int decimals)
    {
        // Convert integer to decimal (e.g., "1234" with 4 decimals = 0.1234)
        if (double.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        {
            return v / Math.Pow(10, decimals);
        }
        return 0;
    }
    
    private double ParseDouble(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    
    private List<Point2D> LinearizeArc(double x1, double y1, double x2, double y2, double i, double j, bool clockwise, int segments = 32)
    {
        var points = new List<Point2D>();
        
        double cx = x1 + i;
        double cy = y1 + j;
        double radius = Math.Sqrt(i * i + j * j);
        
        double startAngle = Math.Atan2(y1 - cy, x1 - cx);
        double endAngle = Math.Atan2(y2 - cy, x2 - cx);
        
        if (clockwise)
        {
            if (endAngle >= startAngle) endAngle -= 2 * Math.PI;
        }
        else
        {
            if (endAngle <= startAngle) endAngle += 2 * Math.PI;
        }
        
        double span = endAngle - startAngle;
        int numSegments = Math.Max(4, (int)(Math.Abs(span) / (2 * Math.PI) * segments));
        
        for (int seg = 1; seg <= numSegments; seg++)
        {
            double t = (double)seg / numSegments;
            double angle = startAngle + span * t;
            points.Add(new Point2D(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)));
        }
        
        return points;
    }
    
    private PolyPath? CreateApertureShape(GerberAperture aperture, double x, double y)
    {
        var path = new PolyPath { IsClosed = true };
        
        switch (aperture.Shape)
        {
            case ApertureShape.Circle:
                double radius = (aperture.Parameters.Length > 0 ? aperture.Parameters[0] : 0.1) * _scale / 2;
                for (int i = 0; i <= 32; i++)
                {
                    double angle = 2 * Math.PI * i / 32;
                    path.Segments.Add(new LineSegment
                    {
                        EndPoint = new Point2D(x + radius * Math.Cos(angle), y + radius * Math.Sin(angle))
                    });
                }
                break;
                
            case ApertureShape.Rectangle:
                double w = (aperture.Parameters.Length > 0 ? aperture.Parameters[0] : 0.1) * _scale / 2;
                double h = (aperture.Parameters.Length > 1 ? aperture.Parameters[1] : aperture.Parameters[0]) * _scale / 2;
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(x - w, y - h) });
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(x + w, y - h) });
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(x + w, y + h) });
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(x - w, y + h) });
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(x - w, y - h) });
                break;
                
            default:
                return null;
        }
        
        return path;
    }
    
    private void AddPath(Layer layer, List<Point2D> points, bool closed)
    {
        if (points.Count < 2) return;
        
        var path = new PolyPath { IsClosed = closed };
        foreach (var pt in points)
        {
            path.Segments.Add(new LineSegment { EndPoint = pt });
        }
        
        layer.Paths.Add(path);
    }
}

public class GerberAperture
{
    public string Number { get; set; } = "";
    public ApertureShape Shape { get; set; }
    public double[] Parameters { get; set; } = Array.Empty<double>();
}

public enum ApertureShape
{
    Circle,
    Rectangle,
    Obround,
    Polygon
}

public enum GerberInterpolation
{
    Linear,
    ClockwiseArc,
    CounterClockwiseArc
}
