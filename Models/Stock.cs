namespace E3Studio.Models;

/// <summary>
/// Represents the stock/workpiece dimensions and zero point
/// </summary>
public class Stock
{
    public double Width { get; set; } = 100;   // X dimension (mm)
    public double Height { get; set; } = 100;  // Y dimension (mm)
    public double Thickness { get; set; } = 10; // Z dimension (mm)
    public double Length { get => Height; set => Height = value; } // Alias for Height (Y)
    
    public StockOrigin ZeroPoint { get; set; } = StockOrigin.TopCenter;
    
    public Material? Material { get; set; }
}

public enum StockOrigin
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}
