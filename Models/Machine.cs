namespace E3Studio.Models;

/// <summary>
/// Represents a CNC Machine configuration
/// </summary>
public class Machine
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Generic 3-Axis CNC";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public MachineType Type { get; set; } = MachineType.Router;
    
    // Work envelope (mm)
    public double MaxX { get; set; } = 300;
    public double MaxY { get; set; } = 200;
    public double MaxZ { get; set; } = 100;
    
    // Speed limits
    public double MaxFeedRate { get; set; } = 5000;      // mm/min
    public double MaxRapidRate { get; set; } = 10000;    // mm/min
    public double MaxSpindleRPM { get; set; } = 24000;
    public double MinSpindleRPM { get; set; } = 5000;
    
    // Safe heights
    public double SafeZ { get; set; } = 25.0;
    public double RapidClearanceZ { get; set; } = 5.0;
    public double ToolChangeZ { get; set; } = 50.0;
    
    // Machine capabilities
    public bool HasToolChanger { get; set; } = false;
    public int ToolChangerCapacity { get; set; } = 0;
    public bool HasCoolant { get; set; } = false;
    public bool HasProbe { get; set; } = false;
    public bool HasRotaryAxis { get; set; } = false;
    
    // Post processor settings
    public PostProcessor PostProcessor { get; set; } = PostProcessor.Grbl;
    public string FileExtension { get; set; } = ".nc";
    public string ProgramStartCode { get; set; } = "";
    public string ProgramEndCode { get; set; } = "";
    public string ToolChangeCode { get; set; } = "M6 T{tool}";
    
    // Coordinate system
    public bool UseAbsoluteCoordinates { get; set; } = true;
    public bool UseMetricUnits { get; set; } = true;
    public int DecimalPlaces { get; set; } = 3;
    
    // Home position
    public double HomeX { get; set; } = 0;
    public double HomeY { get; set; } = 0;
    public double HomeZ { get; set; } = 0;
    
    public override string ToString() => $"{Name} ({Manufacturer} {Model})".Trim();
    
    /// <summary>
    /// Get default machines library
    /// </summary>
    public static List<Machine> GetDefaultMachines() => new()
    {
        // ═══════════════════════════════════════════════════════════════════
        // ENDER3CNC MACHINES (E3 Series) - PRIMARY
        // ═══════════════════════════════════════════════════════════════════
        new Machine
        {
            Id = "e3cnc-budget",
            Name = "Ender3CNC Budget",
            Manufacturer = "E3CNC",
            Model = "Budget Version",
            Type = MachineType.Router,
            MaxX = 165, MaxY = 260, MaxZ = 150,
            MaxFeedRate = 3000, MaxRapidRate = 5000, MaxSpindleRPM = 30000, MinSpindleRPM = 5000,
            SafeZ = 25.0, RapidClearanceZ = 5.0, ToolChangeZ = 50.0,
            HasToolChanger = false, HasCoolant = false, HasProbe = false,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc",
            ProgramStartCode = "G21 G90 G17\nG28 G91 Z0\nG90",
            ProgramEndCode = "M5\nG28 G91 Z0\nG90\nM30",
            DecimalPlaces = 3
        },
        new Machine
        {
            Id = "e3cnc-standard",
            Name = "Ender3CNC Standard",
            Manufacturer = "E3CNC",
            Model = "Standard Version",
            Type = MachineType.Router,
            MaxX = 220, MaxY = 220, MaxZ = 100,
            MaxFeedRate = 4000, MaxRapidRate = 6000, MaxSpindleRPM = 30000, MinSpindleRPM = 5000,
            SafeZ = 25.0, RapidClearanceZ = 5.0, ToolChangeZ = 50.0,
            HasToolChanger = false, HasCoolant = false, HasProbe = true,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc",
            ProgramStartCode = "G21 G90 G17\nG28 G91 Z0\nG90",
            ProgramEndCode = "M5\nG28 G91 Z0\nG90\nM30",
            DecimalPlaces = 3
        },
        new Machine
        {
            Id = "e3cnc-pro",
            Name = "Ender3CNC Pro",
            Manufacturer = "E3CNC",
            Model = "Pro Version (MGN12 Rails)",
            Type = MachineType.Router,
            MaxX = 235, MaxY = 235, MaxZ = 120,
            MaxFeedRate = 5000, MaxRapidRate = 8000, MaxSpindleRPM = 30000, MinSpindleRPM = 5000,
            SafeZ = 30.0, RapidClearanceZ = 5.0, ToolChangeZ = 60.0,
            HasToolChanger = false, HasCoolant = true, HasProbe = true,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc",
            ProgramStartCode = "G21 G90 G17\nG28 G91 Z0\nG90",
            ProgramEndCode = "M5\nM9\nG28 G91 Z0\nG90\nM30",
            DecimalPlaces = 3
        },
        new Machine
        {
            Id = "e3cnc-xl",
            Name = "Ender3CNC XL",
            Manufacturer = "E3CNC",
            Model = "XL Extended Version",
            Type = MachineType.Router,
            MaxX = 300, MaxY = 300, MaxZ = 150,
            MaxFeedRate = 5000, MaxRapidRate = 8000, MaxSpindleRPM = 30000, MinSpindleRPM = 5000,
            SafeZ = 30.0, RapidClearanceZ = 5.0, ToolChangeZ = 70.0,
            HasToolChanger = false, HasCoolant = true, HasProbe = true,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc",
            ProgramStartCode = "G21 G90 G17\nG28 G91 Z0\nG90",
            ProgramEndCode = "M5\nM9\nG28 G91 Z0\nG90\nM30",
            DecimalPlaces = 3
        },
        new Machine
        {
            Id = "e3cnc-laser",
            Name = "Ender3CNC Laser Module",
            Manufacturer = "E3CNC",
            Model = "Laser Attachment",
            Type = MachineType.Laser,
            MaxX = 220, MaxY = 220, MaxZ = 50,
            MaxFeedRate = 6000, MaxRapidRate = 10000, MaxSpindleRPM = 1000, MinSpindleRPM = 0,
            SafeZ = 10.0, RapidClearanceZ = 2.0, ToolChangeZ = 20.0,
            HasToolChanger = false, HasCoolant = false, HasProbe = false,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc",
            ProgramStartCode = "G21 G90 G17\nM4 S0",
            ProgramEndCode = "M5\nG0 X0 Y0\nM30",
            DecimalPlaces = 3
        },
        
        // ═══════════════════════════════════════════════════════════════════
        // OTHER HOBBY MACHINES
        // ═══════════════════════════════════════════════════════════════════
        new Machine
        {
            Id = "generic-3axis",
            Name = "Generic 3-Axis CNC Router",
            Manufacturer = "Generic",
            Model = "3-Axis",
            Type = MachineType.Router,
            MaxX = 300, MaxY = 200, MaxZ = 100,
            MaxFeedRate = 5000, MaxRapidRate = 10000, MaxSpindleRPM = 24000,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc"
        },
        new Machine
        {
            Id = "3018-pro",
            Name = "3018 Pro CNC",
            Manufacturer = "Generic",
            Model = "3018 Pro",
            Type = MachineType.Router,
            MaxX = 300, MaxY = 180, MaxZ = 45,
            MaxFeedRate = 1500, MaxRapidRate = 3000, MaxSpindleRPM = 10000,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc"
        },
        new Machine
        {
            Id = "shapeoko-4xxl",
            Name = "Shapeoko 4 XXL",
            Manufacturer = "Carbide 3D",
            Model = "Shapeoko 4 XXL",
            Type = MachineType.Router,
            MaxX = 838, MaxY = 838, MaxZ = 95,
            MaxFeedRate = 5000, MaxRapidRate = 10000, MaxSpindleRPM = 18000,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc"
        },
        new Machine
        {
            Id = "xcarve-pro",
            Name = "X-Carve Pro 4x4",
            Manufacturer = "Inventables",
            Model = "X-Carve Pro",
            Type = MachineType.Router,
            MaxX = 1219, MaxY = 1219, MaxZ = 95,
            MaxFeedRate = 8000, MaxRapidRate = 12000, MaxSpindleRPM = 24000,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc"
        },
        new Machine
        {
            Id = "onefinity-x50",
            Name = "Onefinity Woodworker X-50",
            Manufacturer = "Onefinity",
            Model = "Woodworker X-50",
            Type = MachineType.Router,
            MaxX = 816, MaxY = 816, MaxZ = 133,
            MaxFeedRate = 6000, MaxRapidRate = 10000, MaxSpindleRPM = 18000,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc"
        },
        
        // ═══════════════════════════════════════════════════════════════════
        // INDUSTRIAL MACHINES
        // ═══════════════════════════════════════════════════════════════════
        new Machine
        {
            Id = "tormach-440",
            Name = "Tormach PCNC 440",
            Manufacturer = "Tormach",
            Model = "PCNC 440",
            Type = MachineType.Mill,
            MaxX = 254, MaxY = 165, MaxZ = 254,
            MaxFeedRate = 2500, MaxRapidRate = 5000, MaxSpindleRPM = 10000,
            HasToolChanger = true, ToolChangerCapacity = 8,
            PostProcessor = PostProcessor.LinuxCNC,
            FileExtension = ".ngc"
        },
        new Machine
        {
            Id = "haas-minimill",
            Name = "Haas Mini Mill",
            Manufacturer = "Haas",
            Model = "Mini Mill",
            Type = MachineType.Mill,
            MaxX = 406, MaxY = 305, MaxZ = 254,
            MaxFeedRate = 12700, MaxRapidRate = 15240, MaxSpindleRPM = 6000,
            HasToolChanger = true, ToolChangerCapacity = 10,
            HasCoolant = true,
            PostProcessor = PostProcessor.Haas,
            FileExtension = ".nc"
        },
        new Machine
        {
            Id = "nomad-3",
            Name = "Nomad 3",
            Manufacturer = "Carbide 3D",
            Model = "Nomad 3",
            Type = MachineType.Mill,
            MaxX = 203, MaxY = 203, MaxZ = 76,
            MaxFeedRate = 2500, MaxRapidRate = 5000, MaxSpindleRPM = 24000,
            HasToolChanger = false,
            PostProcessor = PostProcessor.Grbl,
            FileExtension = ".nc"
        }
    };
}

public enum MachineType
{
    Router,
    Mill,
    Lathe,
    Laser,
    Plasma,
    WaterJet,
    EDM
}

public enum PostProcessor
{
    Grbl,
    LinuxCNC,
    Mach3,
    Mach4,
    Fanuc,
    Haas,
    Mazak,
    Siemens,
    Heidenhain,
    Custom
}
