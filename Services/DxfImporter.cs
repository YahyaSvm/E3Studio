using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Imports DXF (Drawing Exchange Format) files
/// </summary>
public class DxfImporter
{
    private readonly List<Layer> _layers = new();
    
    public List<Layer> Import(string filePath)
    {
        _layers.Clear();
        _layers.Add(new Layer { Name = "Default", Color = "#00D4AA" });
        
        var lines = File.ReadAllLines(filePath);
        int i = 0;
        
        while (i < lines.Length)
        {
            var code = lines[i].Trim();
            var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            
            if (code == "0")
            {
                switch (value)
                {
                    case "LINE":
                        i = ParseLine(lines, i + 2);
                        break;
                    case "CIRCLE":
                        i = ParseCircle(lines, i + 2);
                        break;
                    case "ARC":
                        i = ParseArc(lines, i + 2);
                        break;
                    case "POLYLINE":
                    case "LWPOLYLINE":
                        i = ParsePolyline(lines, i + 2, value == "LWPOLYLINE");
                        break;
                    case "SPLINE":
                        i = ParseSpline(lines, i + 2);
                        break;
                    case "ELLIPSE":
                        i = ParseEllipse(lines, i + 2);
                        break;
                    default:
                        i += 2;
                        break;
                }
            }
            else
            {
                i += 2;
            }
        }
        
        return _layers;
    }
    
    private Layer GetOrCreateLayer(string name)
    {
        var layer = _layers.Find(l => l.Name == name);
        if (layer == null)
        {
            layer = new Layer { Name = name, Color = GetColorForLayer(name) };
            _layers.Add(layer);
        }
        return layer;
    }
    
    private string GetColorForLayer(string name)
    {
        // Simple hash-based color assignment
        int hash = name.GetHashCode();
        int r = (hash & 0xFF0000) >> 16;
        int g = (hash & 0x00FF00) >> 8;
        int b = hash & 0x0000FF;
        
        // Ensure minimum brightness
        r = Math.Max(100, r);
        g = Math.Max(100, g);
        b = Math.Max(100, b);
        
        return $"#{r:X2}{g:X2}{b:X2}";
    }
    
    private int ParseLine(string[] lines, int start)
    {
        double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
        string layerName = "0";
        int i = start;
        
        while (i < lines.Length)
        {
            var code = lines[i].Trim();
            if (code == "0") break;
            
            var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            
            switch (code)
            {
                case "8": layerName = value; break;
                case "10": x1 = ParseDouble(value); break;
                case "20": y1 = ParseDouble(value); break;
                case "11": x2 = ParseDouble(value); break;
                case "21": y2 = ParseDouble(value); break;
            }
            
            i += 2;
        }
        
        var layer = GetOrCreateLayer(layerName);
        var path = new PolyPath { IsClosed = false };
        path.Segments.Add(new LineSegment { EndPoint = new Point2D(x1, y1) });
        path.Segments.Add(new LineSegment { EndPoint = new Point2D(x2, y2) });
        layer.Paths.Add(path);
        
        return i;
    }
    
    private int ParseCircle(string[] lines, int start)
    {
        double cx = 0, cy = 0, radius = 0;
        string layerName = "0";
        int i = start;
        
        while (i < lines.Length)
        {
            var code = lines[i].Trim();
            if (code == "0") break;
            
            var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            
            switch (code)
            {
                case "8": layerName = value; break;
                case "10": cx = ParseDouble(value); break;
                case "20": cy = ParseDouble(value); break;
                case "40": radius = ParseDouble(value); break;
            }
            
            i += 2;
        }
        
        // Convert circle to polyline approximation
        var layer = GetOrCreateLayer(layerName);
        var path = new PolyPath { IsClosed = true };
        
        int segments = 36;
        for (int j = 0; j <= segments; j++)
        {
            double angle = 2 * Math.PI * j / segments;
            double x = cx + radius * Math.Cos(angle);
            double y = cy + radius * Math.Sin(angle);
            path.Segments.Add(new LineSegment { EndPoint = new Point2D(x, y) });
        }
        
        layer.Paths.Add(path);
        return i;
    }
    
