using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace E3Studio.Dialogs;

public partial class GridSnapSettingsDialog : Window
{
    public GridSnapSettings Settings { get; private set; }
    
    public GridSnapSettingsDialog(GridSnapSettings? currentSettings = null)
    {
        InitializeComponent();
        
        Settings = currentSettings ?? new GridSnapSettings();
        LoadSettings();
    }
    
    private void LoadSettings()
    {
        ChkShowGrid.IsChecked = Settings.ShowGrid;
        TxtGridSpacing.Text = Settings.GridSpacing.ToString();
        TxtMajorLines.Text = Settings.MajorLineInterval.ToString();
        
        ChkSnapToGrid.IsChecked = Settings.SnapToGrid;
        ChkSnapToEndpoint.IsChecked = Settings.SnapToEndpoint;
        ChkSnapToMidpoint.IsChecked = Settings.SnapToMidpoint;
        ChkSnapToCenter.IsChecked = Settings.SnapToCenter;
        ChkSnapToIntersection.IsChecked = Settings.SnapToIntersection;
        ChkSnapToPerpendicular.IsChecked = Settings.SnapToPerpendicular;
        ChkSnapToTangent.IsChecked = Settings.SnapToTangent;
        ChkSnapToNearest.IsChecked = Settings.SnapToNearest;
        TxtSnapRadius.Text = Settings.SnapRadius.ToString();
        
        ChkOrthoMode.IsChecked = Settings.OrthoMode;
        ChkPolarTracking.IsChecked = Settings.PolarTracking;
        
        // Set polar angle combo
        foreach (ComboBoxItem item in PolarAngleCombo.Items)
        {
            if (item.Tag?.ToString() == Settings.PolarAngle.ToString())
            {
                PolarAngleCombo.SelectedItem = item;
                break;
            }
        }
        
        // Set grid color combo
        foreach (ComboBoxItem item in GridColorCombo.Items)
        {
            if (item.Tag?.ToString() == Settings.GridColor)
            {
                GridColorCombo.SelectedItem = item;
                break;
            }
        }
    }
    
