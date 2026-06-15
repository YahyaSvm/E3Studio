using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Serializes and deserializes E3Studio projects to/from JSON format
/// </summary>
public class ProjectSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new GeometryPathConverter() }
    };
    
    /// <summary>
    /// Save project to .e3p file
    /// </summary>
    public static void Save(Project project, string filePath)
    {
        var dto = ConvertToDto(project);
        var json = JsonSerializer.Serialize(dto, _options);
        File.WriteAllText(filePath, json);
    }
    
    /// <summary>
    /// Load project from .e3p file
    /// </summary>
    public static Project Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<ProjectDto>(json, _options);
        
        if (dto == null)
            throw new Exception("Failed to deserialize project file");
            
        return ConvertFromDto(dto);
    }
    
    #region DTO Conversion
    
    private static ProjectDto ConvertToDto(Project project)
    {
        return new ProjectDto
        {
            Version = "1.0",
            Name = project.Root.Name,
            Stock = new StockDto
            {
                Width = project.Stock.Width,
                Height = project.Stock.Height,
                Thickness = project.Stock.Thickness,
                ZeroPoint = project.Stock.ZeroPoint.ToString(),
                Material = project.Stock.Material != null ? new MaterialDto
                {
                    Name = project.Stock.Material.Name,
                    Category = project.Stock.Material.Category,
                    FeedRate = project.Stock.Material.FeedRate,
                    PlungeRate = project.Stock.Material.PlungeRate,
                    SpindleRPM = project.Stock.Material.SpindleRPM,
                    DepthPerPass = project.Stock.Material.DepthPerPass
                } : null
            },
            Nodes = ConvertNodesToDto(project.Root.Children)
        };
    }
    
    private static List<NodeDto> ConvertNodesToDto(IEnumerable<ProjectNode> nodes)
    {
        var list = new List<NodeDto>();
        foreach (var node in nodes)
        {
            var dto = new NodeDto
            {
                Type = node.Type.ToString(),
                Name = node.Name,
                IsVisible = node.IsVisible,
                IsExpanded = node.IsExpanded,
                Children = ConvertNodesToDto(node.Children)
            };
            
            if (node is LayerNode layer)
            {
                dto.Color = layer.Color;
            }
            
            if (node is GeometryNode geo && geo.PathData is PolyPath poly)
            {
                dto.Geometry = new GeometryDto
                {
                    X = poly.X,
                    Y = poly.Y,
                    Rotation = poly.Rotation,
                    ScaleX = poly.ScaleX,
                    ScaleY = poly.ScaleY,
                    IsClosed = poly.IsClosed,
                    Segments = poly.Segments.Select(s => new SegmentDto
                    {
                        Type = s is ArcSegment ? "Arc" : "Line",
                        EndX = s.EndPoint.X,
                        EndY = s.EndPoint.Y,
                        CenterX = s is ArcSegment arc ? arc.Center.X : 0,
                        CenterY = s is ArcSegment arc2 ? arc2.Center.Y : 0,
                        IsClockwise = s is ArcSegment arc3 && arc3.IsClockwise
                    }).ToList()
                };
            }
            
            if (node is ToolpathNode tp)
            {
                dto.Toolpath = new ToolpathDto
                {
                    Name = tp.Data.Name,
                    Type = tp.Data.Type.ToString(),
                    CutDepth = tp.Data.CutDepth,
                    FinalDepth = tp.Data.FinalDepth,
                    FeedRate = tp.Data.FeedRate,
                    PlungeRate = tp.Data.PlungeRate,
                    SpindleRPM = tp.Data.SpindleRPM,
                    Side = tp.Data.Side.ToString(),
                    StepOver = tp.Data.StepOver
                };
            }
            
            list.Add(dto);
        }
        return list;
    }
    
    private static Project ConvertFromDto(ProjectDto dto)
    {
        var project = new Project();
        project.Root.Name = dto.Name ?? "Untitled";
        project.Root.Children.Clear();
        
        // Load stock
        if (dto.Stock != null)
        {
            project.Stock.Width = dto.Stock.Width;
            project.Stock.Height = dto.Stock.Height;
            project.Stock.Thickness = dto.Stock.Thickness;
            
            if (Enum.TryParse<StockOrigin>(dto.Stock.ZeroPoint, out var origin))
                project.Stock.ZeroPoint = origin;
                
            if (dto.Stock.Material != null)
            {
                project.Stock.Material = new Material
                {
                    Name = dto.Stock.Material.Name ?? "",
                    Category = dto.Stock.Material.Category ?? "",
                    FeedRate = dto.Stock.Material.FeedRate,
                    PlungeRate = dto.Stock.Material.PlungeRate,
                    SpindleRPM = dto.Stock.Material.SpindleRPM,
                    DepthPerPass = dto.Stock.Material.DepthPerPass
                };
            }
        }
        
        // Rebuild tree structure
        var wcs = new WCSNode();
        project.Root.Children.Add(wcs);
        
        var models = new FolderNode("3D Models");
        wcs.Children.Add(models);
        
        var layers = new FolderNode("2D Layers");
        wcs.Children.Add(layers);
        
        var toolpaths = new FolderNode("Toolpaths");
        wcs.Children.Add(toolpaths);
        
        // Load nodes from DTO
        if (dto.Nodes != null)
        {
            foreach (var nodeDto in dto.Nodes)
            {
                LoadNodeFromDto(nodeDto, wcs, layers, toolpaths);
            }
        }
        
        return project;
    }
    
    private static void LoadNodeFromDto(NodeDto dto, WCSNode wcs, FolderNode layers, FolderNode toolpaths)
    {
        if (dto.Type == "Layer")
        {
            var layer = new LayerNode
            {
                Name = dto.Name ?? "Layer",
                Color = dto.Color ?? "#FFFFFF",
                IsVisible = dto.IsVisible,
                IsExpanded = dto.IsExpanded
            };
            
            if (dto.Children != null)
            {
                foreach (var childDto in dto.Children)
                {
                    var child = CreateNodeFromDto(childDto);
                    if (child != null)
                        layer.Children.Add(child);
                }
            }
            
            layers.Children.Add(layer);
        }
        else if (dto.Type == "Toolpath")
        {
            var node = CreateNodeFromDto(dto);
            if (node != null)
                toolpaths.Children.Add(node);
        }
    }
    
    private static ProjectNode? CreateNodeFromDto(NodeDto dto)
    {
        switch (dto.Type)
        {
            case "Geometry":
                var geo = new GeometryNode
                {
                    Name = dto.Name ?? "Path",
                    IsVisible = dto.IsVisible
                };
                
                if (dto.Geometry != null)
                {
                    var poly = new PolyPath
                    {
                        X = dto.Geometry.X,
                        Y = dto.Geometry.Y,
                        Rotation = dto.Geometry.Rotation,
                        ScaleX = dto.Geometry.ScaleX,
                        ScaleY = dto.Geometry.ScaleY,
                        IsClosed = dto.Geometry.IsClosed
                    };
                    
                    if (dto.Geometry.Segments != null)
                    {
                        foreach (var segDto in dto.Geometry.Segments)
                        {
                            PathSegment seg;
                            if (segDto.Type == "Arc")
                            {
                                seg = new ArcSegment
                                {
                                    EndPoint = new Point2D(segDto.EndX, segDto.EndY),
                                    Center = new Point2D(segDto.CenterX, segDto.CenterY),
                                    IsClockwise = segDto.IsClockwise
                                };
                            }
                            else
                            {
                                seg = new LineSegment
                                {
                                    EndPoint = new Point2D(segDto.EndX, segDto.EndY)
                                };
                            }
                            poly.Segments.Add(seg);
                        }
                    }
                    
                    geo.PathData = poly;
                }
                return geo;
                
            case "Toolpath":
                var tp = new ToolpathNode
                {
                    Name = dto.Name ?? "Toolpath"
                };
                
                if (dto.Toolpath != null)
                {
                    tp.Data.Name = dto.Toolpath.Name ?? "Toolpath";
                    if (Enum.TryParse<ToolpathType>(dto.Toolpath.Type, out var tpType))
                        tp.Data.Type = tpType;
                    tp.Data.CutDepth = dto.Toolpath.CutDepth;
                    tp.Data.FinalDepth = dto.Toolpath.FinalDepth;
                    tp.Data.FeedRate = dto.Toolpath.FeedRate;
                    tp.Data.PlungeRate = dto.Toolpath.PlungeRate;
                    tp.Data.SpindleRPM = dto.Toolpath.SpindleRPM;
                    if (Enum.TryParse<ProfileSide>(dto.Toolpath.Side, out var side))
                        tp.Data.Side = side;
                    tp.Data.StepOver = dto.Toolpath.StepOver;
                }
                return tp;
                
            case "Layer":
                var layer = new LayerNode
                {
                    Name = dto.Name ?? "Layer",
                    Color = dto.Color ?? "#FFFFFF",
                    IsVisible = dto.IsVisible,
                    IsExpanded = dto.IsExpanded
                };
                
                if (dto.Children != null)
                {
                    foreach (var childDto in dto.Children)
                    {
                        var child = CreateNodeFromDto(childDto);
                        if (child != null)
                            layer.Children.Add(child);
                    }
                }
                return layer;
                
            case "Folder":
                var folder = new FolderNode(dto.Name ?? "Folder")
                {
                    IsVisible = dto.IsVisible,
                    IsExpanded = dto.IsExpanded
                };
                
                if (dto.Children != null)
                {
                    foreach (var childDto in dto.Children)
                    {
                        var child = CreateNodeFromDto(childDto);
                        if (child != null)
                            folder.Children.Add(child);
                    }
                }
                return folder;
                
            case "ModelGroup":
                var group = new GroupNode
                {
                    Name = dto.Name ?? "Group",
                    IsVisible = dto.IsVisible,
                    IsExpanded = dto.IsExpanded
                };
                
                if (dto.Children != null)
                {
                    foreach (var childDto in dto.Children)
                    {
                        var child = CreateNodeFromDto(childDto);
                        if (child != null)
                            group.Children.Add(child);
                    }
                }
                return group;
        }
        
        return null;
    }
    
    #endregion
}

