using System.Windows.Media;
using System.Windows.Media.Media3D;
using E3Studio.Models;
using HelixToolkit.Wpf;

namespace E3Studio.CAM;

/// <summary>
/// Creates 3D visualizations of toolpaths for preview
/// </summary>
public class ToolpathVisualizer3D
{
    // Colors for different move types
    private static readonly Color RapidColor = Color.FromRgb(255, 100, 100);    // Red for rapids
    private static readonly Color FeedColor = Color.FromRgb(0, 212, 170);       // Cyan for feeds
    private static readonly Color PlungeColor = Color.FromRgb(255, 200, 50);    // Yellow for plunges
    private static readonly Color HighlightColor = Color.FromRgb(255, 255, 0);  // Bright yellow for selected
    private static readonly Color DimColor = Color.FromRgb(80, 80, 80);         // Gray for unselected
    
    /// <summary>
    /// Create 3D visual for all toolpaths with optional highlight
    /// </summary>
    public static ModelVisual3D CreateToolpathVisual(List<Toolpath> toolpaths, double stockThickness, Toolpath? selectedToolpath = null, double stockWidth = 200, double stockHeight = 150)
    {
        var visual = new ModelVisual3D();
        var group = new Model3DGroup();
        
        // Stock center offset - toolpath coordinates are relative to (0,0) but stock is at (Width/2, Height/2)
        double offsetX = stockWidth / 2;
        double offsetY = stockHeight / 2;
        
        foreach (var toolpath in toolpaths)
        {
            bool isSelected = selectedToolpath != null && toolpath == selectedToolpath;
            bool isDimmed = selectedToolpath != null && toolpath != selectedToolpath;
            AddToolpathToGroup(group, toolpath, stockThickness, isSelected, isDimmed, offsetX, offsetY);
        }
        
        visual.Content = group;
        return visual;
    }
    
    /// <summary>
    /// Add a single toolpath to the model group
    /// </summary>
    private static void AddToolpathToGroup(Model3DGroup group, Toolpath toolpath, double stockThickness, bool isSelected = false, bool isDimmed = false, double offsetX = 0, double offsetY = 0)
    {
        if (toolpath.Moves == null || toolpath.Moves.Count < 2) return;
        
        Point3D? lastPoint = null;
        
        foreach (var move in toolpath.Moves)
        {
            // Convert 2D point to 3D with stock center offset
            // X, Y are offset to stock center, Z is stockThickness + move.Z (move.Z is negative for cuts)
            var point3D = new Point3D(move.X + offsetX, move.Y + offsetY, stockThickness + move.Z);
            
            if (lastPoint.HasValue)
            {
                // Determine color based on selection state or move type
                Color color;
                double tubeRadius;
                
                if (isSelected)
                {
                    // Highlighted toolpath - bright colors with larger radius
                    if (move.Type == MoveType.Rapid)
                    {
                        color = Color.FromRgb(255, 150, 150);
                        tubeRadius = 0.3;
                    }
                    else if (point3D.Z < lastPoint.Value.Z - 0.1)
                    {
                        color = HighlightColor;
                        tubeRadius = 0.6;
                    }
                    else
                    {
                        color = Color.FromRgb(100, 255, 100); // Bright green for selected feed
                        tubeRadius = 0.8;
                    }
                }
                else if (isDimmed)
                {
                    // Dimmed toolpath - gray, thin
                    color = DimColor;
                    tubeRadius = 0.15;
                }
                else
                {
                    // Normal colors
                    if (move.Type == MoveType.Rapid)
                    {
                        color = RapidColor;
                        tubeRadius = 0.2;
                    }
                    else if (point3D.Z < lastPoint.Value.Z - 0.1)
                    {
                        color = PlungeColor;
                        tubeRadius = 0.4;
                    }
                    else
                    {
                        color = FeedColor;
                        tubeRadius = 0.6;
                    }
                }
                
                // Create tube between points
                var mesh = CreateTubeMesh(lastPoint.Value, point3D, tubeRadius);
                if (mesh != null)
                {
                    var material = new DiffuseMaterial(new SolidColorBrush(color));
                    var model = new GeometryModel3D(mesh, material);
                    model.BackMaterial = material;
                    group.Children.Add(model);
                }
            }
            
            lastPoint = point3D;
        }
    }
    