    private void SaveSettings()
    {
        Settings.ShowGrid = ChkShowGrid.IsChecked == true;
        double.TryParse(TxtGridSpacing.Text, out double spacing);
        Settings.GridSpacing = spacing > 0 ? spacing : 10;
        
        int.TryParse(TxtMajorLines.Text, out int majorLines);
        Settings.MajorLineInterval = majorLines > 0 ? majorLines : 5;
        
        Settings.SnapToGrid = ChkSnapToGrid.IsChecked == true;
        Settings.SnapToEndpoint = ChkSnapToEndpoint.IsChecked == true;
        Settings.SnapToMidpoint = ChkSnapToMidpoint.IsChecked == true;
        Settings.SnapToCenter = ChkSnapToCenter.IsChecked == true;
        Settings.SnapToIntersection = ChkSnapToIntersection.IsChecked == true;
        Settings.SnapToPerpendicular = ChkSnapToPerpendicular.IsChecked == true;
        Settings.SnapToTangent = ChkSnapToTangent.IsChecked == true;
        Settings.SnapToNearest = ChkSnapToNearest.IsChecked == true;
        
        double.TryParse(TxtSnapRadius.Text, out double snapRadius);
        Settings.SnapRadius = snapRadius > 0 ? snapRadius : 10;
        
        Settings.OrthoMode = ChkOrthoMode.IsChecked == true;
        Settings.PolarTracking = ChkPolarTracking.IsChecked == true;
        
        if (PolarAngleCombo.SelectedItem is ComboBoxItem polarItem)
        {
            int.TryParse(polarItem.Tag?.ToString(), out int polarAngle);
            Settings.PolarAngle = polarAngle;
        }
        
        if (GridColorCombo.SelectedItem is ComboBoxItem colorItem)
        {
            Settings.GridColor = colorItem.Tag?.ToString() ?? "#404040";
        }
    }
    
    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        Settings = new GridSnapSettings();
        LoadSettings();
    }
    
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class GridSnapSettings
{
    // Grid display
    public bool ShowGrid { get; set; } = true;
    public double GridSpacing { get; set; } = 10; // mm
    public int MajorLineInterval { get; set; } = 5;
    public string GridColor { get; set; } = "#404040";
    
    // Snap options
    public bool SnapToGrid { get; set; } = true;
    public bool SnapToEndpoint { get; set; } = true;
    public bool SnapToMidpoint { get; set; } = true;
    public bool SnapToCenter { get; set; } = true;
    public bool SnapToIntersection { get; set; } = true;
    public bool SnapToPerpendicular { get; set; } = false;
    public bool SnapToTangent { get; set; } = false;
    public bool SnapToNearest { get; set; } = false;
    public double SnapRadius { get; set; } = 10; // pixels
    
    // Angle constraints
    public bool OrthoMode { get; set; } = false;
    public bool PolarTracking { get; set; } = false;
    public int PolarAngle { get; set; } = 45;
    
    public Point SnapPoint(Point inputPoint, Point? lastPoint = null)
    {
        var result = inputPoint;
        
        // Grid snap
        if (SnapToGrid)
        {
            result = new Point(
                Math.Round(inputPoint.X / GridSpacing) * GridSpacing,
                Math.Round(inputPoint.Y / GridSpacing) * GridSpacing);
        }
        
        // Ortho mode
        if (OrthoMode && lastPoint.HasValue)
        {
            var dx = Math.Abs(inputPoint.X - lastPoint.Value.X);
            var dy = Math.Abs(inputPoint.Y - lastPoint.Value.Y);
            
            if (dx > dy)
            {
                result = new Point(result.X, lastPoint.Value.Y);
            }
            else
            {
                result = new Point(lastPoint.Value.X, result.Y);
            }
        }
        
        // Polar tracking
        if (PolarTracking && lastPoint.HasValue)
        {
            var angle = Math.Atan2(inputPoint.Y - lastPoint.Value.Y, 
                inputPoint.X - lastPoint.Value.X);
            var angleDeg = angle * 180 / Math.PI;
            
            // Snap to nearest polar angle
            var snappedAngle = Math.Round(angleDeg / PolarAngle) * PolarAngle;
            var distance = Math.Sqrt(
                Math.Pow(inputPoint.X - lastPoint.Value.X, 2) + 
                Math.Pow(inputPoint.Y - lastPoint.Value.Y, 2));
            
            var radians = snappedAngle * Math.PI / 180;
            result = new Point(
                lastPoint.Value.X + distance * Math.Cos(radians),
                lastPoint.Value.Y + distance * Math.Sin(radians));
        }
        
        return result;
    }
    
    public (Point point, SnapType type)? FindObjectSnap(Point inputPoint, 
        IEnumerable<GeometryObject> objects, double screenScale)
    {
        var searchRadius = SnapRadius / screenScale;
        
        foreach (var obj in objects)
        {
            // Endpoint snap
            if (SnapToEndpoint)
            {
                foreach (var endpoint in obj.GetEndpoints())
                {
                    if (Distance(inputPoint, endpoint) < searchRadius)
                    {
                        return (endpoint, SnapType.Endpoint);
                    }
                }
            }
            
            // Midpoint snap
            if (SnapToMidpoint)
            {
                var midpoint = obj.GetMidpoint();
                if (midpoint.HasValue && Distance(inputPoint, midpoint.Value) < searchRadius)
                {
                    return (midpoint.Value, SnapType.Midpoint);
                }
            }
            
            // Center snap (for circles/arcs)
            if (SnapToCenter)
            {
                var center = obj.GetCenter();
                if (center.HasValue && Distance(inputPoint, center.Value) < searchRadius)
                {
                    return (center.Value, SnapType.Center);
                }
            }
        }
        
        return null;
    }
    
    private static double Distance(Point a, Point b)
    {
        return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    }
}

public enum SnapType
{
    None,
    Grid,
    Endpoint,
    Midpoint,
    Center,
    Intersection,
    Perpendicular,
    Tangent,
    Nearest
}

// Interface for geometry objects to support snap
public interface GeometryObject
{
    IEnumerable<Point> GetEndpoints();
    Point? GetMidpoint();
    Point? GetCenter();
}