#region DTO Classes

public class ProjectDto
{
    public string? Version { get; set; }
    public string? Name { get; set; }
    public StockDto? Stock { get; set; }
    public List<NodeDto>? Nodes { get; set; }
}

public class StockDto
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double Thickness { get; set; }
    public string? ZeroPoint { get; set; }
    public MaterialDto? Material { get; set; }
}

public class MaterialDto
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public double FeedRate { get; set; }
    public double PlungeRate { get; set; }
    public double SpindleRPM { get; set; }
    public double DepthPerPass { get; set; }
}

public class NodeDto
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsExpanded { get; set; } = true;
    public string? Color { get; set; }
    public GeometryDto? Geometry { get; set; }
    public ToolpathDto? Toolpath { get; set; }
    public List<NodeDto>? Children { get; set; }
}

public class GeometryDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public double ScaleX { get; set; } = 1;
    public double ScaleY { get; set; } = 1;
    public bool IsClosed { get; set; }
    public List<SegmentDto>? Segments { get; set; }
}

public class SegmentDto
{
    public string? Type { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public bool IsClockwise { get; set; }
}

public class ToolpathDto
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public double CutDepth { get; set; }
    public double FinalDepth { get; set; }
    public double FeedRate { get; set; }
    public double PlungeRate { get; set; }
    public double SpindleRPM { get; set; }
    public string? Side { get; set; }
    public double StepOver { get; set; }
}

#endregion

#region JSON Converters

public class GeometryPathConverter : JsonConverter<GeometryPath>
{
    public override GeometryPath? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new PolyPath(); // Default implementation
    }

    public override void Write(Utf8JsonWriter writer, GeometryPath value, JsonSerializerOptions options)
    {
        // Handled by DTO conversion
        writer.WriteNullValue();
    }
}

#endregion