    private int ParseArc(string[] lines, int start)
    {
        double cx = 0, cy = 0, radius = 0;
        double startAngle = 0, endAngle = 360;
        string layerName = "0";
        int i = start;
        
        while (i < lines.Length)
        {
            var code = lines[i].Trim();
            if (code == "0") break;
            
            var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            
            switch (code)
            {
                case "8": layerName = value; break;
                case "10": cx = ParseDouble(value); break;
                case "20": cy = ParseDouble(value); break;
                case "40": radius = ParseDouble(value); break;
                case "50": startAngle = ParseDouble(value); break;
                case "51": endAngle = ParseDouble(value); break;
            }
            
            i += 2;
        }
        
        var layer = GetOrCreateLayer(layerName);
        var path = new PolyPath { IsClosed = false };
        
        // Convert to radians
        startAngle *= Math.PI / 180;
        endAngle *= Math.PI / 180;
        
        if (endAngle < startAngle) endAngle += 2 * Math.PI;
        
        int segments = Math.Max(8, (int)((endAngle - startAngle) / (Math.PI / 18)));
        double step = (endAngle - startAngle) / segments;
        
        for (int j = 0; j <= segments; j++)
        {
            double angle = startAngle + step * j;
            double x = cx + radius * Math.Cos(angle);
            double y = cy + radius * Math.Sin(angle);
            path.Segments.Add(new LineSegment { EndPoint = new Point2D(x, y) });
        }
        
        layer.Paths.Add(path);
        return i;
    }
    
    private int ParsePolyline(string[] lines, int start, bool isLW)
    {
        var points = new List<(double x, double y, double bulge)>();
        string layerName = "0";
        bool isClosed = false;
        int i = start;
        
        double currentX = 0, currentY = 0, currentBulge = 0;
        
        if (isLW)
        {
            // LWPOLYLINE - all data in one entity
            while (i < lines.Length)
            {
                var code = lines[i].Trim();
                if (code == "0") break;
                
                var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
                
                switch (code)
                {
                    case "8": layerName = value; break;
                    case "70": 
                        int flags = int.TryParse(value, out var f) ? f : 0;
                        isClosed = (flags & 1) == 1;
                        break;
                    case "10": 
                        if (currentX != 0 || currentY != 0)
                            points.Add((currentX, currentY, currentBulge));
                        currentX = ParseDouble(value);
                        currentBulge = 0;
                        break;
                    case "20": currentY = ParseDouble(value); break;
                    case "42": currentBulge = ParseDouble(value); break;
                }
                
                i += 2;
            }
            
            // Add last point
            if (currentX != 0 || currentY != 0)
                points.Add((currentX, currentY, currentBulge));
        }
        else
        {
            // Old-style POLYLINE with VERTEX entities
            while (i < lines.Length)
            {
                var code = lines[i].Trim();
                if (code == "0")
                {
                    var entityType = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
                    if (entityType == "SEQEND") { i += 2; break; }
                    if (entityType == "VERTEX")
                    {
                        i += 2;
                        double vx = 0, vy = 0, vbulge = 0;
                        
                        while (i < lines.Length)
                        {
                            var vcode = lines[i].Trim();
                            if (vcode == "0") break;
                            
                            var vvalue = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
                            
                            switch (vcode)
                            {
                                case "10": vx = ParseDouble(vvalue); break;
                                case "20": vy = ParseDouble(vvalue); break;
                                case "42": vbulge = ParseDouble(vvalue); break;
                            }
                            
                            i += 2;
                        }
                        
                        points.Add((vx, vy, vbulge));
                        continue;
                    }
                }
                
                var mainValue = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
                
                switch (code)
                {
                    case "8": layerName = mainValue; break;
                    case "70":
                        int flags = int.TryParse(mainValue, out var f) ? f : 0;
                        isClosed = (flags & 1) == 1;
                        break;
                }
                
                i += 2;
            }
        }
        
        if (points.Count >= 2)
        {
            var layer = GetOrCreateLayer(layerName);
            var path = new PolyPath { IsClosed = isClosed };
            
            foreach (var pt in points)
            {
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(pt.x, pt.y) });
            }
            
