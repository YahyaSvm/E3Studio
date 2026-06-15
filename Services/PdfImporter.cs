using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace E3Studio.Services;

/// <summary>
/// PDF Vector Importer - Extracts vector graphics from PDF files
/// Supports: Lines, Curves, Paths, Text outlines
/// </summary>
public class PdfImporter
{
    public class PdfImportResult
    {
        public List<PathData> Paths { get; set; } = new();
        public double Width { get; set; }
        public double Height { get; set; }
        public string? ErrorMessage { get; set; }
        public int LayerCount { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage);
    }
    
    public class PathData
    {
        public List<Point> Points { get; set; } = new();
        public bool IsClosed { get; set; }
        public string? Layer { get; set; }
        public double StrokeWidth { get; set; }
        public string? Color { get; set; }
        public PathType Type { get; set; }
    }
    
    public enum PathType
    {
        Line,
        Rectangle,
        Circle,
        Bezier,
        Polygon,
        Unknown
    }
    
    private double _scale = 1.0;
    private double _offsetX = 0;
    private double _offsetY = 0;
    
    /// <summary>
    /// Import vector graphics from a PDF file
    /// </summary>
    public PdfImportResult Import(string filePath, double scale = 1.0)
    {
        _scale = scale;
        var result = new PdfImportResult();
        
        try
        {
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File not found";
                return result;
            }
            
            var content = File.ReadAllBytes(filePath);
            var text = Encoding.ASCII.GetString(content);
            
            // Verify PDF header
            if (!text.StartsWith("%PDF"))
            {
                result.ErrorMessage = "Invalid PDF file";
                return result;
            }
            
            // Extract media box (page size)
            var mediaBox = ExtractMediaBox(text);
            result.Width = mediaBox.Width * _scale;
            result.Height = mediaBox.Height * _scale;
            
            _offsetY = mediaBox.Height; // PDF origin is bottom-left
            
            // Find and parse content streams
            var streams = ExtractContentStreams(content, text);
            
            foreach (var stream in streams)
            {
                var paths = ParseContentStream(stream);
                result.Paths.AddRange(paths);
            }
            
            // Post-process paths
            PostProcessPaths(result);
            
            result.LayerCount = result.Paths.Select(p => p.Layer).Distinct().Count();
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Import error: {ex.Message}";
        }
        
