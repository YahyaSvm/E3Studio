using System.Windows.Media;
using System.Windows.Media.Media3D;
using E3Studio.Services;

namespace E3Studio.Models;

public class StlModel3D
{
    public string Name { get; set; } = "STL Model";
    public string FilePath { get; set; } = "";
    
    public Point3D Position { get; set; } = new Point3D(0, 0, 0);
    public Vector3D Rotation { get; set; } = new Vector3D(0, 0, 0);
    public Vector3D Scale { get; set; } = new Vector3D(1, 1, 1);
    
    public Color Color { get; set; } = Colors.Cyan;
    public double Opacity { get; set; } = 0.85;
    
    public bool IsSelected { get; set; }
    public bool IsVisible { get; set; } = true;
    
    public List<StlTriangle> Triangles { get; set; } = new();
    
    public double MinX { get; set; }
    public double MaxX { get; set; }
    public double MinY { get; set; }
    public double MaxY { get; set; }
    public double MinZ { get; set; }
    public double MaxZ { get; set; }
    
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double Depth => MaxZ - MinZ;
    
    public MeshGeometry3D CreateMesh()
    {
        var mesh = new MeshGeometry3D();
        
        foreach (var tri in Triangles)
        {
            var p1 = new Point3D(tri.V1.X, tri.V1.Y, tri.V1.Z);
            var p2 = new Point3D(tri.V2.X, tri.V2.Y, tri.V2.Z);
            var p3 = new Point3D(tri.V3.X, tri.V3.Y, tri.V3.Z);
            
            int idx = mesh.Positions.Count;
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);
            
            mesh.TriangleIndices.Add(idx);
            mesh.TriangleIndices.Add(idx + 1);
            mesh.TriangleIndices.Add(idx + 2);
            
            var normal = new Vector3D(tri.Normal.X, tri.Normal.Y, tri.Normal.Z);
            if (normal.Length < 0.001)
            {
                normal = Vector3D.CrossProduct(p2 - p1, p3 - p1);
                if (normal.Length > 0.001)
                    normal.Normalize();
                else
                    normal = new Vector3D(0, 0, 1);
            }
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
        }
        
        return mesh;
    }
    
    public Transform3D GetTransform3D()
    {
        var group = new Transform3DGroup();
        
        // 1. Center model at origin
        var centerTransform = new TranslateTransform3D(-Width / 2, -Height / 2, -MinZ);
        group.Children.Add(centerTransform);
        
        // 2. Apply rotation
        if (Rotation.X != 0)
        {
            var rotX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), Rotation.X));
            group.Children.Add(rotX);
        }
        if (Rotation.Y != 0)
        {
            var rotY = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), Rotation.Y));
            group.Children.Add(rotY);
        }
        if (Rotation.Z != 0)
        {
            var rotZ = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), Rotation.Z));
            group.Children.Add(rotZ);
        }
        
        // 3. Apply scale
        if (Scale.X != 1 || Scale.Y != 1 || Scale.Z != 1)
        {
            var scaleTransform = new ScaleTransform3D(Scale.X, Scale.Y, Scale.Z);
            group.Children.Add(scaleTransform);
        }
        
        // 4. Apply position (translate to final position)
        var positionTransform = new TranslateTransform3D(Position.X, Position.Y, Position.Z);
        group.Children.Add(positionTransform);
        
        return group;
    }
    
    public void CenterOnStock(double stockWidth, double stockHeight, double stockThickness)
    {
        // Place model centered on stock, sitting on top of stock
        Position = new Point3D(stockWidth / 2, stockHeight / 2, stockThickness);
    }
    
    public void FitToStock(double stockWidth, double stockHeight, double stockThickness)
    {
        if (Width <= 0 || Height <= 0 || Depth <= 0) return;
        
        double scaleX = stockWidth / Width;
        double scaleY = stockHeight / Height;
        double scaleZ = stockThickness / Depth;
        
        // Use uniform scale to maintain proportions
        double uniformScale = Math.Min(Math.Min(scaleX, scaleY), scaleZ);
        Scale = new Vector3D(uniformScale, uniformScale, uniformScale);
        
        CenterOnStock(stockWidth, stockHeight, stockThickness);
    }
    
    public void PlaceOnStockSurface(double stockWidth, double stockHeight, double stockThickness)
    {
        // Place model centered on stock surface (Z = stock top)
        Position = new Point3D(stockWidth / 2, stockHeight / 2, stockThickness);
    }
}