            if (isClosed && points.Count > 0)
            {
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(points[0].x, points[0].y) });
            }
            
            layer.Paths.Add(path);
        }
        
        return i;
    }
    
    private int ParseSpline(string[] lines, int start)
    {
        var controlPoints = new List<(double x, double y)>();
        string layerName = "0";
        bool isClosed = false;
        int i = start;
        
        double currentX = 0;
        
        while (i < lines.Length)
        {
            var code = lines[i].Trim();
            if (code == "0") break;
            
            var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            
            switch (code)
            {
                case "8": layerName = value; break;
                case "70":
                    int flags = int.TryParse(value, out var f) ? f : 0;
                    isClosed = (flags & 1) == 1;
                    break;
                case "10": currentX = ParseDouble(value); break;
                case "20":
                    controlPoints.Add((currentX, ParseDouble(value)));
                    break;
            }
            
            i += 2;
        }
        
        if (controlPoints.Count >= 2)
        {
            var layer = GetOrCreateLayer(layerName);
            var path = new PolyPath { IsClosed = isClosed };
            
            // Approximate spline with line segments
            var interpolated = InterpolateSpline(controlPoints, 50);
            foreach (var pt in interpolated)
            {
                path.Segments.Add(new LineSegment { EndPoint = new Point2D(pt.x, pt.y) });
            }
            
            layer.Paths.Add(path);
        }
        
        return i;
    }
    
    private int ParseEllipse(string[] lines, int start)
    {
        double cx = 0, cy = 0;
        double majorX = 1, majorY = 0;
        double ratio = 1;
        double startParam = 0, endParam = 2 * Math.PI;
        string layerName = "0";
        int i = start;
        
        while (i < lines.Length)
        {
            var code = lines[i].Trim();
            if (code == "0") break;
            
            var value = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
            
            switch (code)
            {
                case "8": layerName = value; break;
                case "10": cx = ParseDouble(value); break;
                case "20": cy = ParseDouble(value); break;
                case "11": majorX = ParseDouble(value); break;
                case "21": majorY = ParseDouble(value); break;
                case "40": ratio = ParseDouble(value); break;
                case "41": startParam = ParseDouble(value); break;
                case "42": endParam = ParseDouble(value); break;
            }
            
            i += 2;
        }
        
        var layer = GetOrCreateLayer(layerName);
        var path = new PolyPath { IsClosed = Math.Abs(endParam - startParam - 2 * Math.PI) < 0.01 };
        
        double majorLength = Math.Sqrt(majorX * majorX + majorY * majorY);
        double minorLength = majorLength * ratio;
        double rotation = Math.Atan2(majorY, majorX);
        
        int segments = 36;
        double step = (endParam - startParam) / segments;
        
        for (int j = 0; j <= segments; j++)
        {
            double t = startParam + step * j;
            double ex = majorLength * Math.Cos(t);
            double ey = minorLength * Math.Sin(t);
            
            // Rotate
            double rx = ex * Math.Cos(rotation) - ey * Math.Sin(rotation);
            double ry = ex * Math.Sin(rotation) + ey * Math.Cos(rotation);
            
            path.Segments.Add(new LineSegment { EndPoint = new Point2D(cx + rx, cy + ry) });
        }
        
        layer.Paths.Add(path);
        return i;
    }
    
    private List<(double x, double y)> InterpolateSpline(List<(double x, double y)> controlPoints, int segments)
    {
        var result = new List<(double x, double y)>();
        
        if (controlPoints.Count < 2)
        {
            result.AddRange(controlPoints);
            return result;
        }
        
        // Simple Catmull-Rom spline interpolation
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            var p0 = controlPoints[Math.Max(0, i - 1)];
            var p1 = controlPoints[i];
            var p2 = controlPoints[Math.Min(controlPoints.Count - 1, i + 1)];
            var p3 = controlPoints[Math.Min(controlPoints.Count - 1, i + 2)];
            
            int segmentsPerSection = segments / (controlPoints.Count - 1);
            
            for (int j = 0; j <= segmentsPerSection; j++)
            {
                double t = (double)j / segmentsPerSection;
                double t2 = t * t;
                double t3 = t2 * t;
                
                double x = 0.5 * ((2 * p1.x) +
                           (-p0.x + p2.x) * t +
                           (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 +
                           (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3);
                           
                double y = 0.5 * ((2 * p1.y) +
                           (-p0.y + p2.y) * t +
                           (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 +
                           (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3);
                
                result.Add((x, y));
            }
        }
        
        return result;
    }
    
    private double ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }
}
