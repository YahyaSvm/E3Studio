using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Media3D;

namespace E3Studio.Services;

/// <summary>
/// STEP (ISO 10303) and IGES file importer
/// Supports basic geometry: points, curves, surfaces for CAD/CAM
/// </summary>
public class StepIgesImporter
{
    public class ImportResult
    {
        public List<Curve3D> Curves { get; set; } = new();
        public List<Surface3D> Surfaces { get; set; } = new();
        public List<Point3D> Points { get; set; } = new();
        public Rect3D Bounds { get; set; }
        public string? ErrorMessage { get; set; }
        public FileType DetectedType { get; set; }
        public bool Success => string.IsNullOrEmpty(ErrorMessage);
    }
    
    public enum FileType
    {
        Unknown,
        STEP,
        IGES
    }
    
    public class Curve3D
    {
        public List<Point3D> Points { get; set; } = new();
        public CurveType Type { get; set; }
        public string? Name { get; set; }
        public bool IsClosed { get; set; }
    }
    
    public class Surface3D
    {
        public List<List<Point3D>> ControlPoints { get; set; } = new();
        public SurfaceType Type { get; set; }
        public string? Name { get; set; }
        public List<Curve3D> BoundaryLoops { get; set; } = new();
    }
    
    public enum CurveType { Line, Arc, Circle, Spline, Polyline }
    public enum SurfaceType { Plane, Cylinder, Cone, Sphere, Torus, BSpline }
    
    /// <summary>
    /// Import STEP or IGES file
    /// </summary>
    public ImportResult Import(string filePath)
    {
        var result = new ImportResult();
        
        try
        {
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = "File not found";
                return result;
            }
            
            var content = File.ReadAllText(filePath);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Detect file type
            if (extension == ".step" || extension == ".stp" || content.Contains("ISO-10303-21"))
            {
                result.DetectedType = FileType.STEP;
                ParseStepFile(content, result);
            }
            else if (extension == ".iges" || extension == ".igs" || content.StartsWith("                                                                        S"))
            {
                result.DetectedType = FileType.IGES;
                ParseIgesFile(content, result);
            }
            else
            {
                result.ErrorMessage = "Unknown file format. Expected STEP or IGES.";
                return result;
            }
            
            // Calculate bounds
            CalculateBounds(result);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Import error: {ex.Message}";
        }
        
