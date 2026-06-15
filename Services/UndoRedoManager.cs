using System.Text.Json;
using E3Studio.Models;

namespace E3Studio.Services;

/// <summary>
/// Undo/Redo manager for E3Studio
/// Supports geometry operations, transforms, and toolpath changes
/// </summary>
public class UndoRedoManager
{
    private readonly Stack<UndoAction> _undoStack = new();
    private readonly Stack<UndoAction> _redoStack = new();
    private const int MaxHistorySize = 100;
    
    public event EventHandler? StateChanged;
    
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    
    public string UndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : "";
    public string RedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : "";
    
    /// <summary>
    /// Record an action for undo
    /// </summary>
    public void RecordAction(string description, Action undoAction, Action redoAction)
    {
        var action = new UndoAction
        {
            Description = description,
            Undo = undoAction,
            Redo = redoAction,
            Timestamp = DateTime.Now
        };
        
        _undoStack.Push(action);
        _redoStack.Clear(); // Clear redo stack on new action
        
        // Limit history size
        if (_undoStack.Count > MaxHistorySize)
        {
            var tempList = _undoStack.ToList();
            tempList.RemoveAt(tempList.Count - 1);
            _undoStack.Clear();
            foreach (var item in tempList.AsEnumerable().Reverse())
                _undoStack.Push(item);
        }
        
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Record a geometry transform action
    /// </summary>
    public void RecordTransform(GeometryPath path, double oldX, double oldY, double oldRotation, 
        double oldScaleX, double oldScaleY, double newX, double newY, double newRotation,
        double newScaleX, double newScaleY, string description = "Transform")
    {
        var capturedPath = path;
        RecordAction(description,
            () => {
                capturedPath.X = oldX;
                capturedPath.Y = oldY;
                capturedPath.Rotation = oldRotation;
                capturedPath.ScaleX = oldScaleX;
                capturedPath.ScaleY = oldScaleY;
            },
            () => {
                capturedPath.X = newX;
                capturedPath.Y = newY;
                capturedPath.Rotation = newRotation;
                capturedPath.ScaleX = newScaleX;
                capturedPath.ScaleY = newScaleY;
            }
        );
    }
    
    /// <summary>
    /// Record adding a path
    /// </summary>
    public void RecordAddPath(GeometryPath path, ProjectNode parent, int index)
    {
        var capturedPath = path;
        var capturedParent = parent;
        var capturedIndex = index;
        
        // Find the path node in parent's children
        var pathNode = capturedParent.Children.FirstOrDefault(c => 
            c is GeometryNode gn && gn.PathData == capturedPath);
        
        RecordAction("Add Path",
            () => {
                if (pathNode != null)
                    capturedParent.Children.Remove(pathNode);
            },
            () => {
                if (pathNode != null)
                    capturedParent.Children.Insert(Math.Min(capturedIndex, capturedParent.Children.Count), pathNode);
            }
        );
    }
    
    /// <summary>
    /// Record removing a path
    /// </summary>
    public void RecordRemovePath(GeometryPath path, ProjectNode parent, int index)
    {
        var capturedPath = path;
        var capturedParent = parent;
        var capturedIndex = index;
        
        // Find the path node
        var pathNode = capturedParent.Children.FirstOrDefault(c => 
            c is GeometryNode gn && gn.PathData == capturedPath);
        
        RecordAction($"Delete Path",
            () => {
                if (pathNode != null)
                    capturedParent.Children.Insert(Math.Min(capturedIndex, capturedParent.Children.Count), pathNode);
            },
            () => {
                if (pathNode != null)
                    capturedParent.Children.Remove(pathNode);
            }
        );
    }
    
    /// <summary>
    /// Record multiple path deletions as a single action
    /// </summary>
    public void RecordMultiDelete(List<(GeometryPath path, ProjectNode parent, int index)> deletions)
    {
        // Store actual nodes for undo
        var capturedDeletions = new List<(ProjectNode node, ProjectNode parent, int index)>();
        
        foreach (var (path, parent, index) in deletions)
        {
            var pathNode = parent.Children.FirstOrDefault(c => 
                c is GeometryNode gn && gn.PathData == path);
            if (pathNode != null)
            {
                capturedDeletions.Add((pathNode, parent, index));
            }
        }
        
        RecordAction($"Delete {capturedDeletions.Count} paths",
            () => {
                // Restore in reverse order
                foreach (var (node, parent, index) in capturedDeletions.AsEnumerable().Reverse())
                {
                    parent.Children.Insert(Math.Min(index, parent.Children.Count), node);
                }
            },
            () => {
                foreach (var (node, parent, _) in capturedDeletions)
                {
                    parent.Children.Remove(node);
                }
            }
        );
    }
    
    /// <summary>
    /// Perform undo
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;
        
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Perform redo
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;
        
        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);
        
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Clear all history
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Get undo history
    /// </summary>
    public IEnumerable<string> GetUndoHistory()
    {
        return _undoStack.Select(a => a.Description);
    }
    
    /// <summary>
    /// Get redo history
    /// </summary>
    public IEnumerable<string> GetRedoHistory()
    {
        return _redoStack.Select(a => a.Description);
    }
}

/// <summary>
/// Represents an undoable action
/// </summary>
public class UndoAction
{
    public string Description { get; set; } = "";
    public Action Undo { get; set; } = () => { };
    public Action Redo { get; set; } = () => { };
    public DateTime Timestamp { get; set; }
}
