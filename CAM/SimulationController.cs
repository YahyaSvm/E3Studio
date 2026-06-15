using System.Windows.Media.Media3D;
using System.Windows.Threading;
using E3Studio.Models;

namespace E3Studio.CAM;

/// <summary>
/// Controls toolpath simulation playback
/// </summary>
public class SimulationController
{
    private readonly DispatcherTimer _timer;
    private List<Toolpath>? _toolpaths;
    private int _currentToolpathIndex = 0;
    private int _currentMoveIndex = 0;
    private double _stockThickness;
    
    // Playback state
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public double Speed { get; set; } = 1.0; // 1.0 = normal, 2.0 = 2x, 0.5 = half
    
    // Current position
    public Point3D CurrentToolPosition { get; private set; }
    public double Progress { get; private set; } // 0.0 to 1.0
    public int TotalMoves { get; private set; }
    public int CurrentMoveNumber { get; private set; }
    
    // Events
    public event EventHandler<SimulationUpdateEventArgs>? PositionChanged;
    public event EventHandler? SimulationStarted;
    public event EventHandler? SimulationPaused;
    public event EventHandler? SimulationStopped;
    public event EventHandler? SimulationCompleted;
    
    public SimulationController()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _timer.Tick += Timer_Tick;
    }
    
    /// <summary>
    /// Load toolpaths for simulation
    /// </summary>
    public void LoadToolpaths(List<Toolpath> toolpaths, double stockThickness)
    {
        _toolpaths = toolpaths;
        _stockThickness = stockThickness;
        _currentToolpathIndex = 0;
        _currentMoveIndex = 0;
        Progress = 0;
        
        // Calculate total moves
        TotalMoves = toolpaths.Sum(t => t.Moves?.Count ?? 0);
        CurrentMoveNumber = 0;
        
        // Set initial position
        if (_toolpaths.Count > 0 && _toolpaths[0].Moves?.Count > 0)
        {
            var firstMove = _toolpaths[0].Moves[0];
            CurrentToolPosition = new Point3D(firstMove.X, firstMove.Y, stockThickness + firstMove.Z);
        }
    }
    
    /// <summary>
    /// Start simulation from beginning
    /// </summary>
    public void Play()
    {
        if (_toolpaths == null || _toolpaths.Count == 0) return;
        
        if (!IsPaused)
        {
            _currentToolpathIndex = 0;
            _currentMoveIndex = 0;
            CurrentMoveNumber = 0;
        }
        
        IsPlaying = true;
        IsPaused = false;
        _timer.Start();
        
        SimulationStarted?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Pause simulation
    /// </summary>
    public void Pause()
    {
        if (!IsPlaying) return;
        
        IsPaused = true;
        IsPlaying = false;
        _timer.Stop();
        
        SimulationPaused?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Stop and reset simulation
    /// </summary>
    public void Stop()
    {
        _timer.Stop();
        IsPlaying = false;
        IsPaused = false;
        _currentToolpathIndex = 0;
        _currentMoveIndex = 0;
        CurrentMoveNumber = 0;
        Progress = 0;
        
        // Reset to initial position
        if (_toolpaths?.Count > 0 && _toolpaths[0].Moves?.Count > 0)
        {
            var firstMove = _toolpaths[0].Moves[0];
            CurrentToolPosition = new Point3D(firstMove.X, firstMove.Y, _stockThickness + firstMove.Z);
        }
        
        SimulationStopped?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Step to next move
    /// </summary>
    public void StepForward()
    {
        if (_toolpaths == null) return;
        
        AdvanceMove();
        UpdatePosition();
    }
    
    /// <summary>
    /// Step to previous move
    /// </summary>
    public void StepBackward()
    {
        if (_toolpaths == null) return;
        
        if (_currentMoveIndex > 0)
        {
            _currentMoveIndex--;
            CurrentMoveNumber--;
        }
        else if (_currentToolpathIndex > 0)
        {
            _currentToolpathIndex--;
            var tp = _toolpaths[_currentToolpathIndex];
            _currentMoveIndex = (tp.Moves?.Count ?? 1) - 1;
            CurrentMoveNumber--;
        }
        
        UpdatePosition();
    }
    
    /// <summary>
    /// Jump to specific progress (0.0 to 1.0)
    /// </summary>
    public void SeekTo(double progress)
    {
        if (_toolpaths == null || TotalMoves == 0) return;
        
        progress = Math.Max(0, Math.Min(1, progress));
        var targetMove = (int)(progress * TotalMoves);
        
        // Find the toolpath and move index
        int moveCount = 0;
        for (int i = 0; i < _toolpaths.Count; i++)
        {
            var tp = _toolpaths[i];
            var tpMoves = tp.Moves?.Count ?? 0;
            
            if (moveCount + tpMoves > targetMove)
            {
                _currentToolpathIndex = i;
                _currentMoveIndex = targetMove - moveCount;
                break;
            }
            moveCount += tpMoves;
        }
        
        CurrentMoveNumber = targetMove;
        UpdatePosition();
    }
    
    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_toolpaths == null || !IsPlaying) return;
        
        // Advance based on speed (higher speed = skip more moves per tick)
        int movesPerTick = (int)Math.Max(1, Speed * 2);
        
        for (int i = 0; i < movesPerTick; i++)
        {
            if (!AdvanceMove())
            {
                // Simulation complete
                _timer.Stop();
                IsPlaying = false;
                Progress = 1.0;
                SimulationCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
        
        UpdatePosition();
    }
    
    private bool AdvanceMove()
    {
        if (_toolpaths == null) return false;
        
        var currentToolpath = _toolpaths[_currentToolpathIndex];
        var moves = currentToolpath.Moves;
        
        if (moves == null || _currentMoveIndex >= moves.Count - 1)
        {
            // Move to next toolpath
            if (_currentToolpathIndex < _toolpaths.Count - 1)
            {
                _currentToolpathIndex++;
                _currentMoveIndex = 0;
                CurrentMoveNumber++;
                return true;
            }
            return false; // No more moves
        }
        
        _currentMoveIndex++;
        CurrentMoveNumber++;
        return true;
    }
    
    private void UpdatePosition()
    {
        if (_toolpaths == null) return;
        
        var currentToolpath = _toolpaths[_currentToolpathIndex];
        var moves = currentToolpath.Moves;
        
        if (moves == null || _currentMoveIndex >= moves.Count) return;
        
        var move = moves[_currentMoveIndex];
        CurrentToolPosition = new Point3D(move.X, move.Y, _stockThickness + move.Z);
        
        // Calculate progress
        Progress = TotalMoves > 0 ? (double)CurrentMoveNumber / TotalMoves : 0;
        
        // Raise event
        PositionChanged?.Invoke(this, new SimulationUpdateEventArgs
        {
            Position = CurrentToolPosition,
            MoveType = move.Type,
            Progress = Progress,
            CurrentMove = CurrentMoveNumber,
            TotalMoves = TotalMoves,
            FeedRate = move.F,
            CurrentToolpath = currentToolpath
        });
    }
    
    /// <summary>
    /// Get all positions up to current point (for trail visualization)
    /// </summary>
    public List<Point3D> GetTrailPositions()
    {
        var positions = new List<Point3D>();
        
        if (_toolpaths == null) return positions;
        
        for (int i = 0; i <= _currentToolpathIndex && i < _toolpaths.Count; i++)
        {
            var tp = _toolpaths[i];
            if (tp.Moves == null) continue;
            
            int maxIndex = (i == _currentToolpathIndex) ? _currentMoveIndex : tp.Moves.Count - 1;
            
            for (int j = 0; j <= maxIndex && j < tp.Moves.Count; j++)
            {
                var move = tp.Moves[j];
                positions.Add(new Point3D(move.X, move.Y, _stockThickness + move.Z));
            }
        }
        
        return positions;
    }
}

/// <summary>
/// Event args for simulation position updates
/// </summary>
public class SimulationUpdateEventArgs : EventArgs
{
    public Point3D Position { get; set; }
    public MoveType MoveType { get; set; }
    public double Progress { get; set; }
    public int CurrentMove { get; set; }
    public int TotalMoves { get; set; }
    public double FeedRate { get; set; }
    public Toolpath? CurrentToolpath { get; set; }
}
