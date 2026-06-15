namespace E3Studio.Models;

/// <summary>
/// Represents a cutting tool (endmill, drill, v-bit, etc.)
/// </summary>
public class Tool
{
    public int Id { get; set; }
    public int Number { get; set; } = 1; // Tool number in tool library (T1, T2, etc.)
    public string Name { get; set; } = "Unnamed Tool";
    public ToolType Type { get; set; } = ToolType.Endmill;
    public double Diameter { get; set; } = 3.175; // mm (1/8")
    public double FluteLength { get; set; } = 10.0;
    public double TotalLength { get; set; } = 38.0;
    public double Length { get; set; } = 38.0;  // Alias for TotalLength
    public int Flutes { get; set; } = 2;
    public double VAngle { get; set; } = 0; // For V-bits
    public double Angle { get; set; } = 0; // V-bit angle (same as VAngle)
    public double TipDiameter { get; set; } = 0.1; // V-bit tip diameter
    public string Notes { get; set; } = "";

    public override string ToString() => $"T{Number}: {Name} (Ø{Diameter}mm)";
}

public enum ToolType
{
    Endmill,
    BallNose,
    VBit,
    Drill,
    Engraver
}
