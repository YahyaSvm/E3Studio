using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Measurement and dimension tool for canvas
/// </summary>
public class MeasurementTool
{
    private readonly Canvas _canvas;
    private readonly List<UIElement> _measurementElements = new();
    
    public MeasurementMode Mode { get; set; } = MeasurementMode.Distance;
    public bool IsActive { get; set; }
    public bool ShowDimensions { get; set; } = true;
    
    public event EventHandler<MeasurementResult>? MeasurementComplete;
    
    private Point? _startPoint;
    private Point? _midPoint;
    private Line? _previewLine;
    
    public MeasurementTool(Canvas canvas)
    {
        _canvas = canvas;
    }
    
    public void Start(Point point)
    {
        if (!IsActive) return;
        
        _startPoint = point;
        _midPoint = null;
        
        // Create preview line
        _previewLine = new Line
        {
            Stroke = Brushes.Yellow,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            X1 = point.X,
            Y1 = point.Y,
            X2 = point.X,
            Y2 = point.Y
        };
        _canvas.Children.Add(_previewLine);
        _measurementElements.Add(_previewLine);
    }
    
    public void Update(Point point)
    {
        if (!IsActive || _startPoint == null || _previewLine == null) return;
        
        if (Mode == MeasurementMode.Angle && _midPoint != null)
        {
            // Update second line for angle measurement
            _previewLine.X1 = _midPoint.Value.X;
            _previewLine.Y1 = _midPoint.Value.Y;
        }
        
        _previewLine.X2 = point.X;
        _previewLine.Y2 = point.Y;
    }
    
    public void AddPoint(Point point)
    {
        if (!IsActive || _startPoint == null) return;
        
        if (Mode == MeasurementMode.Angle)
        {
            if (_midPoint == null)
            {
                // First click sets mid point (vertex)
                _midPoint = point;
                
                // Add first line
                var firstLine = new Line
                {
                    Stroke = Brushes.Yellow,
                    StrokeThickness = 1,
                    X1 = _startPoint.Value.X,
                    Y1 = _startPoint.Value.Y,
                    X2 = point.X,
                    Y2 = point.Y
                };
                _canvas.Children.Add(firstLine);
                _measurementElements.Add(firstLine);
                
                // Start new preview line from vertex
                if (_previewLine != null)
                {
                    _previewLine.X1 = point.X;
                    _previewLine.Y1 = point.Y;
                }
            }
            else
            {
                // Final point - calculate angle
                Complete(point);
            }
        }
        else
        {
            Complete(point);
        }
    }
    
    public void Complete(Point endPoint)
    {
        if (!IsActive || _startPoint == null) return;
        
        MeasurementResult result;
        
        switch (Mode)
        {
            case MeasurementMode.Distance:
                result = MeasureDistance(_startPoint.Value, endPoint);
                break;
            case MeasurementMode.Angle:
                if (_midPoint == null) return;
                result = MeasureAngle(_startPoint.Value, _midPoint.Value, endPoint);
                break;
            case MeasurementMode.Radius:
                result = MeasureRadius(_startPoint.Value, endPoint);
                break;
            case MeasurementMode.Area:
                result = MeasureArea(_startPoint.Value, endPoint);
                break;
            default:
                return;
        }
        
        // Draw dimension
        if (ShowDimensions)
        {
            DrawDimension(result);
        }
        
        MeasurementComplete?.Invoke(this, result);
        
        // Reset state
        _startPoint = null;
        _midPoint = null;
        
        // Remove preview line
        if (_previewLine != null)
        {
            _canvas.Children.Remove(_previewLine);
            _measurementElements.Remove(_previewLine);
            _previewLine = null;
        }
    }
    
    private MeasurementResult MeasureDistance(Point start, Point end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        
        return new MeasurementResult
        {
            Type = MeasurementMode.Distance,
            Value = distance,
            StartPoint = start,
            EndPoint = end,
            Unit = "mm",
            Description = $"Distance: {distance:F3} mm",
            DeltaX = dx,
            DeltaY = dy,
            Angle = angle
        };
    }
    
    private MeasurementResult MeasureAngle(Point start, Point vertex, Point end)
    {
        double angle1 = Math.Atan2(start.Y - vertex.Y, start.X - vertex.X);
        double angle2 = Math.Atan2(end.Y - vertex.Y, end.X - vertex.X);
        double angle = (angle2 - angle1) * 180 / Math.PI;
        
        // Normalize to 0-360
        while (angle < 0) angle += 360;
        while (angle > 360) angle -= 360;
        
        // Use smaller angle
        if (angle > 180) angle = 360 - angle;
        
        return new MeasurementResult
        {
            Type = MeasurementMode.Angle,
            Value = angle,
            StartPoint = start,
            EndPoint = end,
            MidPoint = vertex,
            Unit = "°",
            Description = $"Angle: {angle:F2}°"
        };
    }
    