    /// <summary>
    /// Create a simple tube mesh between two points
    /// </summary>
    private static MeshGeometry3D? CreateTubeMesh(Point3D start, Point3D end, double radius)
    {
        var mesh = new MeshGeometry3D();
        
        // Direction vector
        var dir = end - start;
        if (dir.Length < 0.001) return null;
        dir.Normalize();
        
        // Find perpendicular vectors
        var up = new Vector3D(0, 0, 1);
        if (Math.Abs(Vector3D.DotProduct(dir, up)) > 0.9)
            up = new Vector3D(0, 1, 0);
        
        var right = Vector3D.CrossProduct(dir, up);
        right.Normalize();
        up = Vector3D.CrossProduct(right, dir);
        up.Normalize();
        
        // Create vertices around start and end
        int segments = 6;
        for (int i = 0; i < segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            var offset = right * Math.Cos(angle) * radius + up * Math.Sin(angle) * radius;
            
            mesh.Positions.Add(start + offset);
            mesh.Positions.Add(end + offset);
        }
        
        // Create triangles
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int i0 = i * 2;
            int i1 = i * 2 + 1;
            int n0 = next * 2;
            int n1 = next * 2 + 1;
            
            mesh.TriangleIndices.Add(i0);
            mesh.TriangleIndices.Add(i1);
            mesh.TriangleIndices.Add(n1);
            
            mesh.TriangleIndices.Add(i0);
            mesh.TriangleIndices.Add(n1);
            mesh.TriangleIndices.Add(n0);
        }
        
