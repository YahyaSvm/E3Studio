using System.Windows.Media.Media3D;
using System.Windows.Threading;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Realistic G-Code simulation with proper feed rate timing
/// Animates tool movement based on actual machining speeds
/// </summary>
public class RealtimeSimulator
{
    private readonly DispatcherTimer _timer;
    private List<Toolpath>? _toolpaths;
    private double _stockThickness;
    
    // Current state
    private int _toolpathIndex = 0;
    private int _moveIndex = 0;
    private Point3D _currentPosition;
    private Point3D _targetPosition;
    private Point3D _startPosition;
    private double _moveProgress = 0; // 0 to 1 within current move
    private double _moveDistance = 0;
    private double _currentFeedRate = 0;
    private MoveType _currentMoveType = MoveType.Rapid;
    
    // Timing
    private DateTime _moveStartTime;
    private double _moveDuration; // seconds
    
    // Settings
    public double SpeedMultiplier { get; set; } = 1.0;
    public double RapidFeedRate { get; set; } = 5000; // mm/min for rapids
    public double MaxFeedRate { get; set; } = 2000; // mm/min cap
    
    // State
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsComplete { get; private set; }
    
    // Info
    public int TotalMoves { get; private set; }
    public int CurrentMoveNumber { get; private set; }
    public double TotalProgress => TotalMoves > 0 ? (double)CurrentMoveNumber / TotalMoves : 0;
    public Point3D ToolPosition => _currentPosition;
    public MoveType CurrentMoveType => _currentMoveType;
    public double CurrentFeedRate => _currentFeedRate;
    public string CurrentGCode { get; private set; } = "";
    
    // Estimated time
    public TimeSpan EstimatedTotalTime { get; private set; }
    public TimeSpan ElapsedTime { get; private set; }
    public TimeSpan RemainingTime => EstimatedTotalTime - ElapsedTime;
    
    // Events
    public event EventHandler<SimulatorUpdateEventArgs>? OnUpdate;
    public event EventHandler? OnComplete;
    public event EventHandler? OnStart;
    public event EventHandler? OnPause;
    public event EventHandler? OnStop;
    