    private MeasurementResult MeasureRadius(Point center, Point point)
    {
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;
        double radius = Math.Sqrt(dx * dx + dy * dy);
        double diameter = radius * 2;
        double circumference = 2 * Math.PI * radius;
        
        return new MeasurementResult
        {
            Type = MeasurementMode.Radius,
            Value = radius,
            StartPoint = center,
            EndPoint = point,
            Unit = "mm",
            Description = $"Radius: {radius:F3} mm\nDiameter: {diameter:F3} mm\nCircumference: {circumference:F3} mm"
        };
    }
    
    private MeasurementResult MeasureArea(Point corner1, Point corner2)
    {
        double width = Math.Abs(corner2.X - corner1.X);
        double height = Math.Abs(corner2.Y - corner1.Y);
        double area = width * height;
        double perimeter = 2 * (width + height);
        
        return new MeasurementResult
        {
            Type = MeasurementMode.Area,
            Value = area,
            StartPoint = corner1,
            EndPoint = corner2,
            Unit = "mm²",
            Description = $"Area: {area:F3} mm²\nWidth: {width:F3} mm\nHeight: {height:F3} mm\nPerimeter: {perimeter:F3} mm"
        };
    }
    
    private void DrawDimension(MeasurementResult result)
    {
        switch (result.Type)
        {
            case MeasurementMode.Distance:
                DrawLinearDimension(result);
                break;
            case MeasurementMode.Angle:
                DrawAngularDimension(result);
                break;
            case MeasurementMode.Radius:
                DrawRadialDimension(result);
                break;
            case MeasurementMode.Area:
                DrawAreaDimension(result);
                break;
        }
    }
    
    private void DrawLinearDimension(MeasurementResult result)
    {
        var start = result.StartPoint;
        var end = result.EndPoint;
        
        // Main dimension line
        var line = new Line
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 1,
            X1 = start.X, Y1 = start.Y,
            X2 = end.X, Y2 = end.Y
        };
        _canvas.Children.Add(line);
        _measurementElements.Add(line);
        
