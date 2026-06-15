namespace E3Studio.Services.Hardware;

/// <summary>
/// Probe operations for touch-off and workpiece zeroing
/// </summary>
public class ProbeController
{
    private readonly SerialCommunication _serial;
    
    // Probe settings
    public double ProbeFeedRate { get; set; } = 100;      // mm/min (slow for accuracy)
    public double SeekFeedRate { get; set; } = 500;       // mm/min (fast initial seek)
    public double ProbeRetract { get; set; } = 2;         // mm
    public double MaxProbeDistance { get; set; } = 50;    // mm (max distance to probe)
    public double ToolDiameter { get; set; } = 0;         // For tool offset
    public double ProbeOffset { get; set; } = 0;          // Probe tip offset
    
    // Events
    public event EventHandler<ProbeResultEventArgs>? ProbeCompleted;
    public event EventHandler<string>? ProbeError;
    public event EventHandler<string>? ProbeStatus;
    
    public ProbeController(SerialCommunication serial)
    {
        _serial = serial;
    }
    
    /// <summary>
    /// Probe Z (find Z zero - top of workpiece)
    /// </summary>
    public async Task<ProbeResult?> ProbeZ()
    {
        if (!_serial.IsConnected) return null;
        
        try
        {
            ProbeStatus?.Invoke(this, "Starting Z probe...");
            
            // Fast probe to find approximate position
            var fastResult = await ProbeAxis("Z", -MaxProbeDistance, SeekFeedRate);
            if (fastResult == null || !fastResult.Success)
            {
                ProbeError?.Invoke(this, "Fast probe failed - no contact");
                return null;
            }
            
            // Retract
            _serial.SendCommand($"G91 G0 Z{ProbeRetract}");
            await Task.Delay(500);
            
            // Slow probe for accuracy
            var slowResult = await ProbeAxis("Z", -(ProbeRetract + 1), ProbeFeedRate);
            if (slowResult == null || !slowResult.Success)
            {
                ProbeError?.Invoke(this, "Slow probe failed");
                return null;
            }
            
            // Set Z zero (accounting for probe offset)
            _serial.SendCommand($"G10 L20 P1 Z{ProbeOffset}");
            
            // Retract to safe height
            _serial.SendCommand("G91 G0 Z5");
            _serial.SendCommand("G90");
            
            ProbeStatus?.Invoke(this, $"Z probe complete: {slowResult.Position:F3}mm");
            ProbeCompleted?.Invoke(this, new ProbeResultEventArgs { Result = slowResult });
            
            return slowResult;
        }
        catch (Exception ex)
        {
            ProbeError?.Invoke(this, $"Probe error: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Probe X (find edge)
    /// </summary>
    public async Task<ProbeResult?> ProbeX(bool positive = true)
    {
        if (!_serial.IsConnected) return null;
        
        try
        {
            var direction = positive ? 1 : -1;
            ProbeStatus?.Invoke(this, $"Starting X probe ({(positive ? "+" : "-")})...");
            
            var result = await ProbeAxis("X", MaxProbeDistance * direction, ProbeFeedRate);
            if (result == null || !result.Success)
            {
                ProbeError?.Invoke(this, "X probe failed - no contact");
                return null;
            }
            
            // Set X zero (accounting for tool radius and probe offset)
            var offset = (ToolDiameter / 2 + ProbeOffset) * direction;
            _serial.SendCommand($"G10 L20 P1 X{offset}");
            
            // Retract
            _serial.SendCommand($"G91 G0 X{-ProbeRetract * direction}");
            _serial.SendCommand("G90");
            
            ProbeStatus?.Invoke(this, $"X probe complete: {result.Position:F3}mm");
            ProbeCompleted?.Invoke(this, new ProbeResultEventArgs { Result = result });
            
            return result;
        }
        catch (Exception ex)
        {
            ProbeError?.Invoke(this, $"Probe error: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Probe Y (find edge)
    /// </summary>
    public async Task<ProbeResult?> ProbeY(bool positive = true)
    {
        if (!_serial.IsConnected) return null;
        
        try
        {
            var direction = positive ? 1 : -1;
            ProbeStatus?.Invoke(this, $"Starting Y probe ({(positive ? "+" : "-")})...");
            
            var result = await ProbeAxis("Y", MaxProbeDistance * direction, ProbeFeedRate);
            if (result == null || !result.Success)
            {
                ProbeError?.Invoke(this, "Y probe failed - no contact");
                return null;
            }
            
            // Set Y zero (accounting for tool radius and probe offset)
            var offset = (ToolDiameter / 2 + ProbeOffset) * direction;
            _serial.SendCommand($"G10 L20 P1 Y{offset}");
            
            // Retract
            _serial.SendCommand($"G91 G0 Y{-ProbeRetract * direction}");
            _serial.SendCommand("G90");
            
            ProbeStatus?.Invoke(this, $"Y probe complete: {result.Position:F3}mm");
            ProbeCompleted?.Invoke(this, new ProbeResultEventArgs { Result = result });
            
            return result;
        }
        catch (Exception ex)
        {
            ProbeError?.Invoke(this, $"Probe error: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Find center of a hole (probe 4 directions)
    /// </summary>
    public async Task<(double X, double Y)?> FindHoleCenter()
    {
        if (!_serial.IsConnected) return null;
        
        try
        {
            ProbeStatus?.Invoke(this, "Finding hole center...");
            
            // Probe X+ and X-
            var xPos = await ProbeAxis("X", MaxProbeDistance, ProbeFeedRate);
            if (xPos == null) return null;
            _serial.SendCommand("G91 G0 X-" + (MaxProbeDistance / 2)); // Return to center
            await Task.Delay(300);
            
            var xNeg = await ProbeAxis("X", -MaxProbeDistance, ProbeFeedRate);
            if (xNeg == null) return null;
            
            double centerX = (xPos.Position + xNeg.Position) / 2;
            _serial.SendCommand($"G90 G0 X{centerX}");
            await Task.Delay(300);
            
            // Probe Y+ and Y-
            var yPos = await ProbeAxis("Y", MaxProbeDistance, ProbeFeedRate);
            if (yPos == null) return null;
            _serial.SendCommand("G91 G0 Y-" + (MaxProbeDistance / 2));
            await Task.Delay(300);
            
            var yNeg = await ProbeAxis("Y", -MaxProbeDistance, ProbeFeedRate);
            if (yNeg == null) return null;
            
            double centerY = (yPos.Position + yNeg.Position) / 2;
            
            // Move to center
            _serial.SendCommand($"G90 G0 X{centerX} Y{centerY}");
            
            // Set as zero
            _serial.SendCommand("G10 L20 P1 X0 Y0");
            
            ProbeStatus?.Invoke(this, $"Hole center found at X:{centerX:F3} Y:{centerY:F3}");
            
            return (centerX, centerY);
        }
        catch (Exception ex)
        {
            ProbeError?.Invoke(this, $"Hole center error: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Find corner (probe X and Y edges)
    /// </summary>
    public async Task<(double X, double Y)?> FindCorner(bool xPositive = true, bool yPositive = true)
    {
        if (!_serial.IsConnected) return null;
        
        try
        {
            ProbeStatus?.Invoke(this, "Finding corner...");
            
            // Probe X
            var xResult = await ProbeX(xPositive);
            if (xResult == null) return null;
            
            // Move back and down for Y probe
            var xDir = xPositive ? -1 : 1;
            _serial.SendCommand($"G91 G0 X{10 * xDir}");
            await Task.Delay(300);
            
            // Probe Y
            var yResult = await ProbeY(yPositive);
            if (yResult == null) return null;
            
            ProbeStatus?.Invoke(this, "Corner found and zeroed");
            
            return (0, 0); // Corner is now zero
        }
        catch (Exception ex)
        {
            ProbeError?.Invoke(this, $"Corner probe error: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Tool length probe (requires fixed probe)
    /// </summary>
    public async Task<double?> ProbeToolLength(double probeHeight)
    {
        if (!_serial.IsConnected) return null;
        
        try
        {
            ProbeStatus?.Invoke(this, "Probing tool length...");
            
            // Move above probe
            _serial.SendCommand("G90 G0 Z50"); // Safe height
            await Task.Delay(300);
            
            // Probe down
            var result = await ProbeAxis("Z", -60, ProbeFeedRate);
            if (result == null || !result.Success)
            {
                ProbeError?.Invoke(this, "Tool probe failed");
                return null;
            }
            
            // Calculate tool length offset
            double toolLength = probeHeight - result.MachinePosition;
            
            // Store tool length (assuming tool 1)
            _serial.SendCommand($"G43.1 Z{toolLength}");
            
            // Retract
            _serial.SendCommand("G91 G0 Z10");
            _serial.SendCommand("G90");
            
            ProbeStatus?.Invoke(this, $"Tool length: {toolLength:F3}mm");
            
            return toolLength;
        }
        catch (Exception ex)
        {
            ProbeError?.Invoke(this, $"Tool probe error: {ex.Message}");
            return null;
        }
    }
    
    private async Task<ProbeResult?> ProbeAxis(string axis, double distance, double feedRate)
    {
        // Send probe command (G38.2 = probe toward, stop on contact)
        var command = $"G38.2 {axis}{distance:F3} F{feedRate:F0}";
        var response = await _serial.SendCommandAsync(command, 30000);
        
        if (response == null) return null;
        
        // Parse response - GRBL returns PRB:x,y,z:1 for success
        if (response.Contains("PRB:"))
        {
            var prbStart = response.IndexOf("PRB:") + 4;
            var prbEnd = response.IndexOf(":", prbStart);
            var coords = response.Substring(prbStart, prbEnd - prbStart).Split(',');
            
            var success = response.EndsWith(":1");
            
            return new ProbeResult
            {
                Success = success,
                Axis = axis,
                Position = axis switch
                {
                    "X" => double.TryParse(coords[0], out var x) ? x : 0,
                    "Y" => double.TryParse(coords[1], out var y) ? y : 0,
                    "Z" => double.TryParse(coords[2], out var z) ? z : 0,
                    _ => 0
                },
                MachinePosition = axis switch
                {
                    "X" => double.TryParse(coords[0], out var x) ? x : 0,
                    "Y" => double.TryParse(coords[1], out var y) ? y : 0,
                    "Z" => double.TryParse(coords[2], out var z) ? z : 0,
                    _ => 0
                }
            };
        }
        
        // Check for ok (probe succeeded)
        if (response.Contains("ok"))
        {
            // Request status to get position
            _serial.RequestStatus();
            await Task.Delay(100);
            
            return new ProbeResult
            {
                Success = true,
                Axis = axis,
                Position = axis switch
                {
                    "X" => _serial.Status.WorkX,
                    "Y" => _serial.Status.WorkY,
                    "Z" => _serial.Status.WorkZ,
                    _ => 0
                },
                MachinePosition = axis switch
                {
                    "X" => _serial.Status.MachineX,
                    "Y" => _serial.Status.MachineY,
                    "Z" => _serial.Status.MachineZ,
                    _ => 0
                }
            };
        }
        
        return null;
    }
}

/// <summary>
/// Probe result
/// </summary>
public class ProbeResult
{
    public bool Success { get; set; }
    public string Axis { get; set; } = "";
    public double Position { get; set; }          // Work coordinates
    public double MachinePosition { get; set; }   // Machine coordinates
}

public class ProbeResultEventArgs : EventArgs
{
    public ProbeResult Result { get; set; } = new();
}