        return result;
    }
    
    private Rect ExtractMediaBox(string content)
    {
        // Look for /MediaBox [x1 y1 x2 y2]
        var match = Regex.Match(content, @"/MediaBox\s*\[\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*\]");
        if (match.Success)
        {
            double.TryParse(match.Groups[1].Value, out double x1);
            double.TryParse(match.Groups[2].Value, out double y1);
            double.TryParse(match.Groups[3].Value, out double x2);
            double.TryParse(match.Groups[4].Value, out double y2);
            
            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }
        
        return new Rect(0, 0, 612, 792); // Default letter size
    }
    
    private List<string> ExtractContentStreams(byte[] content, string text)
    {
        var streams = new List<string>();
        
        // Find stream...endstream sections
        var streamPattern = @"stream\s*\n([\s\S]*?)\nendstream";
        var matches = Regex.Matches(text, streamPattern);
        
        foreach (Match match in matches)
        {
            var streamContent = match.Groups[1].Value;
            
            // Check if compressed (FlateDecode)
            if (streamContent.Length > 2 && 
                (streamContent[0] == 0x78 || // zlib header
                 IsCompressedStream(content, match.Index)))
            {
                try
                {
                    streamContent = DecompressStream(content, match.Index + "stream\n".Length, 
                        match.Groups[1].Length);
                }
                catch
                {
                    // If decompression fails, skip this stream
                    continue;
                }
            }
            
            streams.Add(streamContent);
        }
        
        return streams;
    }
    
    private bool IsCompressedStream(byte[] content, int position)
    {
        // Look for /Filter /FlateDecode before this stream
        var searchStart = Math.Max(0, position - 500);
        var searchLength = position - searchStart;
        var searchText = Encoding.ASCII.GetString(content, searchStart, searchLength);
        return searchText.Contains("/FlateDecode") || searchText.Contains("/Filter");
    }
    
    private string DecompressStream(byte[] content, int start, int length)
    {
        try
        {
            // Skip zlib header (2 bytes)
            using var input = new MemoryStream(content, start + 2, length - 2);
            using var output = new MemoryStream();
            using var deflate = new System.IO.Compression.DeflateStream(input, 
                System.IO.Compression.CompressionMode.Decompress);
            
            deflate.CopyTo(output);
            return Encoding.ASCII.GetString(output.ToArray());
        }
        catch
        {
            return "";
        }
    }
    
    private List<PathData> ParseContentStream(string stream)
    {
        var paths = new List<PathData>();
        var currentPath = new List<Point>();
        var currentX = 0.0;
        var currentY = 0.0;
        var pathStartX = 0.0;
        var pathStartY = 0.0;
        var ctm = new double[] { 1, 0, 0, 1, 0, 0 }; // Current transformation matrix
        
        // Tokenize stream
        var tokens = Tokenize(stream);
        var stack = new Stack<double>();
        
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            
            // Try parse as number
            if (double.TryParse(token, out double num))
            {
                stack.Push(num);
                continue;
            }
            
            switch (token)
            {
                case "m": // moveto
                    if (stack.Count >= 2)
                    {
                        var y = stack.Pop();
                        var x = stack.Pop();
                        (currentX, currentY) = Transform(x, y, ctm);
                        pathStartX = currentX;
                        pathStartY = currentY;
                        
                        // Start new path if current has points
                        if (currentPath.Count > 0)
                        {
                            var path = new PathData { Points = new List<Point>(currentPath) };
                            paths.Add(path);
                            currentPath.Clear();
                        }
                        
                        currentPath.Add(new Point(currentX * _scale, (_offsetY - currentY) * _scale));
                    }
                    break;
                    
                case "l": // lineto
                    if (stack.Count >= 2)
                    {
                        var y = stack.Pop();
                        var x = stack.Pop();
                        (currentX, currentY) = Transform(x, y, ctm);
                        currentPath.Add(new Point(currentX * _scale, (_offsetY - currentY) * _scale));
                    }
                    break;
                    
                case "c": // curveto (Bezier)
                    if (stack.Count >= 6)
                    {
                        var y3 = stack.Pop();
                        var x3 = stack.Pop();
                        var y2 = stack.Pop();
                        var x2 = stack.Pop();
                        var y1 = stack.Pop();
                        var x1 = stack.Pop();
                        
                        // Flatten Bezier curve
                        var bezierPoints = FlattenCubicBezier(
                            currentX, currentY,
                            x1, y1, x2, y2, x3, y3,
                            ctm);
                        
                        foreach (var pt in bezierPoints)
                        {
                            currentPath.Add(new Point(pt.X * _scale, (_offsetY - pt.Y) * _scale));
                        }
                        
                        (currentX, currentY) = Transform(x3, y3, ctm);
                    }
                    break;
                    
                case "v": // curveto with first control point = current
                    if (stack.Count >= 4)
                    {
                        var y3 = stack.Pop();
                        var x3 = stack.Pop();
                        var y2 = stack.Pop();
                        var x2 = stack.Pop();
                        
                        var bezierPoints = FlattenCubicBezier(
                            currentX, currentY,
                            currentX, currentY, x2, y2, x3, y3,
                            ctm);
                        
                        foreach (var pt in bezierPoints)
                        {
                            currentPath.Add(new Point(pt.X * _scale, (_offsetY - pt.Y) * _scale));
                        }
                        
                        (currentX, currentY) = Transform(x3, y3, ctm);
                    }
                    break;
                    
                case "y": // curveto with last control point = end
                    if (stack.Count >= 4)
                    {
                        var y3 = stack.Pop();
                        var x3 = stack.Pop();
                        var y1 = stack.Pop();
                        var x1 = stack.Pop();
                        
                        var bezierPoints = FlattenCubicBezier(
                            currentX, currentY,
                            x1, y1, x3, y3, x3, y3,
                            ctm);
                        
                        foreach (var pt in bezierPoints)
                        {
                            currentPath.Add(new Point(pt.X * _scale, (_offsetY - pt.Y) * _scale));
                        }
                        
                        (currentX, currentY) = Transform(x3, y3, ctm);
                    }
                    break;
                    
                case "h": // closepath
                    currentPath.Add(new Point(pathStartX * _scale, (_offsetY - pathStartY) * _scale));
                    currentX = pathStartX;
                    currentY = pathStartY;
                    
                    if (currentPath.Count > 1)
                    {
                        paths.Add(new PathData 
                        { 
                            Points = new List<Point>(currentPath), 
                            IsClosed = true 
                        });
                    }
                    currentPath.Clear();
                    break;
                    
                case "re": // rectangle
                    if (stack.Count >= 4)
                    {
                        var h = stack.Pop();
                        var w = stack.Pop();
                        var y = stack.Pop();
                        var x = stack.Pop();
                        
                        var (rx, ry) = Transform(x, y, ctm);
                        var (rx2, ry2) = Transform(x + w, y + h, ctm);
                        
                        paths.Add(new PathData
                        {
                            Points = new List<Point>
                            {
                                new Point(rx * _scale, (_offsetY - ry) * _scale),
                                new Point(rx2 * _scale, (_offsetY - ry) * _scale),
                                new Point(rx2 * _scale, (_offsetY - ry2) * _scale),
                                new Point(rx * _scale, (_offsetY - ry2) * _scale),
                                new Point(rx * _scale, (_offsetY - ry) * _scale)
                            },
                            IsClosed = true,
                            Type = PathType.Rectangle
                        });
                    }
                    break;
                    
                case "S": // stroke
                case "s": // close and stroke
                    if (currentPath.Count > 1)
                    {
                        paths.Add(new PathData 
                        { 
                            Points = new List<Point>(currentPath),
                            IsClosed = token == "s"
                        });
                    }
                    currentPath.Clear();
                    break;
                    
                case "f": // fill (nonzero)
                case "f*": // fill (even-odd)
                case "F": // fill (nonzero, obsolete)
                    if (currentPath.Count > 1)
                    {
                        paths.Add(new PathData 
                        { 
                            Points = new List<Point>(currentPath),
                            IsClosed = true
                        });
                    }
                    currentPath.Clear();
                    break;
                    
                case "B": // fill and stroke
                case "B*":
                case "b": // close, fill and stroke
                case "b*":
                    if (currentPath.Count > 1)
                    {
                        paths.Add(new PathData 
                        { 
                            Points = new List<Point>(currentPath),
                            IsClosed = true
                        });
                    }
                    currentPath.Clear();
                    break;
                    
                case "n": // end path without stroke/fill
                    currentPath.Clear();
                    break;
                    
                case "cm": // concat matrix
                    if (stack.Count >= 6)
                    {
                        var f = stack.Pop();
                        var e = stack.Pop();
                        var d = stack.Pop();
                        var c = stack.Pop();
                        var b = stack.Pop();
                        var a = stack.Pop();
                        ctm = ConcatMatrix(ctm, new[] { a, b, c, d, e, f });
                    }
                    break;
                    
                case "q": // save graphics state
                    // For simplicity, we don't stack transform states
                    break;
                    
                case "Q": // restore graphics state
                    ctm = new double[] { 1, 0, 0, 1, 0, 0 };
                    break;
                    
                case "w": // line width
                    if (stack.Count >= 1)
                    {
                        stack.Pop(); // Ignore for now
                    }
                    break;
            }
        }
        
        // Add remaining path
        if (currentPath.Count > 1)
        {
            paths.Add(new PathData { Points = currentPath });
        }
        
        return paths;
    }
    
    private List<string> Tokenize(string stream)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inString = false;
        int parenDepth = 0;
        
        foreach (char c in stream)
        {
            if (c == '(')
            {
                inString = true;
                parenDepth++;
            }
            else if (c == ')' && inString)
            {
                parenDepth--;
                if (parenDepth == 0) inString = false;
            }
            else if (!inString && (char.IsWhiteSpace(c) || c == '\n' || c == '\r'))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else if (!inString)
            {
                current.Append(c);
            }
        }
        
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }
        
        return tokens;
    }
    
    private (double x, double y) Transform(double x, double y, double[] ctm)
    {
        // Apply transformation matrix: [a b c d e f]
        // x' = ax + cy + e
        // y' = bx + dy + f
        var nx = ctm[0] * x + ctm[2] * y + ctm[4];
        var ny = ctm[1] * x + ctm[3] * y + ctm[5];
        return (nx, ny);
    }
    
    private double[] ConcatMatrix(double[] m1, double[] m2)
    {
        return new double[]
        {
            m1[0] * m2[0] + m1[2] * m2[1],
            m1[1] * m2[0] + m1[3] * m2[1],
            m1[0] * m2[2] + m1[2] * m2[3],
            m1[1] * m2[2] + m1[3] * m2[3],
            m1[0] * m2[4] + m1[2] * m2[5] + m1[4],
            m1[1] * m2[4] + m1[3] * m2[5] + m1[5]
        };
    }
    
    private List<Point> FlattenCubicBezier(double x0, double y0, double x1, double y1,
        double x2, double y2, double x3, double y3, double[] ctm, int segments = 16)
    {
        var points = new List<Point>();
        
        for (int i = 1; i <= segments; i++)
        {
            double t = i / (double)segments;
            double mt = 1 - t;
            
            // Cubic Bezier formula
            double x = mt * mt * mt * x0 + 3 * mt * mt * t * x1 + 3 * mt * t * t * x2 + t * t * t * x3;
            double y = mt * mt * mt * y0 + 3 * mt * mt * t * y1 + 3 * mt * t * t * y2 + t * t * t * y3;
            
            var (tx, ty) = Transform(x, y, ctm);
            points.Add(new Point(tx, ty));
        }
        
        return points;
    }
    
    private void PostProcessPaths(PdfImportResult result)
    {
        // Detect circles
        foreach (var path in result.Paths)
        {
            if (path.Points.Count >= 8 && path.IsClosed)
            {
                var circle = DetectCircle(path.Points);
                if (circle.HasValue)
                {
                    path.Type = PathType.Circle;
                }
            }
        }
        
        // Remove very small paths (noise)
        result.Paths = result.Paths
            .Where(p => p.Points.Count >= 2)
            .Where(p => GetPathLength(p.Points) > 0.1 * _scale)
            .ToList();
    }
    
    private (Point center, double radius)? DetectCircle(List<Point> points)
    {
        if (points.Count < 4) return null;
        
        // Calculate centroid
        var cx = points.Average(p => p.X);
        var cy = points.Average(p => p.Y);
        var center = new Point(cx, cy);
        
        // Calculate average radius
        var radii = points.Select(p => 
            Math.Sqrt(Math.Pow(p.X - cx, 2) + Math.Pow(p.Y - cy, 2))).ToList();
        var avgRadius = radii.Average();
        
        // Check variance
        var variance = radii.Select(r => Math.Abs(r - avgRadius)).Average();
        
        if (variance < avgRadius * 0.1) // Less than 10% variance
        {
            return (center, avgRadius);
        }
        
        return null;
    }
    
    private double GetPathLength(List<Point> points)
    {
        double length = 0;
        for (int i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }
        return length;
    }
    
    /// <summary>
    /// Convert imported PDF paths to geometry suitable for CAM
    /// </summary>
    public List<List<Point>> ConvertToContours(PdfImportResult result)
    {
        return result.Paths
            .Where(p => p.Points.Count >= 2)
            .Select(p => p.Points)
            .ToList();
    }
}
