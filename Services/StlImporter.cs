using System.Globalization;
using System.IO;
using System.Text;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Imports STL (Stereolithography) files for 3D models
/// </summary>
public class StlImporter
{
    public List<StlTriangle> Triangles { get; } = new();
    public double MinX { get; private set; }
    public double MaxX { get; private set; }
    public double MinY { get; private set; }
    public double MaxY { get; private set; }
    public double MinZ { get; private set; }
    public double MaxZ { get; private set; }
    
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double Depth => MaxZ - MinZ;
    
    /// <summary>
    /// Import STL file (auto-detects ASCII or Binary)
    /// </summary>
    public void Import(string filePath)
    {
        Triangles.Clear();
        
        var bytes = File.ReadAllBytes(filePath);
        
        // Check if ASCII or binary
        var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(80, bytes.Length));
        if (header.TrimStart().StartsWith("solid", StringComparison.OrdinalIgnoreCase))
        {
            // Try ASCII first
            try
            {
                ImportAscii(filePath);
                if (Triangles.Count > 0)
                {
                    CalculateBounds();
                    return;
                }
            }
            catch { }
        }
        
        // Binary STL
        ImportBinary(bytes);
        CalculateBounds();
    }
    
    private void ImportAscii(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        StlTriangle? current = null;
        var vertices = new List<StlVertex>();
        
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim().ToLower();
            
            if (line.StartsWith("facet normal"))
            {
                current = new StlTriangle();
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    current.Normal = new StlVertex
                    {
                        X = ParseDouble(parts[2]),
                        Y = ParseDouble(parts[3]),
                        Z = ParseDouble(parts[4])
                    };
                }
                vertices.Clear();
            }
            else if (line.StartsWith("vertex"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    vertices.Add(new StlVertex
                    {
                        X = ParseDouble(parts[1]),
                        Y = ParseDouble(parts[2]),
                        Z = ParseDouble(parts[3])
                    });
                }
            }
            else if (line.StartsWith("endfacet") && current != null && vertices.Count >= 3)
            {
                current.V1 = vertices[0];
                current.V2 = vertices[1];
                current.V3 = vertices[2];
                Triangles.Add(current);
                current = null;
            }
        }
    }
    
    private void ImportBinary(byte[] data)
    {
        if (data.Length < 84) return;
        
        // Skip 80-byte header
        int offset = 80;
        
        // Number of triangles
        uint numTriangles = BitConverter.ToUInt32(data, offset);
        offset += 4;
        
        for (int i = 0; i < numTriangles && offset + 50 <= data.Length; i++)
        {
            var triangle = new StlTriangle();
            
            // Normal
            triangle.Normal = new StlVertex
            {
                X = BitConverter.ToSingle(data, offset),
                Y = BitConverter.ToSingle(data, offset + 4),
                Z = BitConverter.ToSingle(data, offset + 8)
            };
            offset += 12;
            
            // Vertex 1
            triangle.V1 = new StlVertex
            {
                X = BitConverter.ToSingle(data, offset),
                Y = BitConverter.ToSingle(data, offset + 4),
                Z = BitConverter.ToSingle(data, offset + 8)
            };
            offset += 12;
            
            // Vertex 2
            triangle.V2 = new StlVertex
            {
                X = BitConverter.ToSingle(data, offset),
                Y = BitConverter.ToSingle(data, offset + 4),
                Z = BitConverter.ToSingle(data, offset + 8)
            };
            offset += 12;
            
            // Vertex 3
            triangle.V3 = new StlVertex
            {
                X = BitConverter.ToSingle(data, offset),
                Y = BitConverter.ToSingle(data, offset + 4),
                Z = BitConverter.ToSingle(data, offset + 8)
            };
            offset += 12;
            
            // Attribute byte count (usually 0)
            offset += 2;
            
            Triangles.Add(triangle);
        }
    }
    
    private void CalculateBounds()
    {
        if (Triangles.Count == 0) return;
        
        MinX = MinY = MinZ = double.MaxValue;
        MaxX = MaxY = MaxZ = double.MinValue;
        
        foreach (var tri in Triangles)
        {
            foreach (var v in new[] { tri.V1, tri.V2, tri.V3 })
            {
                MinX = Math.Min(MinX, v.X);
                MaxX = Math.Max(MaxX, v.X);
                MinY = Math.Min(MinY, v.Y);
                MaxY = Math.Max(MaxY, v.Y);
                MinZ = Math.Min(MinZ, v.Z);
                MaxZ = Math.Max(MaxZ, v.Z);
            }
        }
    }
    
    /// <summary>
    /// Generate 2D slice at specified Z height
    /// </summary>
    public List<Layer> SliceAtZ(double z)
    {
        var layers = new List<Layer>();
        var layer = new Layer { Name = $"Slice Z={z:F2}" };
        var segments = new List<(Point2D Start, Point2D End)>();
        
        foreach (var tri in Triangles)
        {
            var intersections = GetTriangleIntersection(tri, z);
            if (intersections.Count == 2)
            {
                segments.Add((intersections[0], intersections[1]));
            }
        }
        
        // Connect segments into paths
        var paths = ConnectSegments(segments);
        foreach (var path in paths)
        {
            layer.Paths.Add(path);
        }
        
        layers.Add(layer);
        return layers;
    }
    
    private List<Point2D> GetTriangleIntersection(StlTriangle tri, double z)
    {
        var points = new List<Point2D>();
        var vertices = new[] { tri.V1, tri.V2, tri.V3 };
        
        for (int i = 0; i < 3; i++)
        {
            var v1 = vertices[i];
            var v2 = vertices[(i + 1) % 3];
            
            // Check if edge crosses Z plane
            if ((v1.Z <= z && v2.Z >= z) || (v1.Z >= z && v2.Z <= z))
            {
                if (Math.Abs(v2.Z - v1.Z) < 0.0001) continue;
                
                double t = (z - v1.Z) / (v2.Z - v1.Z);
                double x = v1.X + t * (v2.X - v1.X);
                double y = v1.Y + t * (v2.Y - v1.Y);
                
                points.Add(new Point2D(x, y));
            }
        }
        
        return points;
    }
    
    private List<PolyPath> ConnectSegments(List<(Point2D Start, Point2D End)> segments)
    {
        var paths = new List<PolyPath>();
        var remaining = new List<(Point2D Start, Point2D End)>(segments);
        
        while (remaining.Count > 0)
        {
            var path = new PolyPath();
            var current = remaining[0];
            remaining.RemoveAt(0);
            
            path.Segments.Add(new LineSegment { EndPoint = current.Start });
            path.Segments.Add(new LineSegment { EndPoint = current.End });
            
            var endPoint = current.End;
            bool foundNext = true;
            
            while (foundNext)
            {
                foundNext = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    var seg = remaining[i];
                    
                    if (Distance(endPoint, seg.Start) < 0.01)
                    {
                        path.Segments.Add(new LineSegment { EndPoint = seg.End });
                        endPoint = seg.End;
                        remaining.RemoveAt(i);
                        foundNext = true;
                        break;
                    }
                    else if (Distance(endPoint, seg.End) < 0.01)
                    {
                        path.Segments.Add(new LineSegment { EndPoint = seg.Start });
                        endPoint = seg.Start;
                        remaining.RemoveAt(i);
                        foundNext = true;
                        break;
                    }
                }
            }
            
            // Check if closed
            if (path.Segments.Count > 2 && Distance(path.Segments[0].EndPoint, endPoint) < 0.01)
            {
                path.IsClosed = true;
            }
            
            paths.Add(path);
        }
        
        return paths;
    }
    
    private double Distance(Point2D a, Point2D b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    
    private double ParseDouble(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
}

public class StlTriangle
{
    public StlVertex Normal { get; set; } = new();
    public StlVertex V1 { get; set; } = new();
    public StlVertex V2 { get; set; } = new();
    public StlVertex V3 { get; set; } = new();
}

public class StlVertex
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
