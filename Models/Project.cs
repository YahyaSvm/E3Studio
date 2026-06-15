using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace E3Studio.Models;

// ---------------------------------------------------------
// Core Geometry Definitions (Restored)
// ---------------------------------------------------------

public class Point2D
{
    public double X { get; set; }
    public double Y { get; set; }
    
    public Point2D(double x, double y) { X = x; Y = y; }
}

public abstract class PathSegment 
{
    public Point2D EndPoint { get; set; } = new(0, 0);
}

public class LineSegment : PathSegment {}

public class ArcSegment : PathSegment
{
    public Point2D Center { get; set; } = new(0, 0);
    public bool IsClockwise { get; set; }
}

public abstract class GeometryPath : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _name = "";
    private string _color = "#00D4AA";
    
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }
    
    public string Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }
    
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }
    
    // Transformation Properties
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class PolyPath : GeometryPath
{
    public List<PathSegment> Segments { get; set; } = new();
    public bool IsClosed { get; set; }
    
    // Helper to get raw bounds (before transform)
    public (double minX, double minY, double width, double height) GetBounds()
    {
        if (Segments.Count == 0) return (0,0,0,0);
        
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        
        foreach (var seg in Segments)
        {
            minX = Math.Min(minX, seg.EndPoint.X);
            minY = Math.Min(minY, seg.EndPoint.Y);
            maxX = Math.Max(maxX, seg.EndPoint.X);
            maxY = Math.Max(maxY, seg.EndPoint.Y);
        }
        
        return (minX, minY, maxX - minX, maxY - minY);
    }
}

// Simple DTO for SVG Import compatibility
public class Layer
{
    public string Name { get; set; } = "Layer";
    public string Color { get; set; } = "#FFFFFF";
    public bool IsVisible { get; set; } = true;
    public List<GeometryPath> Paths { get; } = new();
}


// ---------------------------------------------------------
// Project Tree Nodes (New Structure)
// ---------------------------------------------------------

public enum NodeType
{
    Project,
    WCS,
    Folder,
    Layer,
    Geometry,
    ToolpathGroup,
    Toolpath,
    ModelGroup,
    Model
}

/// <summary>
/// Base class for all nodes in the Project Tree
/// </summary>
public abstract class ProjectNode : INotifyPropertyChanged
{
    private string _name = "";
    private bool _isVisible = true;
    private bool _isSelected;
    private bool _isExpanded = true;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public NodeType Type { get; set; }

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ProjectNode> Children { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ---------------------------------------------------------
// Specific Node Implementations
// ---------------------------------------------------------

public class ProjectRootNode : ProjectNode
{
    public ProjectRootNode() { Type = NodeType.Project; }
    public Stock Stock { get; set; } = new();
}

public class WCSNode : ProjectNode
{
    public WCSNode() { Type = NodeType.WCS; Name = "WCS1"; }
}

public class FolderNode : ProjectNode
{
    public FolderNode(string name) { Type = NodeType.Folder; Name = name; }
}

public class LayerNode : ProjectNode
{
    public LayerNode() { Type = NodeType.Layer; }
    public string Color { get; set; } = "#FFFFFF";
    public List<GeometryPath> Paths { get; } = new();
}

public class ToolpathNode : ProjectNode
{
    public ToolpathNode() { Type = NodeType.Toolpath; }
    public Toolpath Data { get; set; } = new();
}

public class GeometryNode : ProjectNode
{
    public GeometryNode() { Type = NodeType.Geometry; }
    public GeometryPath PathData { get; set; } = new PolyPath();
}

public class GroupNode : ProjectNode
{
    public GroupNode() { Type = NodeType.ModelGroup; } // Using ModelGroup type loosely or add new type
    // Children contain GeometryNodes
}

/// <summary>
/// Main Project Class (acting as ViewModel for the tree)
/// </summary>
public class Project : INotifyPropertyChanged
{
    private ProjectRootNode _root;

    public Project()
    {
        _root = new ProjectRootNode { Name = "Untitled Project" };
        
        // Default Structure
        var wcs = new WCSNode();
        _root.Children.Add(wcs);

        var models = new FolderNode("3D Models");
        wcs.Children.Add(models);

        var layers = new FolderNode("2D Layers");
        wcs.Children.Add(layers);
        
        // Add a default layer
        layers.Children.Add(new LayerNode { Name = "Layer 1", Color = "#00D4AA" });

        var toolpaths = new FolderNode("Toolpaths");
        wcs.Children.Add(toolpaths);
    }

    public ProjectRootNode Root
    {
        get => _root;
        set { _root = value; OnPropertyChanged(); }
    }
    
    // Helper to access layers easily
    public ObservableCollection<ProjectNode> Layers => 
        ((_root.Children[0] as WCSNode)?.Children[1] as FolderNode)?.Children 
        ?? new ObservableCollection<ProjectNode>();

    public Stock Stock => _root.Stock;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