    public RealtimeSimulator()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // 60 FPS
        };
        _timer.Tick += OnTimerTick;
    }
    
    /// <summary>
    /// Load toolpaths and calculate estimated time
    /// </summary>
    public void Load(List<Toolpath> toolpaths, double stockThickness)
    {
        _toolpaths = toolpaths;
        _stockThickness = stockThickness;
        
        Reset();
        CalculateEstimatedTime();
    }
    
    /// <summary>
    /// Calculate total machining time based on feed rates
    /// </summary>
    private void CalculateEstimatedTime()
    {
        if (_toolpaths == null) return;
        
        double totalSeconds = 0;
        TotalMoves = 0;
        
        foreach (var toolpath in _toolpaths)
        {
            if (toolpath.Moves == null || toolpath.Moves.Count < 2) continue;
            
            Point3D lastPos = new Point3D(toolpath.Moves[0].X, toolpath.Moves[0].Y, toolpath.Moves[0].Z);
            
            for (int i = 1; i < toolpath.Moves.Count; i++)
            {
                var move = toolpath.Moves[i];
                var pos = new Point3D(move.X, move.Y, move.Z);
                
                double distance = (pos - lastPos).Length;
                double feedRate = move.Type == MoveType.Rapid ? RapidFeedRate : Math.Min(move.F > 0 ? move.F : toolpath.FeedRate, MaxFeedRate);
                
                if (feedRate > 0 && distance > 0)
                {
                    totalSeconds += (distance / feedRate) * 60; // Convert mm/min to seconds
                }
                
                lastPos = pos;
                TotalMoves++;
            }
        }
        
        EstimatedTotalTime = TimeSpan.FromSeconds(totalSeconds);
    }
    
    /// <summary>
    /// Reset to beginning
    /// </summary>
    public void Reset()
    {
        _timer.Stop();
        IsPlaying = false;
        IsPaused = false;
        IsComplete = false;
        
        _toolpathIndex = 0;
        _moveIndex = 0;
        _moveProgress = 0;
        CurrentMoveNumber = 0;
        ElapsedTime = TimeSpan.Zero;
        
        // Set initial position
        if (_toolpaths?.Count > 0 && _toolpaths[0].Moves?.Count > 0)
        {
            var first = _toolpaths[0].Moves[0];
            _currentPosition = new Point3D(first.X, first.Y, _stockThickness + first.Z);
            _startPosition = _currentPosition;
            _targetPosition = _currentPosition;
        }
        
        CurrentGCode = "; Ready";
    }
    
    /// <summary>
    /// Start or resume playback
    /// </summary>
    public void Play()
    {
        if (_toolpaths == null || _toolpaths.Count == 0) return;
        
        if (IsComplete)
        {
            Reset();
        }
        
        if (!IsPaused)
        {
            SetupNextMove();
        }
        
        IsPlaying = true;
        IsPaused = false;
        _moveStartTime = DateTime.Now;
        _timer.Start();
        
        OnStart?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Pause playback
    /// </summary>
    public void Pause()
    {
        if (!IsPlaying) return;
        
        _timer.Stop();
        IsPlaying = false;
        IsPaused = true;
        
        OnPause?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Stop and reset
    /// </summary>
    public void Stop()
    {
        Reset();
        OnStop?.Invoke(this, EventArgs.Empty);
        
        // Send final update
        RaiseUpdate();
    }
    
    /// <summary>
    /// Step forward one move
    /// </summary>
    public void StepForward()
    {
        if (_toolpaths == null || IsComplete) return;
        
        // Complete current move instantly
        _currentPosition = _targetPosition;
        _moveProgress = 1.0;
        
        // Move to next
        if (!AdvanceToNextMove())
        {
            Complete();
        }
        else
        {
            SetupNextMove();
        }
        
        RaiseUpdate();
    }
    
    /// <summary>
    /// Step backward one move
    /// </summary>
    public void StepBackward()
    {
        if (_toolpaths == null || CurrentMoveNumber <= 0) return;
        
        // Go back one move
        if (_moveIndex > 1)
        {
            _moveIndex--;
            CurrentMoveNumber--;
        }
        else if (_toolpathIndex > 0)
        {
            _toolpathIndex--;
            var tp = _toolpaths[_toolpathIndex];
            _moveIndex = Math.Max(1, (tp.Moves?.Count ?? 1) - 1);
            CurrentMoveNumber--;
        }
        
        SetupCurrentMove();
        _currentPosition = _startPosition;
        _moveProgress = 0;
        
        RaiseUpdate();
    }
    
    /// <summary>
    /// Set playback speed (1.0 = realtime, 10 = 10x faster)
    /// </summary>
    public void SetSpeed(double multiplier)
    {
        SpeedMultiplier = Math.Max(0.1, Math.Min(100, multiplier));
    }
    
    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || _toolpaths == null) return;
        
        // Calculate elapsed time for current move
        var elapsed = (DateTime.Now - _moveStartTime).TotalSeconds * SpeedMultiplier;
        
        if (_moveDuration > 0)
        {
            _moveProgress = Math.Min(1.0, elapsed / _moveDuration);
        }
        else
        {
            _moveProgress = 1.0;
        }
        
        // Interpolate position
        _currentPosition = new Point3D(
            _startPosition.X + (_targetPosition.X - _startPosition.X) * _moveProgress,
            _startPosition.Y + (_targetPosition.Y - _startPosition.Y) * _moveProgress,
            _startPosition.Z + (_targetPosition.Z - _startPosition.Z) * _moveProgress
        );
        
        // Update elapsed time
        ElapsedTime += TimeSpan.FromMilliseconds(16 * SpeedMultiplier);
        
        // Check if move complete
        if (_moveProgress >= 1.0)
        {
            _currentPosition = _targetPosition;
            
            if (!AdvanceToNextMove())
            {
                Complete();
                return;
            }
            
            SetupNextMove();
        }
        
        RaiseUpdate();
    }
    
    private bool AdvanceToNextMove()
    {
        if (_toolpaths == null) return false;
        
        var toolpath = _toolpaths[_toolpathIndex];
        
        if (toolpath.Moves == null || _moveIndex >= toolpath.Moves.Count - 1)
        {
            // Move to next toolpath
            if (_toolpathIndex < _toolpaths.Count - 1)
            {
                _toolpathIndex++;
                _moveIndex = 0;
                return true;
            }
            return false; // All done
        }
        
        _moveIndex++;
        CurrentMoveNumber++;
        return true;
    }
    
    private void SetupNextMove()
    {
        SetupCurrentMove();
        _moveStartTime = DateTime.Now;
    }
    
    private void SetupCurrentMove()
    {
        if (_toolpaths == null) return;
        
        var toolpath = _toolpaths[_toolpathIndex];
        if (toolpath.Moves == null || _moveIndex >= toolpath.Moves.Count) return;
        
        // Get previous position
        if (_moveIndex > 0)
        {
            var prevMove = toolpath.Moves[_moveIndex - 1];
            _startPosition = new Point3D(prevMove.X, prevMove.Y, _stockThickness + prevMove.Z);
        }
        
        // Get target position
        var move = toolpath.Moves[_moveIndex];
        _targetPosition = new Point3D(move.X, move.Y, _stockThickness + move.Z);
        
        // Calculate move parameters
        _currentMoveType = move.Type;
        _moveDistance = (_targetPosition - _startPosition).Length;
        
        // Get feed rate
        if (move.Type == MoveType.Rapid)
        {
            _currentFeedRate = RapidFeedRate;
            CurrentGCode = $"G0 X{move.X:F3} Y{move.Y:F3} Z{move.Z:F3}";
        }
        else
        {
            _currentFeedRate = move.F > 0 ? move.F : toolpath.FeedRate;
            _currentFeedRate = Math.Min(_currentFeedRate, MaxFeedRate);
            
            string gcode = move.Type == MoveType.ArcCW ? "G2" : move.Type == MoveType.ArcCCW ? "G3" : "G1";
            CurrentGCode = $"{gcode} X{move.X:F3} Y{move.Y:F3} Z{move.Z:F3} F{_currentFeedRate:F0}";
        }
        
        // Calculate duration (distance / speed in mm/min * 60 = seconds)
        if (_currentFeedRate > 0 && _moveDistance > 0)
        {
            _moveDuration = (_moveDistance / _currentFeedRate) * 60;
        }
        else
        {
            _moveDuration = 0.01; // Minimum duration
        }
        
        _moveProgress = 0;
    }
    
    private void Complete()
    {
        _timer.Stop();
        IsPlaying = false;
        IsComplete = true;
        CurrentGCode = "; Program End";
        
        OnComplete?.Invoke(this, EventArgs.Empty);
        RaiseUpdate();
    }
    
    private void RaiseUpdate()
    {
        OnUpdate?.Invoke(this, new SimulatorUpdateEventArgs
        {
            Position = _currentPosition,
            MoveType = _currentMoveType,
            FeedRate = _currentFeedRate,
            GCode = CurrentGCode,
            Progress = TotalProgress,
            CurrentMove = CurrentMoveNumber,
            TotalMoves = TotalMoves,
            ElapsedTime = ElapsedTime,
            RemainingTime = RemainingTime,
            IsComplete = IsComplete
        });
    }
}

/// <summary>
/// Event args for simulator updates
/// </summary>
public class SimulatorUpdateEventArgs : EventArgs
{
    public Point3D Position { get; set; }
    public MoveType MoveType { get; set; }
    public double FeedRate { get; set; }
    public string GCode { get; set; } = "";
    public double Progress { get; set; }
    public int CurrentMove { get; set; }
    public int TotalMoves { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public bool IsComplete { get; set; }
}
