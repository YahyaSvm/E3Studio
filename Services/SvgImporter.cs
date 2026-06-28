using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Parses SVG files and converts them to internal geometry
/// </summary>
public class SvgImporter
{
    private readonly List<Layer> _layers = new();
    private readonly Dictionary<string, string> _styles = new();
    
    public List<Layer> Import(string filePath)
    {
        _layers.Clear();
        _styles.Clear();
        
        var doc = XDocument.Load(filePath);
        var root = doc.Root;
        
        if (root == null || root.Name.LocalName != "svg")
        {
            throw new Exception("Invalid SVG file");
        }
        
        // Get viewBox for scaling
        var viewBox = root.Attribute("viewBox")?.Value;
        double offsetX = 0, offsetY = 0;
        
        if (!string.IsNullOrEmpty(viewBox))
        {
            var parts = viewBox.Split(' ', ',');
            if (parts.Length >= 4)
            {
                offsetX = ParseDouble(parts[0]);
                offsetY = ParseDouble(parts[1]);
            }
        }
        
        // Create default layer
        var defaultLayer = new Layer { Name = "Default", Color = "#00D4AA" };
        _layers.Add(defaultLayer);
        
        // Parse all elements
        ParseElement(root, defaultLayer, Matrix3x2.Identity);
        
        return _layers;
    }
    
    private void ParseElement(XElement element, Layer currentLayer, Matrix3x2 transform)
    {
        // Check for transform attribute
        var transformAttr = element.Attribute("transform")?.Value;
        if (!string.IsNullOrEmpty(transformAttr))
        {
            transform = transform * ParseTransform(transformAttr);
        }
        
        // Check for group with id (could be a layer)
        if (element.Name.LocalName == "g")
        {
            var id = element.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(id) && !id.StartsWith("_"))
            {
                // Create new layer for this group
                var layer = new Layer { Name = id, Color = GetRandomColor() };
                _layers.Add(layer);
                currentLayer = layer;
            }
        }
        
        // Parse element based on type
        switch (element.Name.LocalName)
        {
            case "path":
                ParsePath(element, currentLayer, transform);
                break;
            case "rect":
                ParseRect(element, currentLayer, transform);
                break;
            case "circle":
                ParseCircle(element, currentLayer, transform);
                break;
            case "ellipse":
                ParseEllipse(element, currentLayer, transform);
                break;
            case "line":
                ParseLine(element, currentLayer, transform);
                break;
            case "polyline":
            case "polygon":
                ParsePolyline(element, currentLayer, transform, element.Name.LocalName == "polygon");
                break;
        }
        
