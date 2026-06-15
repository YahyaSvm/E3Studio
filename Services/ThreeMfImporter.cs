using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

namespace E3Studio.Services;

/// <summary>
/// 3MF (3D Manufacturing Format) Importer
/// 3MF is an XML-based format specifically designed for additive manufacturing
/// </summary>
public class ThreeMfImporter
{
    public class ThreeMfResult
    {
        public List<ThreeMfObject> Objects { get; set; } = new();
        public ThreeMfBuildInfo? BuildInfo { get; set; }
        public Dictionary<string, ThreeMfMaterial> Materials { get; set; } = new();
        public Rect3D Bounds { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage);
    }
    
    public class ThreeMfObject
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; } // model, support, solidsupport
        public List<Triangle3D> Triangles { get; set; } = new();
        public List<Point3D> Vertices { get; set; } = new();
        public string? MaterialId { get; set; }
        public Matrix3D Transform { get; set; } = Matrix3D.Identity;
    }
    
    public class Triangle3D
    {
        public int V1 { get; set; }
        public int V2 { get; set; }
        public int V3 { get; set; }
        public int? PropertyId { get; set; }
    }
    
    public class ThreeMfBuildInfo
    {
        public List<BuildItem> Items { get; set; } = new();
        public string? Unit { get; set; } // millimeter, inch, etc.
    }
    
    public class BuildItem
    {
        public int ObjectId { get; set; }
        public Matrix3D Transform { get; set; } = Matrix3D.Identity;
        public string? PartNumber { get; set; }
    }
    
    public class ThreeMfMaterial
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string? DisplayColor { get; set; }
    }
    
    private static readonly XNamespace Ns3mf = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
    private static readonly XNamespace NsMaterial = "http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
    
    /// <summary>
    /// Import 3MF file
    /// </summary>
    public ThreeMfResult Import(string filePath)
    {
        var result = new ThreeMfResult();
        
        try
        {
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File not found";
                return result;
            }
            
            // 3MF is a ZIP archive
            using var archive = ZipFile.OpenRead(filePath);
            
            // Find and parse main model file
            var modelEntry = archive.GetEntry("3D/3dmodel.model") 
                ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".model"));
            
            if (modelEntry == null)
            {
                result.ErrorMessage = "No model file found in 3MF archive";
                return result;
            }
            
            using var stream = modelEntry.Open();
            var doc = XDocument.Load(stream);
            
            ParseModelDocument(doc, result);
            
            // Calculate bounds
            CalculateBounds(result);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Import error: {ex.Message}";
        }
        
        return result;
    }
    
    private void ParseModelDocument(XDocument doc, ThreeMfResult result)
    {
        var root = doc.Root;
        if (root == null) return;
        
        // Get unit (default is millimeter)
        var unit = root.Attribute("unit")?.Value ?? "millimeter";
        result.BuildInfo = new ThreeMfBuildInfo { Unit = unit };
        
        // Parse resources (objects and materials)
        var resources = root.Element(Ns3mf + "resources");
        if (resources != null)
        {
            // Parse materials first
            foreach (var baseMaterials in resources.Elements(Ns3mf + "basematerials"))
            {
                ParseBaseMaterials(baseMaterials, result);
            }
            
            // Parse color groups
            foreach (var colorGroup in resources.Elements(NsMaterial + "colorgroup"))
            {
                ParseColorGroup(colorGroup, result);
            }
            
            // Parse objects
            foreach (var objectElement in resources.Elements(Ns3mf + "object"))
            {
                var obj = ParseObject(objectElement);
                if (obj != null)
                {
                    result.Objects.Add(obj);
                }
            }
        }
        
        // Parse build section
        var build = root.Element(Ns3mf + "build");
        if (build != null)
        {
            foreach (var item in build.Elements(Ns3mf + "item"))
            {
                var buildItem = ParseBuildItem(item);
                if (buildItem != null)
                {
                    result.BuildInfo.Items.Add(buildItem);
                }
            }
        }
    }
    
    private void ParseBaseMaterials(XElement element, ThreeMfResult result)
    {
        var id = element.Attribute("id")?.Value;
        
        foreach (var baseMat in element.Elements(Ns3mf + "base"))
        {
            var name = baseMat.Attribute("name")?.Value;
            var displayColor = baseMat.Attribute("displaycolor")?.Value;
            
            var material = new ThreeMfMaterial
            {
                Id = id,
                Name = name,
                DisplayColor = displayColor
            };
            
            if (!string.IsNullOrEmpty(name))
            {
                result.Materials[name] = material;
            }
        }
    }
    
    private void ParseColorGroup(XElement element, ThreeMfResult result)
    {
        var id = element.Attribute("id")?.Value;
        
        foreach (var color in element.Elements(NsMaterial + "color"))
        {
            var colorValue = color.Attribute("color")?.Value;
            
            var material = new ThreeMfMaterial
            {
                Id = id,
                Color = colorValue
            };
            
            result.Materials[$"color_{id}"] = material;
        }
    }
    
    private ThreeMfObject? ParseObject(XElement element)
    {
        var obj = new ThreeMfObject
        {
            Id = int.TryParse(element.Attribute("id")?.Value, out int id) ? id : 0,
            Name = element.Attribute("name")?.Value,
            Type = element.Attribute("type")?.Value ?? "model",
            MaterialId = element.Attribute("pid")?.Value
        };
        
        // Parse mesh
        var mesh = element.Element(Ns3mf + "mesh");
        if (mesh != null)
        {
            // Parse vertices
            var vertices = mesh.Element(Ns3mf + "vertices");
            if (vertices != null)
            {
                foreach (var vertex in vertices.Elements(Ns3mf + "vertex"))
                {
                    var x = ParseDouble(vertex.Attribute("x")?.Value);
                    var y = ParseDouble(vertex.Attribute("y")?.Value);
                    var z = ParseDouble(vertex.Attribute("z")?.Value);
                    
                    obj.Vertices.Add(new Point3D(x, y, z));
                }
            }
            
            // Parse triangles
            var triangles = mesh.Element(Ns3mf + "triangles");
            if (triangles != null)
            {
                foreach (var triangle in triangles.Elements(Ns3mf + "triangle"))
                {
                    var tri = new Triangle3D
                    {
                        V1 = int.TryParse(triangle.Attribute("v1")?.Value, out int v1) ? v1 : 0,
                        V2 = int.TryParse(triangle.Attribute("v2")?.Value, out int v2) ? v2 : 0,
                        V3 = int.TryParse(triangle.Attribute("v3")?.Value, out int v3) ? v3 : 0,
                        PropertyId = int.TryParse(triangle.Attribute("pid")?.Value, out int pid) ? pid : null
                    };
                    
                    obj.Triangles.Add(tri);
                }
            }
        }
        
        // Parse components (for assemblies)
        var components = element.Element(Ns3mf + "components");
        if (components != null)
        {
            // Components reference other objects - handled at build level
        }
        
        return obj.Vertices.Count > 0 ? obj : null;
    }
    
    private BuildItem? ParseBuildItem(XElement element)
    {
        var item = new BuildItem
        {
            ObjectId = int.TryParse(element.Attribute("objectid")?.Value, out int objId) ? objId : 0,
            PartNumber = element.Attribute("partnumber")?.Value
        };
        
        // Parse transform matrix
        var transformStr = element.Attribute("transform")?.Value;
        if (!string.IsNullOrEmpty(transformStr))
        {
            item.Transform = ParseTransformMatrix(transformStr);
        }
        
        return item;
    }
    
    private Matrix3D ParseTransformMatrix(string transformStr)
    {
        var values = transformStr.Split(' ')
            .Select(s => ParseDouble(s))
            .ToArray();
        
        if (values.Length >= 12)
        {
            // 3MF uses a 3x4 affine transformation matrix (row-major)
            // [m00 m01 m02 m03]
            // [m10 m11 m12 m13]
            // [m20 m21 m22 m23]
            return new Matrix3D(
                values[0], values[1], values[2], 0,
                values[3], values[4], values[5], 0,
                values[6], values[7], values[8], 0,
                values[9], values[10], values[11], 1);
        }
        
        return Matrix3D.Identity;
    }
    
    private double ParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result);
        return result;
    }
    
    private void CalculateBounds(ThreeMfResult result)
    {
        if (result.Objects.Count == 0)
        {
            result.Bounds = new Rect3D(0, 0, 0, 0, 0, 0);
            return;
        }
        
        var allVertices = result.Objects.SelectMany(o => o.Vertices).ToList();
        if (allVertices.Count == 0)
        {
            result.Bounds = new Rect3D(0, 0, 0, 0, 0, 0);
            return;
        }
        
        var minX = allVertices.Min(v => v.X);
        var minY = allVertices.Min(v => v.Y);
        var minZ = allVertices.Min(v => v.Z);
        var maxX = allVertices.Max(v => v.X);
        var maxY = allVertices.Max(v => v.Y);
        var maxZ = allVertices.Max(v => v.Z);
        
        result.Bounds = new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }
    
    /// <summary>
    /// Generate 2D slices from 3MF model at specified Z heights
    /// Useful for CAM waterline machining or 3D printing preview
    /// </summary>
    public List<SliceResult> GenerateSlices(ThreeMfResult model, double layerHeight, 
        double startZ = 0, double? endZ = null)
    {
        var slices = new List<SliceResult>();
        
        var actualEndZ = endZ ?? model.Bounds.Z + model.Bounds.SizeZ;
        
        for (double z = startZ; z <= actualEndZ; z += layerHeight)
        {
            var slice = SliceAtZ(model, z);
            if (slice.Contours.Count > 0)
            {
                slices.Add(slice);
            }
        }
        
        return slices;
    }
    
    public class SliceResult
    {
        public double Z { get; set; }
        public List<List<Point>> Contours { get; set; } = new();
    }
    
    /// <summary>
    /// Slice model at specific Z height
    /// </summary>
    public SliceResult SliceAtZ(ThreeMfResult model, double z)
    {
        var result = new SliceResult { Z = z };
        var segments = new List<(Point P1, Point P2)>();
        
        foreach (var obj in model.Objects)
        {
            foreach (var triangle in obj.Triangles)
            {
                if (triangle.V1 >= obj.Vertices.Count ||
                    triangle.V2 >= obj.Vertices.Count ||
                    triangle.V3 >= obj.Vertices.Count)
                    continue;
                
                var v1 = obj.Vertices[triangle.V1];
                var v2 = obj.Vertices[triangle.V2];
                var v3 = obj.Vertices[triangle.V3];
                
                // Find intersection with Z plane
                var intersectionPoints = new List<Point>();
                
                // Check each edge
                TryAddIntersection(v1, v2, z, intersectionPoints);
                TryAddIntersection(v2, v3, z, intersectionPoints);
                TryAddIntersection(v3, v1, z, intersectionPoints);
                
                // Valid slice should have exactly 2 intersection points
                if (intersectionPoints.Count == 2)
                {
                    segments.Add((intersectionPoints[0], intersectionPoints[1]));
                }
            }
        }
        
        // Connect segments into contours
        result.Contours = ConnectSegments(segments);
        
        return result;
    }
    
    private void TryAddIntersection(Point3D p1, Point3D p2, double z, List<Point> points)
    {
        if ((p1.Z <= z && p2.Z >= z) || (p1.Z >= z && p2.Z <= z))
        {
            if (Math.Abs(p2.Z - p1.Z) < 0.0001)
            {
                // Edge is on the plane
                if (Math.Abs(p1.Z - z) < 0.0001)
                {
                    points.Add(new Point(p1.X, p1.Y));
                    points.Add(new Point(p2.X, p2.Y));
                }
            }
            else
            {
                var t = (z - p1.Z) / (p2.Z - p1.Z);
                var x = p1.X + t * (p2.X - p1.X);
                var y = p1.Y + t * (p2.Y - p1.Y);
                points.Add(new Point(x, y));
            }
        }
    }
    
    private List<List<Point>> ConnectSegments(List<(Point P1, Point P2)> segments)
    {
        var contours = new List<List<Point>>();
        var used = new bool[segments.Count];
        const double tolerance = 0.001;
        
        while (true)
        {
            // Find first unused segment
            var startIdx = -1;
            for (int i = 0; i < segments.Count; i++)
            {
                if (!used[i])
                {
                    startIdx = i;
                    break;
                }
            }
            
            if (startIdx < 0) break;
            
            var contour = new List<Point> { segments[startIdx].P1, segments[startIdx].P2 };
            used[startIdx] = true;
            
            bool changed = true;
            while (changed)
            {
                changed = false;
                var lastPoint = contour[contour.Count - 1];
                
                for (int i = 0; i < segments.Count; i++)
                {
                    if (used[i]) continue;
                    
                    var seg = segments[i];
                    
                    if (Distance(lastPoint, seg.P1) < tolerance)
                    {
                        contour.Add(seg.P2);
                        used[i] = true;
                        changed = true;
                        break;
                    }
                    else if (Distance(lastPoint, seg.P2) < tolerance)
                    {
                        contour.Add(seg.P1);
                        used[i] = true;
                        changed = true;
                        break;
                    }
                }
            }
            
            if (contour.Count >= 3)
            {
                contours.Add(contour);
            }
        }
        
        return contours;
    }
    
    private double Distance(Point p1, Point p2)
    {
        return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
    }
    
    /// <summary>
    /// Convert to mesh for HelixToolkit visualization
    /// </summary>
    public MeshGeometry3D ToMeshGeometry3D(ThreeMfObject obj)
    {
        var mesh = new MeshGeometry3D();
        
        foreach (var vertex in obj.Vertices)
        {
            mesh.Positions.Add(vertex);
        }
        
        foreach (var triangle in obj.Triangles)
        {
            mesh.TriangleIndices.Add(triangle.V1);
            mesh.TriangleIndices.Add(triangle.V2);
            mesh.TriangleIndices.Add(triangle.V3);
        }
        
        return mesh;
    }
}