        // Extension lines
        double offset = 10;
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X) + Math.PI / 2;
        double perpX = offset * Math.Cos(angle);
        double perpY = offset * Math.Sin(angle);
        
        var ext1 = new Line
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 0.5,
            X1 = start.X, Y1 = start.Y,
            X2 = start.X + perpX, Y2 = start.Y + perpY
        };
        _canvas.Children.Add(ext1);
        _measurementElements.Add(ext1);
        
        var ext2 = new Line
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 0.5,
            X1 = end.X, Y1 = end.Y,
            X2 = end.X + perpX, Y2 = end.Y + perpY
        };
        _canvas.Children.Add(ext2);
        _measurementElements.Add(ext2);
        
        // Arrow heads
        DrawArrowHead(start, end, Brushes.Cyan);
        DrawArrowHead(end, start, Brushes.Cyan);
        
        // Text label
        double midX = (start.X + end.X) / 2;
        double midY = (start.Y + end.Y) / 2;
        
        var text = new TextBlock
        {
            Text = $"{result.Value:F2}",
            Foreground = Brushes.Cyan,
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30))
        };
        Canvas.SetLeft(text, midX + 5);
        Canvas.SetTop(text, midY - 10);
        _canvas.Children.Add(text);
        _measurementElements.Add(text);
    }
    
    private void DrawAngularDimension(MeasurementResult result)
    {
        var vertex = result.MidPoint ?? result.StartPoint;
        var start = result.StartPoint;
        var end = result.EndPoint;
        
        // Arc for angle
        double radius = 30;
        double startAngle = Math.Atan2(start.Y - vertex.Y, start.X - vertex.X);
        double endAngle = Math.Atan2(end.Y - vertex.Y, end.X - vertex.X);
        
        var arcGeometry = new StreamGeometry();
        using (var ctx = arcGeometry.Open())
        {
            var arcStart = new Point(vertex.X + radius * Math.Cos(startAngle), 
                                    vertex.Y + radius * Math.Sin(startAngle));
            var arcEnd = new Point(vertex.X + radius * Math.Cos(endAngle), 
                                   vertex.Y + radius * Math.Sin(endAngle));
            
            ctx.BeginFigure(arcStart, false, false);
            ctx.ArcTo(arcEnd, new Size(radius, radius), 0, 
                     Math.Abs(endAngle - startAngle) > Math.PI, 
                     SweepDirection.Clockwise, true, false);
        }
        
        var arcPath = new Path
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 1,
            Data = arcGeometry
        };
        _canvas.Children.Add(arcPath);
        _measurementElements.Add(arcPath);
        
        // Text
        double midAngle = (startAngle + endAngle) / 2;
        double textX = vertex.X + (radius + 15) * Math.Cos(midAngle);
        double textY = vertex.Y + (radius + 15) * Math.Sin(midAngle);
        
        var text = new TextBlock
        {
            Text = $"{result.Value:F1}°",
            Foreground = Brushes.Cyan,
            FontSize = 12
        };
        Canvas.SetLeft(text, textX);
        Canvas.SetTop(text, textY);
        _canvas.Children.Add(text);
        _measurementElements.Add(text);
    }
    
    private void DrawRadialDimension(MeasurementResult result)
    {
        var center = result.StartPoint;
        var point = result.EndPoint;
        
        // Circle
        var ellipse = new Ellipse
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Width = result.Value * 2,
            Height = result.Value * 2
        };
        Canvas.SetLeft(ellipse, center.X - result.Value);
        Canvas.SetTop(ellipse, center.Y - result.Value);
        _canvas.Children.Add(ellipse);
        _measurementElements.Add(ellipse);
        
        // Radius line
        var line = new Line
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 1,
            X1 = center.X, Y1 = center.Y,
            X2 = point.X, Y2 = point.Y
        };
        _canvas.Children.Add(line);
        _measurementElements.Add(line);
        
        // Text
        double midX = (center.X + point.X) / 2;
        double midY = (center.Y + point.Y) / 2;
        
        var text = new TextBlock
        {
            Text = $"R{result.Value:F2}",
            Foreground = Brushes.Cyan,
            FontSize = 12
        };
        Canvas.SetLeft(text, midX + 5);
        Canvas.SetTop(text, midY - 10);
        _canvas.Children.Add(text);
        _measurementElements.Add(text);
    }
    
    private void DrawAreaDimension(MeasurementResult result)
    {
        var corner1 = result.StartPoint;
        var corner2 = result.EndPoint;
        
        // Rectangle outline
        var rect = new Rectangle
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Width = Math.Abs(corner2.X - corner1.X),
            Height = Math.Abs(corner2.Y - corner1.Y),
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255))
        };
        Canvas.SetLeft(rect, Math.Min(corner1.X, corner2.X));
        Canvas.SetTop(rect, Math.Min(corner1.Y, corner2.Y));
        _canvas.Children.Add(rect);
        _measurementElements.Add(rect);
        
        // Area text
        double centerX = (corner1.X + corner2.X) / 2;
        double centerY = (corner1.Y + corner2.Y) / 2;
        
        var text = new TextBlock
        {
            Text = $"{result.Value:F2} mm²",
            Foreground = Brushes.Cyan,
            FontSize = 12
        };
        Canvas.SetLeft(text, centerX - 30);
        Canvas.SetTop(text, centerY - 8);
        _canvas.Children.Add(text);
        _measurementElements.Add(text);
    }
    
    private void DrawArrowHead(Point from, Point to, Brush brush)
    {
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        double arrowLength = 8;
        double arrowAngle = Math.PI / 6;
        
        double x1 = from.X + arrowLength * Math.Cos(angle + arrowAngle);
        double y1 = from.Y + arrowLength * Math.Sin(angle + arrowAngle);
        double x2 = from.X + arrowLength * Math.Cos(angle - arrowAngle);
        double y2 = from.Y + arrowLength * Math.Sin(angle - arrowAngle);
        
        var arrow = new Polygon
        {
            Fill = brush,
            Points = new PointCollection
            {
                from,
                new Point(x1, y1),
                new Point(x2, y2)
            }
        };
        _canvas.Children.Add(arrow);
        _measurementElements.Add(arrow);
    }
    
    public void ClearMeasurements()
    {
        foreach (var element in _measurementElements)
        {
            _canvas.Children.Remove(element);
        }
        _measurementElements.Clear();
    }
    
    public void Cancel()
    {
        _startPoint = null;
        _midPoint = null;
        
        if (_previewLine != null)
        {
            _canvas.Children.Remove(_previewLine);
            _measurementElements.Remove(_previewLine);
            _previewLine = null;
        }
    }
}

public enum MeasurementMode
{
    Distance,
    Angle,
    Radius,
    Area
}

public class MeasurementResult
{
    public MeasurementMode Type { get; set; }
    public double Value { get; set; }
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    public Point? MidPoint { get; set; }
    public string Unit { get; set; } = "mm";
    public string Description { get; set; } = "";
    public double DeltaX { get; set; }
    public double DeltaY { get; set; }
    public double Angle { get; set; }
}