        // Recurse into children
        foreach (var child in element.Elements())
        {
            ParseElement(child, currentLayer, transform);
        }
    }
    
    private void ParsePath(XElement element, Layer layer, Matrix3x2 transform)
    {
        var d = element.Attribute("d")?.Value;
        if (string.IsNullOrEmpty(d)) return;
        
        var path = new PolyPath();
        var segments = ParsePathData(d);
        
        // Apply transform to all points
        foreach (var seg in segments)
        {
            seg.EndPoint = transform.Transform(seg.EndPoint);
            if (seg is ArcSegment arc)
            {
                arc.Center = transform.Transform(arc.Center);
            }
        }
        
        path.Segments = segments;
        path.IsClosed = d.TrimEnd().EndsWith("Z", StringComparison.OrdinalIgnoreCase);
        
        layer.Paths.Add(path);
    }
    
    private List<PathSegment> ParsePathData(string d)
    {
        var segments = new List<PathSegment>();
        
        // Tokenize path data
        var tokens = Regex.Matches(d, @"[MmLlHhVvCcSsQqTtAaZz]|[-+]?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?");
        
        Point2D currentPoint = new(0, 0);
        Point2D startPoint = new(0, 0);
        char currentCommand = 'M';
        var numbers = new List<double>();
        
        foreach (Match token in tokens)
        {
            var value = token.Value;
            
            if (Regex.IsMatch(value, @"^[MmLlHhVvCcSsQqTtAaZz]$"))
            {
                // Process previous command
                ProcessPathCommand(currentCommand, numbers, ref currentPoint, ref startPoint, segments);
                numbers.Clear();
                currentCommand = value[0];
            }
            else
            {
                numbers.Add(ParseDouble(value));
            }
        }
        
        // Process last command
        ProcessPathCommand(currentCommand, numbers, ref currentPoint, ref startPoint, segments);
        
        return segments;
    }
    
    private void ProcessPathCommand(char cmd, List<double> numbers, ref Point2D current, ref Point2D start, List<PathSegment> segments)
    {
        bool relative = char.IsLower(cmd);
        cmd = char.ToUpper(cmd);
        
        int i = 0;
        while (i < numbers.Count || cmd == 'Z')
        {
            switch (cmd)
            {
                case 'M': // MoveTo
                    if (i + 1 < numbers.Count)
                    {
                        var x = numbers[i++];
                        var y = numbers[i++];
                        current = relative ? new Point2D(current.X + x, current.Y + y) : new Point2D(x, y);
                        start = current;
                        cmd = relative ? 'l' : 'L'; // Subsequent coords are LineTo
                    }
                    else return;
                    break;
                    
                case 'L': // LineTo
                    if (i + 1 < numbers.Count)
                    {
                        var x = numbers[i++];
                        var y = numbers[i++];
                        var end = relative ? new Point2D(current.X + x, current.Y + y) : new Point2D(x, y);
                        segments.Add(new LineSegment { EndPoint = end });
                        current = end;
                    }
                    else return;
                    break;
                    
                case 'H': // Horizontal LineTo
                    if (i < numbers.Count)
                    {
                        var x = numbers[i++];
                        var end = new Point2D(relative ? current.X + x : x, current.Y);
                        segments.Add(new LineSegment { EndPoint = end });
                        current = end;
                    }
                    else return;
                    break;
                    
                case 'V': // Vertical LineTo
                    if (i < numbers.Count)
                    {
                        var y = numbers[i++];
                        var end = new Point2D(current.X, relative ? current.Y + y : y);
                        segments.Add(new LineSegment { EndPoint = end });
                        current = end;
                    }
                    else return;
                    break;
                    
                case 'C': // Cubic Bezier - approximate with lines for now
                    if (i + 5 < numbers.Count)
                    {
                        i += 4; // Skip control points
                        var x = numbers[i++];
                        var y = numbers[i++];
                        var end = relative ? new Point2D(current.X + x, current.Y + y) : new Point2D(x, y);
                        segments.Add(new LineSegment { EndPoint = end });
                        current = end;
                    }
                    else return;
                    break;
                    
                case 'S': // Smooth Cubic Bezier
                    if (i + 3 < numbers.Count)
                    {
                        i += 2; // Skip control point
                        var x = numbers[i++];
                        var y = numbers[i++];
                        var end = relative ? new Point2D(current.X + x, current.Y + y) : new Point2D(x, y);
                        segments.Add(new LineSegment { EndPoint = end });
                        current = end;
                    }
                    else return;
                    break;
                    
                case 'Q': // Quadratic Bezier
                    if (i + 3 < numbers.Count)
                    {
                        i += 2; // Skip control point
                        var x = numbers[i++];
                        var y = numbers[i++];
                        var end = relative ? new Point2D(current.X + x, current.Y + y) : new Point2D(x, y);
                        segments.Add(new LineSegment { EndPoint = end });
                        current = end;
                    }
                    else return;
                    break;
                    
                case 'T': // Smooth Quadratic Bezier
                    if (i + 1 < numbers.Count)
                    {
                        var x = numbers[i++];
                        var y = numbers[i++];
                        var end = relative ? new Point2D(current.X + x, current.Y + y) : new Point2D(x, y);
                        segments.Add(new LineSegment { EndPoint = end });
                        current = end;
                    }
                    else return;
                    break;
                    
                case 'A': // Arc
                    if (i + 6 < numbers.Count)
                    {
                        var rx = numbers[i++];
                        var ry = numbers[i++];
                        var rotation = numbers[i++];
                        var largeArc = numbers[i++] != 0;
                        var sweep = numbers[i++] != 0;
                        var x = numbers[i++];
                        var y = numbers[i++];
                        var end = relative ? new Point2D(current.X + x, current.Y + y) : new Point2D(x, y);
                        var startPt = current;
                        int segmentsCount = largeArc ? 12 : 8;
                        for (int s = 1; s <= segmentsCount; s++)
                        {
                            double t = (double)s / segmentsCount;
                            var pt = new Point2D(
                                startPt.X + (end.X - startPt.X) * t,
                                startPt.Y + (end.Y - startPt.Y) * t);
                            segments.Add(new LineSegment { EndPoint = pt });
                            current = pt;
                        }
                        current = end;
                    }
                    else return;
                    break;
                    
                case 'Z': // ClosePath
                    if (current.X != start.X || current.Y != start.Y)
                    {
                        segments.Add(new LineSegment { EndPoint = start });
                    }
                    current = start;
                    return;
                    
                default:
                    return;
            }
        }
    }
    
    private void ParseRect(XElement element, Layer layer, Matrix3x2 transform)
    {
        var x = ParseDouble(element.Attribute("x")?.Value ?? "0");
        var y = ParseDouble(element.Attribute("y")?.Value ?? "0");
        var width = ParseDouble(element.Attribute("width")?.Value ?? "0");
        var height = ParseDouble(element.Attribute("height")?.Value ?? "0");
        
        if (width <= 0 || height <= 0) return;
        
        var path = new PolyPath { IsClosed = true };
        
        var p1 = transform.Transform(new Point2D(x, y));
        var p2 = transform.Transform(new Point2D(x + width, y));
        var p3 = transform.Transform(new Point2D(x + width, y + height));
        var p4 = transform.Transform(new Point2D(x, y + height));
        
        path.Segments.Add(new LineSegment { EndPoint = p2 });
        path.Segments.Add(new LineSegment { EndPoint = p3 });
        path.Segments.Add(new LineSegment { EndPoint = p4 });
        path.Segments.Add(new LineSegment { EndPoint = p1 });
        
        layer.Paths.Add(path);
    }
    
    private void ParseCircle(XElement element, Layer layer, Matrix3x2 transform)
    {
        var cx = ParseDouble(element.Attribute("cx")?.Value ?? "0");
        var cy = ParseDouble(element.Attribute("cy")?.Value ?? "0");
        var r = ParseDouble(element.Attribute("r")?.Value ?? "0");
        
        if (r <= 0) return;
        
        // Approximate circle with polygon
        var path = new PolyPath { IsClosed = true };
        int segments = 36;
        
        for (int i = 0; i <= segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            var point = transform.Transform(new Point2D(
                cx + r * Math.Cos(angle),
                cy + r * Math.Sin(angle)
            ));
            path.Segments.Add(new LineSegment { EndPoint = point });
        }
        
        layer.Paths.Add(path);
    }
    
    private void ParseEllipse(XElement element, Layer layer, Matrix3x2 transform)
    {
        var cx = ParseDouble(element.Attribute("cx")?.Value ?? "0");
        var cy = ParseDouble(element.Attribute("cy")?.Value ?? "0");
        var rx = ParseDouble(element.Attribute("rx")?.Value ?? "0");
        var ry = ParseDouble(element.Attribute("ry")?.Value ?? "0");
        
        if (rx <= 0 || ry <= 0) return;
        
        var path = new PolyPath { IsClosed = true };
        int segments = 36;
        
        for (int i = 0; i <= segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            var point = transform.Transform(new Point2D(
                cx + rx * Math.Cos(angle),
                cy + ry * Math.Sin(angle)
            ));
            path.Segments.Add(new LineSegment { EndPoint = point });
        }
        
        layer.Paths.Add(path);
    }
    
    private void ParseLine(XElement element, Layer layer, Matrix3x2 transform)
    {
        var x1 = ParseDouble(element.Attribute("x1")?.Value ?? "0");
        var y1 = ParseDouble(element.Attribute("y1")?.Value ?? "0");
        var x2 = ParseDouble(element.Attribute("x2")?.Value ?? "0");
        var y2 = ParseDouble(element.Attribute("y2")?.Value ?? "0");
        
        var path = new PolyPath { IsClosed = false };
        path.Segments.Add(new LineSegment { EndPoint = transform.Transform(new Point2D(x1, y1)) });
        path.Segments.Add(new LineSegment { EndPoint = transform.Transform(new Point2D(x2, y2)) });
        
        layer.Paths.Add(path);
    }
    
    private void ParsePolyline(XElement element, Layer layer, Matrix3x2 transform, bool closed)
    {
        var points = element.Attribute("points")?.Value;
        if (string.IsNullOrEmpty(points)) return;
        
        var numbers = Regex.Matches(points, @"[-+]?[0-9]*\.?[0-9]+")
            .Select(m => ParseDouble(m.Value))
            .ToList();
        
        if (numbers.Count < 4) return;
        
        var path = new PolyPath { IsClosed = closed };
        
        for (int i = 0; i < numbers.Count - 1; i += 2)
        {
            var point = transform.Transform(new Point2D(numbers[i], numbers[i + 1]));
            path.Segments.Add(new LineSegment { EndPoint = point });
        }
        
        layer.Paths.Add(path);
    }
    
    private Matrix3x2 ParseTransform(string transform)
    {
        var result = Matrix3x2.Identity;
        
        var matches = Regex.Matches(transform, @"(\w+)\s*\(([^)]+)\)");
        foreach (Match match in matches)
        {
            var type = match.Groups[1].Value;
            var values = Regex.Matches(match.Groups[2].Value, @"[-+]?[0-9]*\.?[0-9]+")
                .Select(m => ParseDouble(m.Value))
                .ToArray();
            
            switch (type)
            {
                case "translate":
                    result = result * Matrix3x2.CreateTranslation(
                        values.Length > 0 ? values[0] : 0,
                        values.Length > 1 ? values[1] : 0);
                    break;
                case "scale":
                    result = result * Matrix3x2.CreateScale(
                        values.Length > 0 ? values[0] : 1,
                        values.Length > 1 ? values[1] : values[0]);
                    break;
                case "rotate":
                    var angle = values.Length > 0 ? values[0] * Math.PI / 180 : 0;
                    result = result * Matrix3x2.CreateRotation(angle);
                    break;
                case "matrix":
                    if (values.Length >= 6)
                    {
                        result = result * new Matrix3x2(values[0], values[1], values[2], values[3], values[4], values[5]);
                    }
                    break;
            }
        }
        
        return result;
    }
    
    private static double ParseDouble(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return 0;
    }
    
    private static readonly Random _random = new();
    private static string GetRandomColor()
    {
        var colors = new[] { "#00D4AA", "#0099FF", "#7B61FF", "#F59E0B", "#EF4444", "#10B981" };
        return colors[_random.Next(colors.Length)];
    }
}