        return result;
    }
    
    #region STEP Parser
    
    private void ParseStepFile(string content, ImportResult result)
    {
        // Extract DATA section
        var dataMatch = Regex.Match(content, @"DATA;([\s\S]*?)ENDSEC;", RegexOptions.IgnoreCase);
        if (!dataMatch.Success)
        {
            result.ErrorMessage = "Invalid STEP file: DATA section not found";
            return;
        }
        
        var dataSection = dataMatch.Groups[1].Value;
        
        // Parse entities
        var entities = new Dictionary<int, StepEntity>();
        var entityPattern = @"#(\d+)\s*=\s*(\w+)\s*\((.*?)\)\s*;";
        var matches = Regex.Matches(dataSection, entityPattern, RegexOptions.Singleline);
        
        foreach (Match match in matches)
        {
            var id = int.Parse(match.Groups[1].Value);
            var type = match.Groups[2].Value.ToUpper();
            var args = match.Groups[3].Value;
            
            entities[id] = new StepEntity { Id = id, Type = type, Arguments = args };
        }
        
        // Process geometry entities
        foreach (var entity in entities.Values)
        {
            switch (entity.Type)
            {
                case "CARTESIAN_POINT":
                    var point = ParseCartesianPoint(entity.Arguments);
                    if (point.HasValue)
                    {
                        entity.Data = point.Value;
                        result.Points.Add(point.Value);
                    }
                    break;
                    
                case "LINE":
                    var line = ParseStepLine(entity, entities);
                    if (line != null) result.Curves.Add(line);
                    break;
                    
                case "CIRCLE":
                    var circle = ParseStepCircle(entity, entities);
                    if (circle != null) result.Curves.Add(circle);
                    break;
                    
                case "B_SPLINE_CURVE_WITH_KNOTS":
                case "B_SPLINE_CURVE":
                    var spline = ParseStepBSpline(entity, entities);
                    if (spline != null) result.Curves.Add(spline);
                    break;
                    
                case "POLYLINE":
                    var polyline = ParseStepPolyline(entity, entities);
                    if (polyline != null) result.Curves.Add(polyline);
                    break;
                    
                case "PLANE":
                    var plane = ParseStepPlane(entity, entities);
                    if (plane != null) result.Surfaces.Add(plane);
                    break;
                    
                case "CYLINDRICAL_SURFACE":
                    var cylinder = ParseStepCylinder(entity, entities);
                    if (cylinder != null) result.Surfaces.Add(cylinder);
                    break;
            }
        }
    }
    
    private class StepEntity
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public string Arguments { get; set; } = "";
        public object? Data { get; set; }
    }
    
    private Point3D? ParseCartesianPoint(string args)
    {
        // Format: 'name',(x,y,z)
        var match = Regex.Match(args, @"\(\s*([-\d.eE+]+)\s*,\s*([-\d.eE+]+)\s*,\s*([-\d.eE+]+)\s*\)");
        if (match.Success)
        {
            return new Point3D(
                double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
        }
        
        // 2D point
        match = Regex.Match(args, @"\(\s*([-\d.eE+]+)\s*,\s*([-\d.eE+]+)\s*\)");
        if (match.Success)
        {
            return new Point3D(
                double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                0);
        }
        
        return null;
    }
    
    private Curve3D? ParseStepLine(StepEntity entity, Dictionary<int, StepEntity> entities)
    {
        // Format: LINE('name',#point,#vector)
        var refs = ExtractReferences(entity.Arguments);
        if (refs.Count < 2) return null;
        
        var p1 = GetPointFromRef(refs[0], entities);
        var dirEntity = entities.GetValueOrDefault(refs[1]);
        
        if (p1 == null || dirEntity == null) return null;
        
        // Get direction and length from VECTOR entity
        var vecRefs = ExtractReferences(dirEntity.Arguments);
        if (vecRefs.Count < 2) return null;
        
        var dirRef = entities.GetValueOrDefault(vecRefs[0]);
        var length = ExtractNumber(dirEntity.Arguments);
        
        if (dirRef == null || length <= 0) return null;
        
        var dir = ParseDirection(dirRef.Arguments);
        if (dir == null) return null;
        
        var p2 = new Point3D(
            p1.Value.X + dir.Value.X * length,
            p1.Value.Y + dir.Value.Y * length,
            p1.Value.Z + dir.Value.Z * length);
        
        return new Curve3D
        {
            Type = CurveType.Line,
            Points = new List<Point3D> { p1.Value, p2 }
        };
    }
    
    private Curve3D? ParseStepCircle(StepEntity entity, Dictionary<int, StepEntity> entities)
    {
        // Format: CIRCLE('name',#axis,radius)
        var match = Regex.Match(entity.Arguments, @"#(\d+)\s*,\s*([-\d.eE+]+)");
        if (!match.Success) return null;
        
        var axisRef = int.Parse(match.Groups[1].Value);
        var radius = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        
        var axisEntity = entities.GetValueOrDefault(axisRef);
        if (axisEntity == null) return null;
        
        var center = GetPointFromAxisPlacement(axisEntity, entities) ?? new Point3D(0, 0, 0);
        
        // Generate circle points
        var points = new List<Point3D>();
        for (int i = 0; i <= 36; i++)
        {
            var angle = i * 10 * Math.PI / 180;
            points.Add(new Point3D(
                center.X + radius * Math.Cos(angle),
                center.Y + radius * Math.Sin(angle),
                center.Z));
        }
        
        return new Curve3D
        {
            Type = CurveType.Circle,
            Points = points,
            IsClosed = true
        };
    }
    
    private Curve3D? ParseStepBSpline(StepEntity entity, Dictionary<int, StepEntity> entities)
    {
        var refs = ExtractReferences(entity.Arguments);
        if (refs.Count == 0) return null;
        
        var points = refs
            .Select(r => GetPointFromRef(r, entities))
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToList();
        
        if (points.Count < 2) return null;
        
        return new Curve3D
        {
            Type = CurveType.Spline,
            Points = points
        };
    }
    
    private Curve3D? ParseStepPolyline(StepEntity entity, Dictionary<int, StepEntity> entities)
    {
        var refs = ExtractReferences(entity.Arguments);
        var points = refs
            .Select(r => GetPointFromRef(r, entities))
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToList();
        
        if (points.Count < 2) return null;
        
        return new Curve3D
        {
            Type = CurveType.Polyline,
            Points = points
        };
    }
    
    private Surface3D? ParseStepPlane(StepEntity entity, Dictionary<int, StepEntity> entities)
    {
        var refs = ExtractReferences(entity.Arguments);
        if (refs.Count == 0) return null;
        
        var axisEntity = entities.GetValueOrDefault(refs[0]);
        if (axisEntity == null) return null;
        
        var center = GetPointFromAxisPlacement(axisEntity, entities) ?? new Point3D(0, 0, 0);
        
        return new Surface3D
        {
            Type = SurfaceType.Plane,
            ControlPoints = new List<List<Point3D>> { new List<Point3D> { center } }
        };
    }
    
    private Surface3D? ParseStepCylinder(StepEntity entity, Dictionary<int, StepEntity> entities)
    {
        var match = Regex.Match(entity.Arguments, @"#(\d+)\s*,\s*([-\d.eE+]+)");
        if (!match.Success) return null;
        
        var axisRef = int.Parse(match.Groups[1].Value);
        var radius = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        
        var axisEntity = entities.GetValueOrDefault(axisRef);
        var center = axisEntity != null 
            ? GetPointFromAxisPlacement(axisEntity, entities) ?? new Point3D(0, 0, 0)
            : new Point3D(0, 0, 0);
        
        return new Surface3D
        {
            Type = SurfaceType.Cylinder,
            ControlPoints = new List<List<Point3D>> { new List<Point3D> { center } }
        };
    }
    
    private List<int> ExtractReferences(string args)
    {
        var refs = new List<int>();
        var matches = Regex.Matches(args, @"#(\d+)");
        foreach (Match match in matches)
        {
            refs.Add(int.Parse(match.Groups[1].Value));
        }
        return refs;
    }
    
    private double ExtractNumber(string args)
    {
        var match = Regex.Match(args, @"(?<![#\d])([-\d.eE+]+)\s*\)");
        if (match.Success)
        {
            return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        return 0;
    }
    
    private Point3D? GetPointFromRef(int refId, Dictionary<int, StepEntity> entities)
    {
        var entity = entities.GetValueOrDefault(refId);
        if (entity == null) return null;
        
        if (entity.Data is Point3D pt) return pt;
        
        if (entity.Type == "CARTESIAN_POINT")
        {
            return ParseCartesianPoint(entity.Arguments);
        }
        
        return null;
    }
    
    private Point3D? GetPointFromAxisPlacement(StepEntity axisEntity, Dictionary<int, StepEntity> entities)
    {
        var refs = ExtractReferences(axisEntity.Arguments);
        if (refs.Count > 0)
        {
            return GetPointFromRef(refs[0], entities);
        }
        return null;
    }
    
    private Vector3D? ParseDirection(string args)
    {
        var match = Regex.Match(args, @"\(\s*([-\d.eE+]+)\s*,\s*([-\d.eE+]+)\s*,\s*([-\d.eE+]+)\s*\)");
        if (match.Success)
        {
            return new Vector3D(
                double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
        }
        return null;
    }
    
    #endregion
    
    #region IGES Parser
    
    private void ParseIgesFile(string content, ImportResult result)
    {
        var lines = content.Split('\n');
        
        // IGES file sections: S(tart), G(lobal), D(irectory), P(arameter), T(erminate)
        var parameterLines = new List<string>();
        var directoryLines = new List<string>();
        
        foreach (var line in lines)
        {
            if (line.Length < 73) continue;
            
            var section = line[72];
            switch (section)
            {
                case 'D':
                    directoryLines.Add(line);
                    break;
                case 'P':
                    parameterLines.Add(line);
                    break;
            }
        }
        
        // Parse directory entries (pairs of lines)
        var entities = new Dictionary<int, IgesEntity>();
        for (int i = 0; i < directoryLines.Count - 1; i += 2)
        {
            var entity = ParseIgesDirectoryEntry(directoryLines[i], directoryLines[i + 1]);
            if (entity != null)
            {
                entities[entity.DirectoryPointer] = entity;
            }
        }
        
        // Build parameter data string
        var paramData = new StringBuilder();
        foreach (var line in parameterLines)
        {
            paramData.Append(line.Substring(0, 64).Trim());
        }
        var paramString = paramData.ToString();
        
        // Parse each entity
        foreach (var entity in entities.Values)
        {
            ParseIgesEntity(entity, paramString, result, entities);
        }
    }
    
    private class IgesEntity
    {
        public int EntityType { get; set; }
        public int ParameterPointer { get; set; }
        public int DirectoryPointer { get; set; }
        public int LineCount { get; set; }
    }
    
    private IgesEntity? ParseIgesDirectoryEntry(string line1, string line2)
    {
        try
        {
            return new IgesEntity
            {
                EntityType = int.Parse(line1.Substring(0, 8).Trim()),
                ParameterPointer = int.Parse(line1.Substring(8, 8).Trim()),
                DirectoryPointer = int.Parse(line1.Substring(64, 8).Trim()),
                LineCount = int.Parse(line2.Substring(24, 8).Trim())
            };
        }
        catch
        {
            return null;
        }
    }
    
    private void ParseIgesEntity(IgesEntity entity, string paramData, ImportResult result, 
        Dictionary<int, IgesEntity> entities)
    {
        // Extract parameters for this entity
        var startMarker = $"{entity.DirectoryPointer}P";
        var start = paramData.IndexOf($";{entity.DirectoryPointer - 1}P");
        if (start < 0) start = 0;
        
        var end = paramData.IndexOf(";", start + 1);
        if (end < 0) end = paramData.Length;
        
        var section = paramData.Substring(start, end - start);
        var values = section.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .ToArray();
        
        switch (entity.EntityType)
        {
            case 110: // Line
                if (values.Length >= 7)
                {
                    var curve = new Curve3D { Type = CurveType.Line };
                    curve.Points.Add(new Point3D(
                        ParseIgesDouble(values, 1),
                        ParseIgesDouble(values, 2),
                        ParseIgesDouble(values, 3)));
                    curve.Points.Add(new Point3D(
                        ParseIgesDouble(values, 4),
                        ParseIgesDouble(values, 5),
                        ParseIgesDouble(values, 6)));
                    result.Curves.Add(curve);
                }
                break;
                
            case 100: // Circular arc
                if (values.Length >= 6)
                {
                    var z = ParseIgesDouble(values, 1);
                    var cx = ParseIgesDouble(values, 2);
                    var cy = ParseIgesDouble(values, 3);
                    var sx = ParseIgesDouble(values, 4);
                    var sy = ParseIgesDouble(values, 5);
                    var ex = values.Length > 6 ? ParseIgesDouble(values, 6) : sx;
                    var ey = values.Length > 7 ? ParseIgesDouble(values, 7) : sy;
                    
                    var radius = Math.Sqrt((sx - cx) * (sx - cx) + (sy - cy) * (sy - cy));
                    var startAngle = Math.Atan2(sy - cy, sx - cx);
                    var endAngle = Math.Atan2(ey - cy, ex - cx);
                    
                    var curve = new Curve3D { Type = CurveType.Arc };
                    var steps = 20;
                    for (int i = 0; i <= steps; i++)
                    {
                        var t = i / (double)steps;
                        var angle = startAngle + t * (endAngle - startAngle);
                        curve.Points.Add(new Point3D(
                            cx + radius * Math.Cos(angle),
                            cy + radius * Math.Sin(angle),
                            z));
                    }
                    result.Curves.Add(curve);
                }
                break;
                
            case 126: // B-Spline curve
                var spline = ParseIgesBSpline(values);
                if (spline != null) result.Curves.Add(spline);
                break;
                
            case 116: // Point
                if (values.Length >= 4)
                {
                    result.Points.Add(new Point3D(
                        ParseIgesDouble(values, 1),
                        ParseIgesDouble(values, 2),
                        ParseIgesDouble(values, 3)));
                }
                break;
        }
    }
    
    private Curve3D? ParseIgesBSpline(string[] values)
    {
        if (values.Length < 10) return null;
        
        try
        {
            var n = int.Parse(values[1]); // Upper index of sum
            var controlPointCount = n + 1;
            
            // Skip to control points (after knots)
            var knotCount = n + int.Parse(values[2]) + 2;
            var cpStart = 7 + knotCount;
            
            var curve = new Curve3D { Type = CurveType.Spline };
            
            for (int i = 0; i < controlPointCount && cpStart + i * 3 + 2 < values.Length; i++)
            {
                var idx = cpStart + i * 3;
                curve.Points.Add(new Point3D(
                    ParseIgesDouble(values, idx),
                    ParseIgesDouble(values, idx + 1),
                    ParseIgesDouble(values, idx + 2)));
            }
            
            return curve.Points.Count >= 2 ? curve : null;
        }
        catch
        {
            return null;
        }
    }
    
    private double ParseIgesDouble(string[] values, int index)
    {
        if (index >= values.Length) return 0;
        
        var value = values[index].Trim();
        // IGES uses 'D' for exponent
        value = value.Replace("D", "E").Replace("d", "e");
        
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        return 0;
    }
    
    #endregion
    
    private void CalculateBounds(ImportResult result)
    {
        var allPoints = new List<Point3D>();
        allPoints.AddRange(result.Points);
        foreach (var curve in result.Curves)
        {
            allPoints.AddRange(curve.Points);
        }
        
        if (allPoints.Count == 0)
        {
            result.Bounds = new Rect3D(0, 0, 0, 0, 0, 0);
            return;
        }
        
        var minX = allPoints.Min(p => p.X);
        var minY = allPoints.Min(p => p.Y);
        var minZ = allPoints.Min(p => p.Z);
        var maxX = allPoints.Max(p => p.X);
        var maxY = allPoints.Max(p => p.Y);
        var maxZ = allPoints.Max(p => p.Z);
        
        result.Bounds = new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }
    
    /// <summary>
    /// Slice 3D geometry at specified Z height to get 2D contours
    /// </summary>
    public List<List<Point>> SliceAtZ(ImportResult result, double z, double tolerance = 0.001)
    {
        var contours = new List<List<Point>>();
        
        foreach (var curve in result.Curves)
        {
            var contour = new List<Point>();
            
            for (int i = 0; i < curve.Points.Count - 1; i++)
            {
                var p1 = curve.Points[i];
                var p2 = curve.Points[i + 1];
                
                // Check if segment crosses Z plane
                if ((p1.Z <= z && p2.Z >= z) || (p1.Z >= z && p2.Z <= z))
                {
                    if (Math.Abs(p2.Z - p1.Z) < tolerance)
                    {
                        // Segment is on plane
                        contour.Add(new Point(p1.X, p1.Y));
                        contour.Add(new Point(p2.X, p2.Y));
                    }
                    else
                    {
                        // Interpolate intersection point
                        var t = (z - p1.Z) / (p2.Z - p1.Z);
                        var x = p1.X + t * (p2.X - p1.X);
                        var y = p1.Y + t * (p2.Y - p1.Y);
                        contour.Add(new Point(x, y));
                    }
                }
                else if (Math.Abs(p1.Z - z) < tolerance)
                {
                    contour.Add(new Point(p1.X, p1.Y));
                }
            }
            
            if (contour.Count >= 2)
            {
                contours.Add(contour);
            }
        }
        
        return contours;
    }
}
