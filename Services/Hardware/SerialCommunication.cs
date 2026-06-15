using System.IO.Ports;
using System.Text;

namespace E3Studio.Services.Hardware;

/// <summary>
/// Serial port communication for CNC machines
/// </summary>
public class SerialCommunication : IDisposable
{
    private SerialPort? _port;
    private readonly Queue<string> _commandQueue = new();
    private readonly Queue<string> _responseBuffer = new();
    private bool _isStreaming = false;
    private CancellationTokenSource? _streamCts;
    
    // Events
    public event EventHandler<string>? DataReceived;
    public event EventHandler<string>? CommandSent;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? ConnectionChanged;
    public event EventHandler<StreamingProgressEventArgs>? StreamingProgress;
    public event EventHandler<MachineStatus>? StatusChanged;
    
    // Status
    public bool IsConnected => _port?.IsOpen ?? false;
    public bool IsStreaming => _isStreaming;
    public string PortName => _port?.PortName ?? "";
    public int BaudRate => _port?.BaudRate ?? 0;
    
    // Machine status (for GRBL)
    public MachineStatus Status { get; private set; } = new();
    
    /// <summary>
    /// Get available serial ports
    /// </summary>
    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();
    
    /// <summary>
    /// Connect to serial port
    /// </summary>
    public bool Connect(string portName, int baudRate = 115200)
    {
        try
        {
            Disconnect();
            
            _port = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                DtrEnable = true,
                RtsEnable = true,
                NewLine = "\n"
            };
            
            _port.DataReceived += Port_DataReceived;
            _port.ErrorReceived += Port_ErrorReceived;
            _port.Open();
            
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Disconnect from serial port
    /// </summary>
    public void Disconnect()
    {
        StopStreaming();
        
        if (_port != null)
        {
            if (_port.IsOpen)
            {
                try { _port.Close(); } catch { }
            }
            _port.DataReceived -= Port_DataReceived;
            _port.ErrorReceived -= Port_ErrorReceived;
            _port.Dispose();
            _port = null;
            
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>
    /// Send a single command
    /// </summary>
    public bool SendCommand(string command)
    {
        if (!IsConnected || _port == null) return false;
        
        try
        {
            var cmd = command.Trim();
            if (!cmd.EndsWith('\n')) cmd += '\n';
            
            _port.Write(cmd);
            CommandSent?.Invoke(this, command.Trim());
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Send failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Send command and wait for response
    /// </summary>
    public async Task<string?> SendCommandAsync(string command, int timeoutMs = 5000)
    {
        if (!IsConnected || _port == null) return null;
        
        _responseBuffer.Clear();
        SendCommand(command);
        
        var startTime = DateTime.Now;
        while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
        {
            if (_responseBuffer.Count > 0)
            {
                return _responseBuffer.Dequeue();
            }
            await Task.Delay(10);
        }
        
        return null;
    }
    
    /// <summary>
    /// Stream G-Code file
    /// </summary>
    public async Task StreamGCode(string gcode, IProgress<StreamingProgressEventArgs>? progress = null)
    {
        if (!IsConnected) return;
        
        var lines = gcode.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith(';') && !l.StartsWith('('))
            .ToList();
        
        if (lines.Count == 0) return;
        
        _isStreaming = true;
        _streamCts = new CancellationTokenSource();
        
        int sentCount = 0;
        int totalLines = lines.Count;
        
        try
        {
            foreach (var line in lines)
            {
                if (_streamCts.Token.IsCancellationRequested) break;
                
                // Wait for OK from machine
                var response = await SendCommandAsync(line, 30000);
                
                sentCount++;
                var progressArgs = new StreamingProgressEventArgs
                {
                    CurrentLine = sentCount,
                    TotalLines = totalLines,
                    CurrentCommand = line,
                    Response = response ?? "",
                    Progress = (double)sentCount / totalLines * 100
                };
                
                progress?.Report(progressArgs);
                StreamingProgress?.Invoke(this, progressArgs);
                
                // Check for error
                if (response != null && response.Contains("error"))
                {
                    ErrorOccurred?.Invoke(this, $"Error at line {sentCount}: {response}");
                }
            }
        }
        finally
        {
            _isStreaming = false;
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }
    
    /// <summary>
    /// Stop streaming
    /// </summary>
    public void StopStreaming()
    {
        _streamCts?.Cancel();
        _isStreaming = false;
    }
    
    /// <summary>
    /// Send soft reset (GRBL)
    /// </summary>
    public void SoftReset()
    {
        if (_port?.IsOpen == true)
        {
            _port.Write(new byte[] { 0x18 }, 0, 1); // Ctrl+X
        }
    }
    
    /// <summary>
    /// Send feed hold (GRBL)
    /// </summary>
    public void FeedHold()
    {
        if (_port?.IsOpen == true)
        {
            _port.Write("!");
        }
    }
    
    /// <summary>
    /// Resume from feed hold (GRBL)
    /// </summary>
    public void CycleStart()
    {
        if (_port?.IsOpen == true)
        {
            _port.Write("~");
        }
    }
    
    /// <summary>
    /// Request status (GRBL)
    /// </summary>
    public void RequestStatus()
    {
        if (_port?.IsOpen == true)
        {
            _port.Write("?");
        }
    }
    
    private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port == null || !_port.IsOpen) return;
        
        try
        {
            var data = _port.ReadExisting();
            if (!string.IsNullOrEmpty(data))
            {
                // Parse status if it's a GRBL status response
                if (data.StartsWith("<") && data.Contains(">"))
                {
                    ParseGrblStatus(data);
                }
                
                _responseBuffer.Enqueue(data);
                DataReceived?.Invoke(this, data);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Read error: {ex.Message}");
        }
    }
    
    private void Port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke(this, $"Serial error: {e.EventType}");
    }
    
    private void ParseGrblStatus(string data)
    {
        // Parse GRBL status: <Idle|MPos:0.000,0.000,0.000|WPos:0.000,0.000,0.000>
        try
        {
            var inner = data.Trim('<', '>', '\r', '\n');
            var parts = inner.Split('|');
            
            if (parts.Length > 0)
            {
                Status.State = parts[0] switch
                {
                    "Idle" => MachineState.Idle,
                    "Run" => MachineState.Running,
                    "Hold" => MachineState.Hold,
                    "Jog" => MachineState.Jog,
                    "Alarm" => MachineState.Alarm,
                    "Door" => MachineState.Door,
                    "Check" => MachineState.Check,
                    "Home" => MachineState.Homing,
                    "Sleep" => MachineState.Sleep,
                    _ => MachineState.Unknown
                };
                
                foreach (var part in parts.Skip(1))
                {
                    if (part.StartsWith("MPos:"))
                    {
                        var coords = part.Substring(5).Split(',');
                        if (coords.Length >= 3)
                        {
                            Status.MachineX = double.TryParse(coords[0], out var x) ? x : 0;
                            Status.MachineY = double.TryParse(coords[1], out var y) ? y : 0;
                            Status.MachineZ = double.TryParse(coords[2], out var z) ? z : 0;
                        }
                    }
                    else if (part.StartsWith("WPos:"))
                    {
                        var coords = part.Substring(5).Split(',');
                        if (coords.Length >= 3)
                        {
                            Status.WorkX = double.TryParse(coords[0], out var x) ? x : 0;
                            Status.WorkY = double.TryParse(coords[1], out var y) ? y : 0;
                            Status.WorkZ = double.TryParse(coords[2], out var z) ? z : 0;
                        }
                    }
                    else if (part.StartsWith("FS:"))
                    {
                        var fs = part.Substring(3).Split(',');
                        if (fs.Length >= 2)
                        {
                            Status.FeedRate = double.TryParse(fs[0], out var f) ? f : 0;
                            Status.SpindleSpeed = double.TryParse(fs[1], out var s) ? s : 0;
                        }
                    }
                }
            }
        }
        catch { }
    }
    
    public void Dispose()
    {
        Disconnect();
    }
}

/// <summary>
/// Machine status
/// </summary>
public class MachineStatus
{
    public MachineState State { get; set; } = MachineState.Unknown;
    
    // Machine coordinates
    public double MachineX { get; set; }
    public double MachineY { get; set; }
    public double MachineZ { get; set; }
    
    // Work coordinates
    public double WorkX { get; set; }
    public double WorkY { get; set; }
    public double WorkZ { get; set; }
    
    // Current feed/speed
    public double FeedRate { get; set; }
    public double SpindleSpeed { get; set; }
    
    // Override percentages
    public int FeedOverride { get; set; } = 100;
    public int RapidOverride { get; set; } = 100;
    public int SpindleOverride { get; set; } = 100;
}

public enum MachineState
{
    Unknown,
    Idle,
    Running,
    Hold,
    Jog,
    Alarm,
    Door,
    Check,
    Homing,
    Sleep
}

/// <summary>
/// Streaming progress event args
/// </summary>
public class StreamingProgressEventArgs : EventArgs
{
    public int CurrentLine { get; set; }
    public int TotalLines { get; set; }
    public string CurrentCommand { get; set; } = "";
    public string Response { get; set; } = "";
    public double Progress { get; set; }
}