        return mesh;
    }
    
    /// <summary>
    /// Get color for move type
    /// </summary>
    private static Color GetMoveColor(MoveType type)
    {
        return type switch
        {
            MoveType.Rapid => RapidColor,
            MoveType.Linear => FeedColor,
            MoveType.ArcCW => FeedColor,
            MoveType.ArcCCW => FeedColor,
            _ => FeedColor
        };
    }
    
    /// <summary>
    /// Create a tool visual (endmill, drill bit, etc.) at specified position
    /// </summary>
    public static ModelVisual3D CreateToolVisual(Tool tool, Point3D position, double stockThickness)
    {
        var visual = new ModelVisual3D();
        var group = new Model3DGroup();
        
        double diameter = tool.Diameter;
        double radius = diameter / 2.0;
        double fluteLength = tool.FluteLength;
        double shankLength = tool.TotalLength - fluteLength;
        
        // Material colors
        var cuttingMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(180, 180, 190)));  // Steel color
        var shankMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(120, 120, 130)));    // Darker shank
        var tipMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(220, 200, 100)));      // Gold tip
        
        // Adjust position to be at stock surface level
        double toolBottomZ = position.Z;
        
        switch (tool.Type)
        {
            case ToolType.Drill:
                // Drill bit with pointed tip
                // 1. Conical tip
                double tipLength = diameter * 0.5;  // 118° point angle typical
                var tipMesh = CreateConeMesh(
                    new Point3D(position.X, position.Y, toolBottomZ),
                    new Point3D(position.X, position.Y, toolBottomZ + tipLength),
                    0, radius, 12);
                if (tipMesh != null)
                    group.Children.Add(new GeometryModel3D(tipMesh, tipMaterial) { BackMaterial = tipMaterial });
                
                // 2. Fluted body (cylinder with grooves - simplified as cylinder)
                var fluteMesh = CreateCylinderMesh(
                    new Point3D(position.X, position.Y, toolBottomZ + tipLength),
                    new Point3D(position.X, position.Y, toolBottomZ + tipLength + fluteLength),
                    radius, 12);
                if (fluteMesh != null)
                    group.Children.Add(new GeometryModel3D(fluteMesh, cuttingMaterial) { BackMaterial = cuttingMaterial });
                
                // 3. Shank
                double shankRadius = radius * 1.2;
                var shankMesh = CreateCylinderMesh(
                    new Point3D(position.X, position.Y, toolBottomZ + tipLength + fluteLength),
                    new Point3D(position.X, position.Y, toolBottomZ + tipLength + fluteLength + shankLength),
                    shankRadius, 12);
                if (shankMesh != null)
                    group.Children.Add(new GeometryModel3D(shankMesh, shankMaterial) { BackMaterial = shankMaterial });
                break;
                
            case ToolType.Endmill:
            case ToolType.BallNose:
                // Flat/ball end mill
                double flatBottom = toolBottomZ;
                
                if (tool.Type == ToolType.BallNose)
                {
                    // Ball nose tip
                    var ballMesh = CreateSphereMesh(new Point3D(position.X, position.Y, toolBottomZ), radius, 12);
                    if (ballMesh != null)
                        group.Children.Add(new GeometryModel3D(ballMesh, tipMaterial) { BackMaterial = tipMaterial });
                    flatBottom += radius;
                }
                
                // Fluted body
                var endmillFlute = CreateCylinderMesh(
                    new Point3D(position.X, position.Y, flatBottom),
                    new Point3D(position.X, position.Y, flatBottom + fluteLength),
                    radius, 12);
                if (endmillFlute != null)
                    group.Children.Add(new GeometryModel3D(endmillFlute, cuttingMaterial) { BackMaterial = cuttingMaterial });
                
                // Shank
                var endmillShank = CreateCylinderMesh(
                    new Point3D(position.X, position.Y, flatBottom + fluteLength),
                    new Point3D(position.X, position.Y, flatBottom + fluteLength + shankLength),
                    radius * 1.2, 12);
                if (endmillShank != null)
                    group.Children.Add(new GeometryModel3D(endmillShank, shankMaterial) { BackMaterial = shankMaterial });
                break;
                
            case ToolType.VBit:
                // V-bit with angle
                double vAngle = tool.VAngle > 0 ? tool.VAngle : 60;
                double vTipLength = radius / Math.Tan(vAngle * Math.PI / 360);
                
                var vMesh = CreateConeMesh(
                    new Point3D(position.X, position.Y, toolBottomZ),
                    new Point3D(position.X, position.Y, toolBottomZ + vTipLength),
                    0, radius, 12);
                if (vMesh != null)
                    group.Children.Add(new GeometryModel3D(vMesh, tipMaterial) { BackMaterial = tipMaterial });
                
                var vShank = CreateCylinderMesh(
                    new Point3D(position.X, position.Y, toolBottomZ + vTipLength),
                    new Point3D(position.X, position.Y, toolBottomZ + vTipLength + tool.TotalLength - vTipLength),
                    radius, 12);
                if (vShank != null)
                    group.Children.Add(new GeometryModel3D(vShank, shankMaterial) { BackMaterial = shankMaterial });
                break;
                
            default:
                // Generic cylinder tool
                var genericMesh = CreateCylinderMesh(
                    new Point3D(position.X, position.Y, toolBottomZ),
                    new Point3D(position.X, position.Y, toolBottomZ + tool.TotalLength),
                    radius, 12);
                if (genericMesh != null)
                    group.Children.Add(new GeometryModel3D(genericMesh, cuttingMaterial) { BackMaterial = cuttingMaterial });
                break;
        }
        
        visual.Content = group;
        return visual;
    }
    
    /// <summary>
    /// Create a cylinder mesh
    /// </summary>
    private static MeshGeometry3D? CreateCylinderMesh(Point3D bottom, Point3D top, double radius, int segments)
    {
        var mesh = new MeshGeometry3D();
        var dir = top - bottom;
        if (dir.Length < 0.001) return null;
        
        // Find perpendicular vectors
        var up = new Vector3D(0, 0, 1);
        if (Math.Abs(Vector3D.DotProduct(dir, up) / dir.Length) > 0.9)
            up = new Vector3D(0, 1, 0);
        
        var right = Vector3D.CrossProduct(dir, up);
        right.Normalize();
        up = Vector3D.CrossProduct(right, dir);
        up.Normalize();
        
        // Create vertices
        for (int i = 0; i <= segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            var offset = right * Math.Cos(angle) * radius + up * Math.Sin(angle) * radius;
            mesh.Positions.Add(bottom + offset);
            mesh.Positions.Add(top + offset);
        }
        
        // Side triangles
        for (int i = 0; i < segments; i++)
        {
            int i0 = i * 2;
            int i1 = i * 2 + 1;
            int n0 = (i + 1) * 2;
            int n1 = (i + 1) * 2 + 1;
            
            mesh.TriangleIndices.Add(i0);
            mesh.TriangleIndices.Add(i1);
            mesh.TriangleIndices.Add(n1);
            
            mesh.TriangleIndices.Add(i0);
            mesh.TriangleIndices.Add(n1);
            mesh.TriangleIndices.Add(n0);
        }
        
        // Add caps
        int bottomCenter = mesh.Positions.Count;
        mesh.Positions.Add(bottom);
        int topCenter = mesh.Positions.Count;
        mesh.Positions.Add(top);
        
        for (int i = 0; i < segments; i++)
        {
            // Bottom cap
            mesh.TriangleIndices.Add(bottomCenter);
            mesh.TriangleIndices.Add((i + 1) * 2);
            mesh.TriangleIndices.Add(i * 2);
            
            // Top cap
            mesh.TriangleIndices.Add(topCenter);
            mesh.TriangleIndices.Add(i * 2 + 1);
            mesh.TriangleIndices.Add((i + 1) * 2 + 1);
        }
        
        return mesh;
    }
    
    /// <summary>
    /// Create a cone mesh
    /// </summary>
    private static MeshGeometry3D? CreateConeMesh(Point3D tip, Point3D baseCenter, double tipRadius, double baseRadius, int segments)
    {
        var mesh = new MeshGeometry3D();
        var dir = baseCenter - tip;
        if (dir.Length < 0.001) return null;
        
        var up = new Vector3D(0, 0, 1);
        if (Math.Abs(Vector3D.DotProduct(dir, up) / dir.Length) > 0.9)
            up = new Vector3D(0, 1, 0);
        
        var right = Vector3D.CrossProduct(dir, up);
        right.Normalize();
        up = Vector3D.CrossProduct(right, dir);
        up.Normalize();
        
        // Tip point
        mesh.Positions.Add(tip);
        
        // Base circle
        for (int i = 0; i <= segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            var offset = right * Math.Cos(angle) * baseRadius + up * Math.Sin(angle) * baseRadius;
            mesh.Positions.Add(baseCenter + offset);
        }
        
        // Side triangles
        for (int i = 1; i <= segments; i++)
        {
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(i);
            mesh.TriangleIndices.Add(i + 1);
        }
        
        // Base cap
        int baseCapCenter = mesh.Positions.Count;
        mesh.Positions.Add(baseCenter);
        for (int i = 1; i <= segments; i++)
        {
            mesh.TriangleIndices.Add(baseCapCenter);
            mesh.TriangleIndices.Add(i + 1);
            mesh.TriangleIndices.Add(i);
        }
        
        return mesh;
    }
    
    /// <summary>
    /// Create a sphere mesh (for ball nose)
    /// </summary>
    private static MeshGeometry3D? CreateSphereMesh(Point3D center, double radius, int segments)
    {
        var mesh = new MeshGeometry3D();
        int stacks = segments / 2;
        
        for (int stack = 0; stack <= stacks; stack++)
        {
            double phi = Math.PI * stack / stacks;
            double y = radius * Math.Cos(phi);
            double r = radius * Math.Sin(phi);
            
            for (int slice = 0; slice <= segments; slice++)
            {
                double theta = 2 * Math.PI * slice / segments;
                double x = r * Math.Cos(theta);
                double z = r * Math.Sin(theta);
                
                mesh.Positions.Add(new Point3D(center.X + x, center.Y + z, center.Z + y));
            }
        }
        
        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < segments; slice++)
            {
                int i0 = stack * (segments + 1) + slice;
                int i1 = i0 + 1;
                int i2 = i0 + segments + 1;
                int i3 = i2 + 1;
                
                mesh.TriangleIndices.Add(i0);
                mesh.TriangleIndices.Add(i2);
                mesh.TriangleIndices.Add(i1);
                
                mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i2);
                mesh.TriangleIndices.Add(i3);
            }
        }
        
        return mesh;
    }
    
    /// <summary>
    /// Create axis visual
    /// </summary>
    public static ModelVisual3D CreateAxisVisual(double length = 50)
    {
        var visual = new ModelVisual3D();
        var group = new Model3DGroup();
        
        // X axis (Red)
        var xMesh = CreateTubeMesh(new Point3D(0, 0, 0), new Point3D(length, 0, 0), 0.5);
        if (xMesh != null)
            group.Children.Add(new GeometryModel3D(xMesh, new DiffuseMaterial(new SolidColorBrush(Colors.Red))));
        
        // Y axis (Green)
        var yMesh = CreateTubeMesh(new Point3D(0, 0, 0), new Point3D(0, length, 0), 0.5);
        if (yMesh != null)
            group.Children.Add(new GeometryModel3D(yMesh, new DiffuseMaterial(new SolidColorBrush(Colors.Green))));
        
        // Z axis (Blue)
        var zMesh = CreateTubeMesh(new Point3D(0, 0, 0), new Point3D(0, 0, length), 0.5);
        if (zMesh != null)
            group.Children.Add(new GeometryModel3D(zMesh, new DiffuseMaterial(new SolidColorBrush(Colors.Blue))));
        
        visual.Content = group;
        return visual;
    }
}
