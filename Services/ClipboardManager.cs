using System.Text.Json;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Manages clipboard operations for geometry paths
/// </summary>
public class ClipboardManager
{
    private static ClipboardManager? _instance;
    public static ClipboardManager Instance => _instance ??= new ClipboardManager();
    
    private List<ClipboardItem> _clipboardData = new();
    private Point2D _copyOrigin = new(0, 0);
    
    /// <summary>
    /// Copy paths to clipboard
    /// </summary>
    public void Copy(IEnumerable<GeometryPath> paths)
    {
        _clipboardData.Clear();
        
        // Calculate center of selection for paste offset
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        
        foreach (var path in paths)
        {
            if (path is PolyPath poly)
            {
                foreach (var seg in poly.Segments)
                {
                    var pt = TransformPoint(seg.EndPoint, poly);
                    minX = Math.Min(minX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxX = Math.Max(maxX, pt.X);
                    maxY = Math.Max(maxY, pt.Y);
                }
            }
            
            // Clone the path data
            _clipboardData.Add(ClipboardItem.FromPath(path));
        }
        
        if (minX != double.MaxValue)
        {
            _copyOrigin = new Point2D((minX + maxX) / 2, (minY + maxY) / 2);
        }
    }
    
    /// <summary>
    /// Cut paths (copy + mark for deletion)
    /// </summary>
    public void Cut(IEnumerable<GeometryPath> paths)
    {
        Copy(paths);
    }
    
    /// <summary>
    /// Paste paths from clipboard
    /// </summary>
    public List<GeometryPath> Paste(double offsetX = 10, double offsetY = -10)
    {
        var pasted = new List<GeometryPath>();
        
        foreach (var item in _clipboardData)
        {
            var path = item.ToPath();
            if (path != null)
            {
                // Apply offset
                path.X += offsetX;
                path.Y += offsetY;
                
                // Generate new ID
                path.Id = Guid.NewGuid().ToString();
                path.Name = path.Name + " (Copy)";
                
                pasted.Add(path);
            }
        }
        
        return pasted;
    }
    
    /// <summary>
    /// Check if clipboard has data
    /// </summary>
    public bool HasData => _clipboardData.Count > 0;
    
    /// <summary>
    /// Get clipboard item count
    /// </summary>
    public int Count => _clipboardData.Count;
    
    /// <summary>
    /// Clear clipboard
    /// </summary>
    public void Clear()
    {
        _clipboardData.Clear();
    }
    
    private Point2D TransformPoint(Point2D pt, PolyPath poly)
    {
        var transform = Matrix3x2.Identity *
                        Matrix3x2.CreateScale(poly.ScaleX, poly.ScaleY) *
                        Matrix3x2.CreateRotation(poly.Rotation * Math.PI / 180.0) *
                        Matrix3x2.CreateTranslation(poly.X, poly.Y);
        return transform.Transform(pt);
    }
}

/// <summary>
/// Serializable clipboard item
/// </summary>
public class ClipboardItem
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public double ScaleX { get; set; } = 1;
    public double ScaleY { get; set; } = 1;
    public bool IsClosed { get; set; }
    public string Color { get; set; } = "#00D4AA";
    public List<SegmentData> Segments { get; set; } = new();
    
    public static ClipboardItem FromPath(GeometryPath path)
    {
        var item = new ClipboardItem
        {
            Type = path.GetType().Name,
            Name = path.Name,
            X = path.X,
            Y = path.Y,
            Rotation = path.Rotation,
            ScaleX = path.ScaleX,
            ScaleY = path.ScaleY,
            Color = path.Color
        };
        
        if (path is PolyPath poly)
        {
            item.IsClosed = poly.IsClosed;
            foreach (var seg in poly.Segments)
            {
                item.Segments.Add(new SegmentData
                {
                    Type = seg.GetType().Name,
                    EndX = seg.EndPoint.X,
                    EndY = seg.EndPoint.Y,
                    // Arc data
                    CenterX = seg is ArcSegment arc ? arc.Center.X : 0,
                    CenterY = seg is ArcSegment ? ((ArcSegment)seg).Center.Y : 0,
                    Clockwise = seg is ArcSegment ? ((ArcSegment)seg).IsClockwise : false
                });
            }
        }
        
        return item;
    }
    
    public GeometryPath? ToPath()
    {
        if (Type == "PolyPath" || Type == "GeometryPath")
        {
            var poly = new PolyPath
            {
                Id = Guid.NewGuid().ToString(),
                Name = Name,
                X = X,
                Y = Y,
                Rotation = Rotation,
                ScaleX = ScaleX,
                ScaleY = ScaleY,
                IsClosed = IsClosed,
                Color = Color
            };
            
            foreach (var segData in Segments)
            {
                PathSegment seg;
                if (segData.Type == "ArcSegment")
                {
                    seg = new ArcSegment
                    {
                        EndPoint = new Point2D(segData.EndX, segData.EndY),
                        Center = new Point2D(segData.CenterX, segData.CenterY),
                        IsClockwise = segData.Clockwise
                    };
                }
                else
                {
                    seg = new LineSegment
                    {
                        EndPoint = new Point2D(segData.EndX, segData.EndY)
                    };
                }
                poly.Segments.Add(seg);
            }
            
            return poly;
        }
        
        return null;
    }
}

public class SegmentData
{
    public string Type { get; set; } = "LineSegment";
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public bool Clockwise { get; set; }
}