/// <summary>
/// Simple 2D transformation matrix
/// </summary>
public struct Matrix3x2
{
    public double M11, M12, M21, M22, M31, M32;
    
    public Matrix3x2(double m11, double m12, double m21, double m22, double m31, double m32)
    {
        M11 = m11; M12 = m12;
        M21 = m21; M22 = m22;
        M31 = m31; M32 = m32;
    }
    
    public static Matrix3x2 Identity => new(1, 0, 0, 1, 0, 0);
    
    public static Matrix3x2 CreateTranslation(double x, double y) => new(1, 0, 0, 1, x, y);
    
    public static Matrix3x2 CreateScale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
    
    public static Matrix3x2 CreateRotation(double radians)
    {
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new Matrix3x2(cos, sin, -sin, cos, 0, 0);
    }
    
    public Point2D Transform(Point2D p) => new(
        p.X * M11 + p.Y * M21 + M31,
        p.X * M12 + p.Y * M22 + M32
    );
    
    public static Matrix3x2 operator *(Matrix3x2 a, Matrix3x2 b) => new(
        a.M11 * b.M11 + a.M12 * b.M21,
        a.M11 * b.M12 + a.M12 * b.M22,
        a.M21 * b.M11 + a.M22 * b.M21,
        a.M21 * b.M12 + a.M22 * b.M22,
        a.M31 * b.M11 + a.M32 * b.M21 + b.M31,
        a.M31 * b.M12 + a.M32 * b.M22 + b.M32
    );
}
