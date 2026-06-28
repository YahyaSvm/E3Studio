using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using E3Studio.Models;
using E3Studio.Dialogs;
using E3Studio.CAM;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using E3Studio.Services;
using System.IO;
using HelixToolkit.Wpf;

namespace E3Studio;

/// <summary>
/// Simple RelayCommand for MVVM pattern
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }
    
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}

/// <summary>
/// Main application window for E3Studio CAM software
/// </summary>
public partial class MainWindow : Window
{
    // Current project
    private Project? _currentProject;
    private string? _currentProjectPath;
    private bool _isDirty = false;
    private DispatcherTimer? _autoSaveTimer;
    private DateTime _lastSaveTime = DateTime.Now;
    
    // Toolpaths
    private List<Toolpath> _toolpaths = new();
    
    // Simulation - Realtime G-Code player
    private RealtimeSimulator? _simulator;
    
    // Stock removal simulation
    private StockRemovalSimulator? _stockRemoval;
    
    // Undo/Redo Manager
    private readonly UndoRedoManager _undoManager = new();
    
    // Clipboard Manager
    private readonly ClipboardManager _clipboard = ClipboardManager.Instance;
    
    // Last generated G-Code (for export)
    private string _lastGeneratedGCode = "";
    
    // Active cutting tool (set via Quick Tool Combo in toolbar)
    private Tool? _activeCamTool;
    
    // 3D STL Models
    private List<StlModel3D> _stlModels = new();
    private StlModel3D? _selectedStlModel;
    private ModelVisual3D? _stlModelVisual;
    
    // 3D viewport interaction
    private bool _isDragging3D = false;
    private Point3D _dragStart3D;
    
    // Canvas state
    private Point _panStart;
    private bool _isPanning = false;
    private double _zoom = 1.0;
    private Point _panOffset = new Point(0, 0);
    private double _canvasOffsetX = 0;
    private double _canvasOffsetY = 0;
    
    // Commands for keyboard shortcuts
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand CutCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand SelectAllCommand { get; }
    
    public MainWindow()
    {
        // Initialize commands BEFORE InitializeComponent
        UndoCommand = new RelayCommand(_ => PerformUndo());
        RedoCommand = new RelayCommand(_ => PerformRedo());
        DeleteCommand = new RelayCommand(_ => DeleteSelectedPaths());
        CopyCommand = new RelayCommand(_ => Copy_Click(this, new RoutedEventArgs()));
        CutCommand = new RelayCommand(_ => Cut_Click(this, new RoutedEventArgs()));
        PasteCommand = new RelayCommand(_ => Paste_Click(this, new RoutedEventArgs()));
        DuplicateCommand = new RelayCommand(_ => DuplicateSelection());
        SelectAllCommand = new RelayCommand(_ => SelectAllPaths());
        
        InitializeComponent();
        DataContext = this;
        Loaded += MainWindow_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Closing += MainWindow_Closing;
        
        // Initialize autosave timer
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(SettingsManager.Current.AutoSaveInterval)
        };
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        _autoSaveTimer.Start();
    }
    
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDirty && _currentProject != null)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                SaveProject_Click(this, new RoutedEventArgs());
            }
            else if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
        }
        
        _autoSaveTimer?.Stop();
    }
    
    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        if (_isDirty && _currentProject != null && !string.IsNullOrEmpty(_currentProjectPath))
        {
            try
            {
                ProjectSerializer.Save(_currentProject, _currentProjectPath);
                _isDirty = false;
                _lastSaveTime = DateTime.Now;
                LogOutput($"Auto-saved: {System.IO.Path.GetFileName(_currentProjectPath)}");
            }
            catch (Exception ex)
            {
                LogOutput($"Auto-save failed: {ex.Message}");
            }
        }
    }
    
    private void MarkDirty()
    {
        _isDirty = true;
        if (_currentProject != null)
        {
            TitleProjectName.Text = _currentProject.Root.Name + " •";
        }
    }
    
    private void MarkClean()
    {
        _isDirty = false;
        if (_currentProject != null)
        {
            TitleProjectName.Text = _currentProject.Root.Name;
        }
    }
    
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Keyboard shortcuts - using PreviewKeyDown to catch before TextBox handles
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewProject_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenProject_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.S:
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                        SaveProjectAs_Click(this, new RoutedEventArgs());
                    else
                        SaveProject_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.A:
                    // Select all visible paths
                    SelectAllPaths();
                    e.Handled = true;
                    break;
                case Key.G:
                    GenerateGCode_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.C:
                    Copy_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.X:
                    Cut_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.V:
                    Paste_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.D:
                    DuplicateSelection();
                    e.Handled = true;
                    break;
                case Key.Z:
                    // Undo
                    PerformUndo();
                    e.Handled = true;
                    break;
                case Key.Y:
                    // Redo
                    PerformRedo();
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            switch (e.Key)
            {
                case Key.Delete:
                    DeleteSelectedPaths();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    SelectPath(null);
                    e.Handled = true;
                    break;
                case Key.S:
                    // Select tool
                    _activeTool = TransformTool.Select;
                    StatusText.Text = "Tool: Select";
                    break;
                case Key.M:
                    // Move tool
                    _activeTool = TransformTool.Move;
                    StatusText.Text = "Tool: Move";
                    break;
                case Key.R:
                    // Rotate tool
                    _activeTool = TransformTool.Rotate;
                    StatusText.Text = "Tool: Rotate";
                    break;
                case Key.K:
                    // Scale tool
                    _activeTool = TransformTool.Scale;
                    StatusText.Text = "Tool: Scale";
                    break;
                case Key.F:
                    // Fit view
                    AutoFitView();
                    e.Handled = true;
                    break;
            }
        }
    }
    
    private void SelectAllPaths()
    {
        if (_currentProject == null) return;
        
        _multiSelection.Clear();
        var allPaths = GetAllVisiblePaths(_currentProject.Root);
        _multiSelection.AddRange(allPaths);
        
        foreach (var p in _multiSelection)
            p.IsSelected = true;
        
        if (_multiSelection.Count > 0)
        {
            _selectedPath = _multiSelection[0];
            UpdatePropertiesPanel();
            if (TransformProperties != null) TransformProperties.Visibility = Visibility.Visible;
            if (NoSelectionProperties != null) NoSelectionProperties.Visibility = Visibility.Collapsed;
        }
        
        DrawCanvas();
        StatusText.Text = $"Selected {_multiSelection.Count} paths";
    }
    
    private void DeleteSelectedPaths()
    {
        if (_currentProject == null) 
        {
            return;
        }
        
        if (_multiSelection.Count == 0) 
        {
            return;
        }
        
        // Collect nodes BEFORE deleting (for proper undo)
        var nodesToDelete = new List<(ProjectNode node, ProjectNode parent, int index)>();
        
        foreach (var path in _multiSelection.ToList())
        {
            var node = FindNodeForPath(_currentProject.Root, path);
            if (node != null)
            {
                var parent = FindParent(_currentProject.Root, node);
                if (parent != null)
                {
                    int index = parent.Children.IndexOf(node);
                    nodesToDelete.Add((node, parent, index));
                }
            }
        }
        
        if (nodesToDelete.Count == 0) 
        {
            return;
        }
        
        // Record undo action BEFORE deleting
        var captured = nodesToDelete.ToList();
        _undoManager.RecordAction($"Delete {captured.Count} paths",
            () => {
                // UNDO: Restore nodes in reverse order
                foreach (var (node, parent, index) in captured.AsEnumerable().Reverse())
                {
                    if (!parent.Children.Contains(node))
                    {
                        parent.Children.Insert(Math.Min(index, parent.Children.Count), node);
                    }
                }
                // Refresh tree view
                if (_currentProject != null)
                {
                    ProjectTree.ItemsSource = null;
                    ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
                }
                DrawCanvas();
            },
            () => {
                // REDO: Remove nodes again
                foreach (var (node, parent, _) in captured)
                {
                    parent.Children.Remove(node);
                }
                // Refresh tree view
                if (_currentProject != null)
                {
                    ProjectTree.ItemsSource = null;
                    ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
                }
                DrawCanvas();
            }
        );
        
        // NOW delete the nodes
        foreach (var (node, parent, _) in nodesToDelete)
        {
            parent.Children.Remove(node);
        }
        
        _multiSelection.Clear();
        _selectedPath = null;
        
        if (TransformProperties != null) TransformProperties.Visibility = Visibility.Collapsed;
        if (NoSelectionProperties != null) NoSelectionProperties.Visibility = Visibility.Visible;
        
        // Refresh tree view after deletion
        if (_currentProject != null)
        {
            ProjectTree.ItemsSource = null;
            ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
        }
        
        DrawCanvas();
        StatusText.Text = $"Deleted {nodesToDelete.Count} paths - Press Ctrl+Z to undo";
    }
    
    private void PerformUndo()
    {
        if (_undoManager.CanUndo)
        {
            _undoManager.Undo();
            
            // Refresh selection state
            _multiSelection.Clear();
            _selectedPath = null;
            
            // Refresh tree view
            if (_currentProject != null)
            {
                ProjectTree.ItemsSource = null;
                ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
            }
            
            DrawCanvas();
            StatusText.Text = _undoManager.CanUndo ? $"Undone (more: Ctrl+Z)" : "Nothing to undo";
        }
        else
        {
            StatusText.Text = "Nothing to undo";
        }
    }
    
    private void PerformRedo()
    {
        if (_undoManager.CanRedo)
        {
            _undoManager.Redo();
            
            // Refresh selection state
            _multiSelection.Clear();
            _selectedPath = null;
            
            // Refresh tree view
            if (_currentProject != null)
            {
                ProjectTree.ItemsSource = null;
                ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
            }
            
            DrawCanvas();
            StatusText.Text = _undoManager.CanRedo ? $"Redone (more: Ctrl+Y)" : "Nothing to redo";
        }
        else
        {
            StatusText.Text = "Nothing to redo";
        }
    }
    
    #region Clipboard Operations
    
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_multiSelection.Count == 0)
        {
            StatusText.Text = "Nothing selected to copy";
            return;
        }
        
        _clipboard.Copy(_multiSelection);
        StatusText.Text = $"Copied {_multiSelection.Count} path(s)";
    }
    
    private void Cut_Click(object sender, RoutedEventArgs e)
    {
        if (_multiSelection.Count == 0)
        {
            StatusText.Text = "Nothing selected to cut";
            return;
        }
        
        _clipboard.Cut(_multiSelection);
        var count = _multiSelection.Count;
        
        // Delete the selected paths
        DeleteSelectedPaths();
        
        StatusText.Text = $"Cut {count} path(s) - Press Ctrl+V to paste";
    }
    
    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (!_clipboard.HasData)
        {
            StatusText.Text = "Nothing to paste";
            return;
        }
        
        if (_currentProject == null) return;
        
        var pastedPaths = _clipboard.Paste();
        if (pastedPaths.Count == 0) return;
        
        // Find target layer (use first selected path's layer or default)
        LayerNode? targetLayer = null;
        if (_multiSelection.Count > 0)
        {
            targetLayer = FindLayerForPath(_currentProject.Root, _multiSelection.First());
        }
        targetLayer ??= FindOrCreateDefaultLayer(_currentProject.Root);
        
        if (targetLayer == null) return;
        
        // Add pasted paths to layer
        foreach (var path in pastedPaths)
        {
            if (path is PolyPath poly)
            {
                var node = new GeometryNode { Name = path.Name, PathData = poly };
                targetLayer.Children.Add(node);
            }
        }
        
        // Record undo
        var pastedNodes = targetLayer.Children.TakeLast(pastedPaths.Count).ToList();
        _undoManager.RecordAction($"Paste {pastedPaths.Count} path(s)",
            () => {
                foreach (var node in pastedNodes)
                    targetLayer.Children.Remove(node);
                DrawCanvas();
            },
            () => {
                foreach (var node in pastedNodes)
                    targetLayer.Children.Add(node);
                DrawCanvas();
            }
        );
        
        // Refresh
        ProjectTree.ItemsSource = null;
        ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
        
        // Select pasted items
        _multiSelection.Clear();
        foreach (var path in pastedPaths)
        {
            _multiSelection.Add(path);
        }
        _selectedPath = _multiSelection.FirstOrDefault();
        
        DrawCanvas();
        StatusText.Text = $"Pasted {pastedPaths.Count} path(s)";
    }
    
    private void DuplicateSelection()
    {
        if (_multiSelection.Count == 0)
        {
            StatusText.Text = "Nothing selected to duplicate";
            return;
        }
        
        _clipboard.Copy(_multiSelection);
        Paste_Click(this, new RoutedEventArgs());
    }
    
    private void CopyNode_Click(object sender, RoutedEventArgs e)
    {
        Copy_Click(sender, e);
    }
    
    private void CutNode_Click(object sender, RoutedEventArgs e)
    {
        Cut_Click(sender, e);
    }
    
    private void DuplicateNode_Click(object sender, RoutedEventArgs e)
    {
        DuplicateSelection();
    }
    
    private LayerNode? FindLayerForPath(ProjectNode root, GeometryPath path)
    {
        foreach (var child in root.Children)
        {
            if (child is LayerNode layer)
            {
                foreach (var geom in layer.Children)
                {
                    if (geom is GeometryNode gn && gn.PathData == path)
                        return layer;
                }
            }
            else if (child is WCSNode wcs)
            {
                var result = FindLayerForPath(wcs, path);
                if (result != null) return result;
            }
            else if (child is FolderNode folder)
            {
                var result = FindLayerForPath(folder, path);
                if (result != null) return result;
            }
        }
        return null;
    }
    
    private LayerNode? FindOrCreateDefaultLayer(ProjectNode root)
    {
        // Try to find existing layer
        var layer = FindFirstLayer(root);
        if (layer != null) return layer;
        
        // Find or create WCS
        var wcs = root.Children.OfType<WCSNode>().FirstOrDefault();
        if (wcs == null)
        {
            wcs = new WCSNode { Name = "WCS 1" };
            root.Children.Add(wcs);
        }
        
        // Create default layer
        var newLayer = new LayerNode { Name = "Default Layer" };
        wcs.Children.Add(newLayer);
        return newLayer;
    }
    
    private LayerNode? FindFirstLayer(ProjectNode node)
    {
        if (node is LayerNode layer) return layer;
        
        foreach (var child in node.Children)
        {
            var found = FindFirstLayer(child);
            if (found != null) return found;
        }
        return null;
    }
    
    #endregion
    
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        PerformUndo();
    }
    
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        PerformRedo();
    }
    
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Create initial project
        _currentProject = new Project();
        _currentProject.Root.Name = "New Project";
        TitleProjectName.Text = _currentProject.Root.Name;
        
        // Bind TreeView
        ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
        
        // Initialize canvas
        DrawCanvas();
        UpdateStatusBar();
        
        // Apply saved settings
        ApplySettings();
        
        // Initialize simulation controller
        // Simulator is initialized on demand when toolpaths are ready
        
        // Show welcome message with machine
        var settings = SettingsManager.Current;
        if (settings.SelectedMachine != null)
        {
            StatusText.Text = $"Ready — Machine: {settings.SelectedMachine.Name} ({settings.SelectedMachine.MaxX}x{settings.SelectedMachine.MaxY}x{settings.SelectedMachine.MaxZ}mm)";
        }
        
        // Populate recent projects in welcome screen
        PopulateWelcomeRecent();
        
        // Hide welcome screen since project is already created
        HideWelcomeScreen();
    }
    
    private void SimulationController_PositionChanged_Legacy(object? sender, SimulationUpdateEventArgs e)
    {
        // LEGACY - Not used anymore, replaced by Simulator_OnUpdate
    }
    
    private void SimulationController_Completed_Legacy(object? sender, EventArgs e)
    {
        // LEGACY - Not used anymore
    }
    
    #region Welcome Screen
    
    private void HideWelcomeScreen()
    {
        if (WelcomeScreen != null)
            WelcomeScreen.Visibility = Visibility.Collapsed;
    }
    
    private void PopulateWelcomeRecent()
    {
        var recent = SettingsManager.GetValidRecentProjects();
        WelcomeRecentList.Children.Clear();
        
        if (recent.Count == 0)
        {
            WelcomeRecentBorder.Visibility = Visibility.Collapsed;
            WelcomeNoRecent.Visibility = Visibility.Visible;
            return;
        }
        
        WelcomeRecentBorder.Visibility = Visibility.Visible;
        WelcomeNoRecent.Visibility = Visibility.Collapsed;
        
        foreach (var path in recent.Take(6))
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            var dir = System.IO.Path.GetDirectoryName(path) ?? "";
            
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = fileName,
                Foreground = (Brush)FindResource("TextBrush"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });
            sp.Children.Add(new TextBlock
            {
                Text = dir,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                FontSize = 10
            });
            
            var btn = new Button
            {
                Style = (Style)FindResource("BtnGhost"),
                Content = sp,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 1, 0, 1),
                Tag = path
            };
            btn.Click += (s, _) =>
            {
                var p = (string)((Button)s!).Tag;
                HideWelcomeScreen();
                OpenProjectFromPath(p);
            };
            
            WelcomeRecentList.Children.Add(btn);
        }
    }
    
    private void WelcomeNewProject_Click(object sender, RoutedEventArgs e)
    {
        HideWelcomeScreen();
        NewProject_Click(sender, e);
    }
    
    private void WelcomeOpenProject_Click(object sender, RoutedEventArgs e)
    {
        HideWelcomeScreen();
        OpenProject_Click(sender, e);
    }
    
    #endregion
    
    #region Window Chrome
    
    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void RailHome_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Home";
    }
    
    private void RailDesign_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Design Mode";
    }
    
    private void RailCAM_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "CAM Mode";
    }
    
    #endregion
    
    
    #region View Toggle
    
    // Tools
    private enum TransformTool { Select, Move, Rotate, Scale }
    private TransformTool _activeTool = TransformTool.Select;
    
    // Gizmo state
    private enum GizmoHandle { None, Move, Rotate, ScaleNW, ScaleN, ScaleNE, ScaleE, ScaleSE, ScaleS, ScaleSW, ScaleW }
    private GizmoHandle _activeGizmoHandle = GizmoHandle.None;
    private Rect _selectionWorldBounds;
    private double _gizmoHandleSize = 8;

    
    // Selection & Interaction
    private GeometryPath? _selectedPath; // Primary selection (for properties)
    private List<GeometryPath> _multiSelection = new();
    private Point _lastMousePos;
    private bool _isDraggingPath = false;
    
    // Box Selection
    private bool _isBoxSelecting = false;
    private Point _boxSelectStart;
    private Point _boxSelectEnd;
    
    // Transform undo tracking - stores initial values when transform starts
    private List<(GeometryPath path, double X, double Y, double Rotation, double ScaleX, double ScaleY)> _transformStartState = new();

    
    private void View2D_Checked(object sender, RoutedEventArgs e)
    {
        if (View2D != null && View3D != null)
        {
            View2D.Visibility = Visibility.Visible;
            View3D.Visibility = Visibility.Collapsed;
            DrawCanvas();
        }
    }
    
    private void View3D_Checked(object sender, RoutedEventArgs e)
    {
        if (View2D != null && View3D != null)
        {
            View2D.Visibility = Visibility.Collapsed;
            View3D.Visibility = Visibility.Visible;
            Update3DView();
        }
    }
    
    private void Update3DView()
    {
        if (_currentProject != null && StockBox3D != null)
        {
            var stock = _currentProject.Stock;
            StockBox3D.Width = stock.Width;
            StockBox3D.Length = stock.Height;
            StockBox3D.Height = stock.Thickness;
            StockBox3D.Center = new System.Windows.Media.Media3D.Point3D(
                stock.Width / 2, 
                stock.Height / 2, 
                stock.Thickness / 2);
        }
        
        Update3DStlModels();
    }
    
    #endregion
    
    #region Project Management
    
    
    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        // Show Stock Setup Dialog for new project
        var stockDialog = new StockSetupDialog();
        stockDialog.Owner = this;
        
        if (stockDialog.ShowDialog() == true)
        {
            HideWelcomeScreen();
            _currentProject = new Project();
            _currentProject.Root.Name = "Untitled";
            _currentProject.Stock.Width = stockDialog.Stock.Width;
            _currentProject.Stock.Height = stockDialog.Stock.Height;
            _currentProject.Stock.Thickness = stockDialog.Stock.Thickness;
            _currentProject.Stock.ZeroPoint = stockDialog.Stock.ZeroPoint;
            _currentProject.Stock.Material = stockDialog.SelectedMaterial;
            
            _currentProjectPath = null;
            _toolpaths.Clear();
            
            TitleProjectName.Text = _currentProject.Root.Name;
            ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
            
            DrawCanvas();
            Update3DView();
            
            LogOutput("═══════════════════════════════════════");
            LogOutput($"New project created");
            LogOutput($"  Stock: {stockDialog.Stock.Width} x {stockDialog.Stock.Height} x {stockDialog.Stock.Thickness} mm");
            LogOutput($"  Material: {stockDialog.SelectedMaterial?.Name ?? "Default"}");
            LogOutput($"  Zero Point: {stockDialog.Stock.ZeroPoint}");
            LogOutput("═══════════════════════════════════════");
            LogOutput("Next: Import geometry (DXF/SVG) or 3D model (STL)");
            
            StatusText.Text = $"New project created - Stock: {stockDialog.Stock.Width}x{stockDialog.Stock.Height}x{stockDialog.Stock.Thickness}mm";
        }
    }
    
    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "E3Studio Projects (*.e3p)|*.e3p|All Files (*.*)|*.*",
            Title = "Open Project"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                _currentProject = ProjectSerializer.Load(dialog.FileName);
                _currentProjectPath = dialog.FileName;
                _toolpaths.Clear();
                
                TitleProjectName.Text = _currentProject.Root.Name;
                ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
                
                // Add to recent projects
                SettingsManager.AddRecentProject(dialog.FileName);
                
                DrawCanvas();
                Update3DView();
                
                LogOutput($"Project opened: {System.IO.Path.GetFileName(dialog.FileName)}");
                LogOutput($"  Stock: {_currentProject.Stock.Width} x {_currentProject.Stock.Height} x {_currentProject.Stock.Thickness} mm");
                
                StatusText.Text = $"Opened: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;
        
        if (string.IsNullOrEmpty(_currentProjectPath))
        {
            SaveProjectAs_Click(sender, e);
            return;
        }
        
        try
        {
            ProjectSerializer.Save(_currentProject, _currentProjectPath);
            SettingsManager.AddRecentProject(_currentProjectPath);
            MarkClean();
            LogOutput($"Project saved: {System.IO.Path.GetFileName(_currentProjectPath)}");
            StatusText.Text = $"Saved: {System.IO.Path.GetFileName(_currentProjectPath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void SaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;
        
        var dialog = new SaveFileDialog
        {
            Filter = "E3Studio Projects (*.e3p)|*.e3p",
            Title = "Save Project As",
            FileName = _currentProject.Root.Name
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                _currentProjectPath = dialog.FileName;
                _currentProject.Root.Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                TitleProjectName.Text = _currentProject.Root.Name;
                
                ProjectSerializer.Save(_currentProject, _currentProjectPath);
                SettingsManager.AddRecentProject(_currentProjectPath);
                StatusText.Text = $"Saved: {System.IO.Path.GetFileName(_currentProjectPath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void RecentProjects_Click(object sender, RoutedEventArgs e)
    {
        var recentProjects = SettingsManager.GetValidRecentProjects();
        
        if (recentProjects.Count == 0)
        {
            MessageBox.Show("No recent projects found.", "Recent Projects", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        // Create context menu for recent projects
        var menu = new ContextMenu();
        
        foreach (var projectPath in recentProjects.Take(10))
        {
            var fileName = System.IO.Path.GetFileName(projectPath);
            var menuItem = new MenuItem
            {
                Header = fileName,
                Tag = projectPath,
                ToolTip = projectPath
            };
            menuItem.Click += (s, args) =>
            {
                var path = (string)((MenuItem)s!).Tag;
                OpenProjectFromPath(path);
            };
            menu.Items.Add(menuItem);
        }
        
        menu.Items.Add(new Separator());
        
        var clearItem = new MenuItem { Header = "Clear Recent" };
        clearItem.Click += (s, args) =>
        {
            SettingsManager.Current.RecentProjects.Clear();
            SettingsManager.Save();
            StatusText.Text = "Recent projects cleared";
        };
        menu.Items.Add(clearItem);
        
        menu.IsOpen = true;
    }
    
    private void OpenProjectFromPath(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show($"Project file not found:\n{path}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        HideWelcomeScreen();
        try
        {
            _currentProject = ProjectSerializer.Load(path);
            _currentProjectPath = path;
            _toolpaths.Clear();
            
            TitleProjectName.Text = _currentProject.Root.Name;
            ProjectTree.ItemsSource = new ObservableCollection<ProjectNode> { _currentProject.Root };
            
            SettingsManager.AddRecentProject(path);
            
            DrawCanvas();
            Update3DView();
            
            StatusText.Text = $"Opened: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog();
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true && dialog.SettingsChanged)
        {
            // Apply settings changes
            ApplySettings();
            StatusText.Text = "Settings saved and applied";
        }
    }
    
    private void OpenPostProcessorManager_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PostProcessorDialog();
        dialog.Owner = this;
        dialog.ShowDialog();
        LogOutput("Post Processor Manager closed");
    }
    
    private void ApplySettings()
    {
        var settings = SettingsManager.Current;
        
        // Apply UI settings
        if (_currentProject != null)
        {
            // Redraw canvas with new grid settings
            DrawCanvas();
        }
        
        // Update 3D view if needed
        Update3DView();
        
        // Update title with machine name
        if (settings.SelectedMachine != null)
        {
            var baseName = _currentProject?.Root.Name ?? "Untitled";
            Title = $"E3Studio — {baseName} [{settings.SelectedMachine.Name}]";
        }
    }
    
    private void OpenStockSetup_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;
        
        var dialog = new StockSetupDialog(_currentProject.Stock);
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true)
        {
            _currentProject.Stock.Width = dialog.Stock.Width;
            _currentProject.Stock.Height = dialog.Stock.Height;
            _currentProject.Stock.Thickness = dialog.Stock.Thickness;
            _currentProject.Stock.ZeroPoint = dialog.Stock.ZeroPoint;
            _currentProject.Stock.Material = dialog.SelectedMaterial;
            
            DrawCanvas();
            Update3DView();
            
            StatusText.Text = $"Stock updated: {dialog.Stock.Width}x{dialog.Stock.Height}x{dialog.Stock.Thickness}mm";
        }
    }
    
    private void ImportSVG_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "SVG Files|*.svg",
            Title = "Import SVG Model"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var importer = new SvgImporter();
                var layers = importer.Import(dialog.FileName);
                
                if (_currentProject != null)
                {
                    var wcs = _currentProject.Root.Children.FirstOrDefault(c => c is WCSNode) as WCSNode;
                    var layersFolder = wcs?.Children.FirstOrDefault(c => c.Name == "2D Layers") as FolderNode;
                    
                    if (layersFolder != null)
                    {
                        var modelNode = new LayerNode 
                        { 
                            Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
                            Color = layers.Count > 0 ? layers[0].Color : "#00D4AA",
                            IsExpanded = true 
                        };
                        
                        var geoNodes = new List<GeometryNode>();
                        int pathIdx = 1;
                        foreach (var importedLayer in layers)
                        {
                            foreach (var path in importedLayer.Paths)
                            {
                                geoNodes.Add(new GeometryNode
                                {
                                    Name = $"Path {pathIdx++}",
                                    PathData = path,
                                    IsVisible = true
                                });
                            }
                        }
                        
                        // Hierarchical grouping (Holes)
                        var sortedNodes = geoNodes.OrderByDescending(n => GetPathArea(n.PathData)).ToList();
                        var topLevelNodes = new List<GeometryNode>();
                        
                        foreach (var node in sortedNodes)
                        {
                            bool wasNested = false;
                            var rectNode = GetBounds(node.PathData);
                            
                            foreach (var potentialParent in geoNodes)
                            {
                                if (potentialParent == node) continue;
                                var rectParent = GetBounds(potentialParent.PathData);
                                if (rectParent.Contains(rectNode))
                                {
                                     potentialParent.Children.Add(node);
                                     wasNested = true;
                                     break;
                                }
                            }
                            if (!wasNested) topLevelNodes.Add(node);
                        }
                        
                        foreach(var n in topLevelNodes) modelNode.Children.Add(n);
                        layersFolder.Children.Add(modelNode);
                    }
                }
                
                // Center imported geometry on stock
                CenterGeometryOnStock(layers.SelectMany(l => l.Paths).ToList());
                
                DrawCanvas();
                AutoFitView();
                StatusText.Text = $"Imported {layers.Sum(l => l.Paths.Count)} paths.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }
    }
    
    /// <summary>
    /// Centers imported geometry on the stock material
    /// </summary>
    private void CenterGeometryOnStock(List<GeometryPath> paths)
    {
        if (_currentProject == null || paths.Count == 0) return;
        
        // Calculate bounding box of all geometry
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        
        foreach (var path in paths)
        {
            if (path is PolyPath poly)
            {
                foreach (var seg in poly.Segments)
                {
                    double px = path.X + seg.EndPoint.X * path.ScaleX;
                    double py = path.Y + seg.EndPoint.Y * path.ScaleY;
                    
                    minX = Math.Min(minX, px);
                    minY = Math.Min(minY, py);
                    maxX = Math.Max(maxX, px);
                    maxY = Math.Max(maxY, py);
                }
            }
        }
        
        if (minX == double.MaxValue) return;
        
        // Calculate geometry center
        double geomCenterX = (minX + maxX) / 2;
        double geomCenterY = (minY + maxY) / 2;
        
        // We want geometry centered at (0,0) for both 2D canvas and 3D view
        // 2D canvas: Origin (0,0) is at canvas center, stock is drawn centered there
        // 3D view: Stock is drawn with center at (Width/2, Height/2, Thickness/2)
        // So toolpath coordinates should be relative to stock center (0,0)
        
        // Offset to move geometry center to origin (0,0)
        double offsetX = -geomCenterX;
        double offsetY = -geomCenterY;
        
        // Apply offset to all paths
        foreach (var path in paths)
        {
            path.X += offsetX;
            path.Y += offsetY;
        }
    }
    
    private void ImportBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void ImportDXF_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All Supported|*.dxf;*.svg|DXF Files|*.dxf|SVG Files|*.svg",
            Title = "Import Vector File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(dialog.FileName).ToLower();
                LogOutput($"Importing {ext.ToUpper().TrimStart('.')}: {System.IO.Path.GetFileName(dialog.FileName)}...");
                
                List<Layer> layers;
                
                if (ext == ".svg")
                {
                    var importer = new SvgImporter();
                    layers = importer.Import(dialog.FileName);
                }
                else
                {
                    var importer = new DxfImporter();
                    layers = importer.Import(dialog.FileName);
                }
                
                if (_currentProject != null)
                {
                    var wcs = _currentProject.Root.Children.FirstOrDefault(c => c is WCSNode) as WCSNode;
                    var layersFolder = wcs?.Children.FirstOrDefault(c => c.Name == "2D Layers") as FolderNode;
                    
                    if (layersFolder != null)
                    {
                        var modelNode = new LayerNode 
                        { 
                            Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
                            Color = layers.Count > 0 ? layers[0].Color : "#00D4AA",
                            IsExpanded = true 
                        };
                        
                        int pathIdx = 1;
                        foreach (var importedLayer in layers)
                        {
                            foreach (var path in importedLayer.Paths)
                            {
                                modelNode.Children.Add(new GeometryNode
                                {
                                    Name = $"Path {pathIdx++}",
                                    PathData = path,
                                    IsVisible = true
                                });
                            }
                        }
                        
                        layersFolder.Children.Add(modelNode);
                    }
                    
                    CenterGeometryOnStock(layers.SelectMany(l => l.Paths).ToList());
                }
                
                DrawCanvas();
                AutoFitView();
                
                var totalPaths = layers.Sum(l => l.Paths.Count);
                LogOutput($"Imported: {totalPaths} paths from {layers.Count} layers");
                LogOutput("Next: Select paths → Create toolpath (Profile/Pocket/Drill)");
                
                StatusText.Text = $"Imported {totalPaths} paths from {ext.ToUpper().TrimStart('.')}.";
            }
            catch (Exception ex)
            {
                LogOutput($"ERROR: Import failed - {ex.Message}");
                MessageBox.Show($"Failed to import: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void CreateProfileToolpath_Click(object sender, RoutedEventArgs e)
    {
        CreateToolpath(ToolpathType.Profile);
    }
    
    private void CreatePocketToolpath_Click(object sender, RoutedEventArgs e)
    {
        CreateToolpath(ToolpathType.Pocket);
    }
    
    private void CreateDrillToolpath_Click(object sender, RoutedEventArgs e)
    {
        CreateToolpath(ToolpathType.Drill);
    }
    
    private void CreateToolpath(ToolpathType type)
    {
        if (_currentProject == null || _multiSelection.Count == 0)
        {
            MessageBox.Show("Please select one or more paths to create a toolpath.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var dialog = new ToolpathDialog(_multiSelection, _currentProject.Stock, type);
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true && dialog.Created)
        {
            var toolpath = dialog.Result;
            
            LogOutput($"Creating {type} toolpath: {toolpath.Name}");
            LogOutput($"  Tool: Ø{toolpath.ToolDiameter}mm {toolpath.ToolName}");
            LogOutput($"  Selected paths: {_multiSelection.Count}");
            
            switch (toolpath.Type)
            {
                case ToolpathType.Profile:
                    toolpath.Moves = ToolpathEngine.ComputeProfile(_multiSelection, toolpath);
                    break;
                case ToolpathType.Pocket:
                    toolpath.Moves = ToolpathEngine.ComputePocket(_multiSelection, toolpath);
                    break;
                case ToolpathType.Drill:
                    toolpath.Moves = ToolpathEngine.ComputeDrill(_multiSelection, toolpath);
                    break;
            }
            
            _toolpaths.Add(toolpath);
            
            var wcs = _currentProject.Root.Children.FirstOrDefault(c => c is WCSNode) as WCSNode;
            var toolpathsFolder = wcs?.Children.FirstOrDefault(c => c.Name == "Toolpaths") as FolderNode;
            
            if (toolpathsFolder != null)
            {
                toolpathsFolder.Children.Add(new ToolpathNode
                {
                    Name = toolpath.Name,
                    Data = toolpath
                });
            }
            
            UpdateGCodePreview();
            DrawCanvas();
            
            LogOutput($"  Moves generated: {toolpath.Moves.Count}");
            LogOutput($"  Step-down: {toolpath.StepDown}mm, Feed: {toolpath.FeedRate}mm/min");
            LogOutput("Ready for G-Code generation");
            
            StatusText.Text = $"Created {toolpath.Type} toolpath: {toolpath.Name} ({toolpath.Moves.Count} moves) - Press GENERATE to preview";
        }
    }

    private Tool GetActiveCamTool()
    {
        if (_activeCamTool != null) return _activeCamTool;
        return new Tool { Number = 1, Diameter = 6, Type = ToolType.Endmill, Name = "Default Endmill" };
    }

    private void CreateVCarveToolpath_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null || _multiSelection.Count == 0)
        {
            MessageBox.Show("Select paths for V-Carve.", "V-Carve", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var paths = _multiSelection.OfType<PolyPath>().ToList();
        if (paths.Count == 0)
        {
            MessageBox.Show("V-Carve requires vector paths.", "V-Carve", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var tool = GetActiveCamTool();
        if (tool.Type != ToolType.VBit)
            tool.VAngle = tool.VAngle > 0 ? tool.VAngle : 60;

        var engine = new VCarveEngine();
        var settings = new VCarveSettings
        {
            MaxDepth = 2.0,
            SafeHeight = 10.0,
            FeedRate = 800,
            PlungeRate = 200,
            SpindleRPM = 12000
        };

        var toolpath = engine.GenerateVCarve(paths, settings, tool);
        _toolpaths.Add(toolpath);
        UpdateGCodePreview();
        DrawCanvas();
        StatusText.Text = $"V-Carve toolpath created ({toolpath.Moves.Count} moves)";
    }

    private void RunNesting_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null || _multiSelection.Count == 0)
        {
            MessageBox.Show("Select parts to nest.", "Nesting", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var parts = _multiSelection.OfType<PolyPath>().Select((p, i) => new NestingPart
        {
            Id = $"part_{i}",
            Paths = new List<PolyPath> { p },
            Quantity = 1,
            AllowRotation = true
        }).ToList();

        var engine = new NestingEngine();
        var settings = new NestingSettings
        {
            StockWidth = _currentProject.Stock.Width,
            StockHeight = _currentProject.Stock.Height,
            Algorithm = NestingAlgorithm.BottomLeftFill
        };

        var result = engine.Nest(parts, settings);
        foreach (var placement in result.Placements)
        {
            foreach (var path in placement.Part.Paths)
            {
                path.X = placement.X;
                path.Y = placement.Y;
                path.Rotation = placement.Rotation;
            }
        }

        DrawCanvas();
        StatusText.Text = $"Nested {result.Placements.Count} parts ({result.Efficiency:P0} efficiency)";
    }

    private void AddTabsToToolpath_Click(object sender, RoutedEventArgs e)
    {
        var toolpath = _selectedToolpath ?? _toolpaths.LastOrDefault();
        if (toolpath == null || toolpath.Moves.Count == 0)
        {
            MessageBox.Show("Select or create a toolpath first.", "Tabs", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var generator = new TabGenerator();
        var settings = new TabSettings
        {
            TabCount = toolpath.TabCount > 0 ? toolpath.TabCount : 4,
            TabWidth = toolpath.TabWidth > 0 ? toolpath.TabWidth : 5,
            TabHeight = toolpath.TabHeight > 0 ? toolpath.TabHeight : 2
        };

        toolpath.Moves = generator.AddTabsToMoves(toolpath.Moves, settings, toolpath.FinalDepth);
        DrawCanvas();
        UpdateGCodePreview();
        StatusText.Text = $"Tabs added to {toolpath.Name}";
    }
    
    private void Update3DToolpaths()
    {
        if (_currentProject == null || Viewport3D == null) return;
        
        try
        {
            // Clear existing toolpath visual
            if (ToolpathVisual3D != null)
            {
                ToolpathVisual3D.Content = null;
            }
            
            if (_toolpaths.Count == 0) return;
            
            // Create new toolpath visualization with selected toolpath highlighted
            // Pass stock dimensions for proper positioning
            var visual = ToolpathVisualizer3D.CreateToolpathVisual(
                _toolpaths, 
                _currentProject.Stock.Thickness, 
                _selectedToolpath,
                _currentProject.Stock.Width,
                _currentProject.Stock.Height);
                
            if (ToolpathVisual3D != null && visual.Content != null)
            {
                ToolpathVisual3D.Content = visual.Content;
            }
            
            // Update tool visual size based on first toolpath's tool
            var firstTool = _toolpaths.FirstOrDefault()?.Tool;
            if (firstTool != null && ToolVisual3D != null)
            {
                ToolVisual3D.BaseRadius = firstTool.Diameter / 2;
                ToolVisual3D.TopRadius = firstTool.Diameter / 2;
                ToolVisual3D.Height = firstTool.FluteLength;
                ToolInfo3D.Text = $"Ø{firstTool.Diameter}mm {firstTool.Type}";
            }
            
            // Load toolpaths into simulator (for realtime playback)
            if (_simulator == null) InitializeSimulator();
            LoadToolpathsToSimulator();
            
            // Update move info
            if (_simulator != null)
            {
                SimMoveInfo.Text = $"Move: 0 / {_simulator.TotalMoves}";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error updating 3D view: {ex.Message}";
        }
    }
    
    private void SwitchTo3DView()
    {
        // Switch to 3D view using radio buttons
        View3DBtn.IsChecked = true;
        View2D.Visibility = Visibility.Collapsed;
        View3D.Visibility = Visibility.Visible;
        
        Update3DView();
        
        // Zoom to fit
        Viewport3D?.ZoomExtents(500);
    }
    
    private void GenerateGCode_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null || _toolpaths.Count == 0)
        {
            MessageBox.Show("No toolpaths to generate. Create toolpaths first.", "No Toolpaths", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        try
        {
            LogOutput("───────────────────────────────────────");
            LogOutput("Generating G-Code...");
            LogOutput($"  Toolpaths: {_toolpaths.Count}");
            
            var generator = new GCodeGenerator();
            
            var appSettings = SettingsManager.Current;
            generator.Settings.MinSafeHeight = appSettings.GCodeSettings.SafeZ;
            generator.Settings.RapidClearance = appSettings.GCodeSettings.RapidClearance;
            generator.Settings.ToolChangeHeight = appSettings.GCodeSettings.ToolChangeHeight;
            generator.Settings.UseCoolant = appSettings.GCodeSettings.UseCoolant;
            generator.Settings.UseCannedCycles = appSettings.GCodeSettings.UseCannedCycles;
            generator.Settings.UseToolLengthComp = appSettings.GCodeSettings.UseToolLengthComp;
            generator.Settings.SpindleWarmupTime = appSettings.GCodeSettings.SpindleWarmupTime;
            generator.Settings.UseLineNumbers = appSettings.GCodeSettings.IncludeLineNumbers;
            generator.Settings.LineNumberIncrement = appSettings.GCodeSettings.LineNumberIncrement;
            generator.Settings.PostProcessor = appSettings.GCodeSettings.PostProcessor;
            
            _lastGeneratedGCode = generator.Generate(_currentProject, _toolpaths);
            
            var lineCount = _lastGeneratedGCode.Split('\n').Length;
            LogOutput($"  G-Code lines: {lineCount:N0}");
            LogOutput($"  Post-processor: {appSettings.GCodeSettings.PostProcessor}");
            
            UpdateGCodePreview();
            
            if (_simulator == null) InitializeSimulator();
            LoadToolpathsToSimulator();
            
            LogOutput("G-Code generation complete");
            LogOutput("Next: Click 'SIMULATE' to preview or 'EXPORT' to save");
            
            StatusText.Text = $"G-Code generated ({_toolpaths.Count} toolpaths) - Click 'Export G-Code' to save to file";
        }
        catch (Exception ex)
        {
            LogOutput($"ERROR: G-Code generation failed - {ex.Message}");
            MessageBox.Show($"Failed to generate G-Code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void ExportGCode_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("No project open.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Generate if not already done
        if (string.IsNullOrEmpty(_lastGeneratedGCode))
        {
            if (_toolpaths.Count == 0)
            {
                MessageBox.Show("No toolpaths to export. Create toolpaths first.", "No Toolpaths", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            GenerateGCode_Click(sender, e);
        }
        
        var settings = SettingsManager.Current;
        var fileExt = settings.GCodeSettings.FileExtension;
        
        var dialog = new SaveFileDialog
        {
            Filter = $"G-Code Files (*{fileExt})|*{fileExt}|G-Code Files (*.nc)|*.nc|G-Code Files (*.gcode)|*.gcode|All Files (*.*)|*.*",
            Title = "Export G-Code",
            FileName = _currentProject.Root.Name + fileExt
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(dialog.FileName, _lastGeneratedGCode);
                
                var fileSize = new System.IO.FileInfo(dialog.FileName).Length;
                LogOutput($"G-Code exported: {System.IO.Path.GetFileName(dialog.FileName)} ({fileSize:N0} bytes)");
                
                StatusText.Text = $"G-Code exported: {System.IO.Path.GetFileName(dialog.FileName)}";
                
                SwitchTo3DView();
                
                MessageBox.Show($"G-Code exported successfully!\n\nFile: {dialog.FileName}\n\nPress Play to start simulation.", 
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogOutput($"ERROR: Export failed - {ex.Message}");
                MessageBox.Show($"Failed to export G-Code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void GCodePanel_Export_Click(object sender, RoutedEventArgs e)
    {
        ExportGCode_Click(sender, e);
    }

    private void GCodePanel_Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastGeneratedGCode))
        {
            Clipboard.SetText(_lastGeneratedGCode);
            StatusText.Text = "G-Code copied to clipboard";
        }
        else if (GCodePreview != null && !string.IsNullOrEmpty(GCodePreview.Text))
        {
            Clipboard.SetText(GCodePreview.Text);
            StatusText.Text = "G-Code preview copied to clipboard";
        }
        else
        {
            StatusText.Text = "No G-Code to copy — generate toolpaths first";
        }
    }

    private void UpdateGCodePreview()
    {
        if (_currentProject == null || _toolpaths.Count == 0)
        {
            GCodePreview.Text = "; E3Studio G-Code\n; No toolpaths generated\n\n; Create toolpaths first...";
            return;
        }
        
        // Use cached G-Code if available
        string gcode = _lastGeneratedGCode;
        if (string.IsNullOrEmpty(gcode))
        {
            var generator = new GCodeGenerator();
            gcode = generator.Generate(_currentProject, _toolpaths);
        }
        
        // Limit preview to first 100 lines
        var lines = gcode.Split('\n');
        if (lines.Length > 100)
        {
            GCodePreview.Text = string.Join("\n", lines.Take(100)) + "\n\n; ... (truncated)";
        }
        else
        {
            GCodePreview.Text = gcode;
        }
    }
    
    private double GetPathArea(GeometryPath path)
    {
        var bounds = GetBounds(path);
        return bounds.Width * bounds.Height;
    }
    
    private Rect GetBounds(GeometryPath path)
    {
        if (path is PolyPath poly && poly.Segments.Count > 0)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var seg in poly.Segments)
            {
                minX = Math.Min(minX, seg.EndPoint.X);
                minY = Math.Min(minY, seg.EndPoint.Y);
                maxX = Math.Max(maxX, seg.EndPoint.X);
                maxY = Math.Max(maxY, seg.EndPoint.Y);
            }
            return new Rect(minX, minY, Math.Max(0.001, maxX - minX), Math.Max(0.001, maxY - minY));
        }
        return new Rect(0,0,0,0);
    }
    
    private Rect GetSelectionWorldBounds()
    {
        if (_multiSelection.Count == 0) return Rect.Empty;
        
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasGeometry = false;

        foreach (var path in _multiSelection)
        {
            if (path is PolyPath polyPath)
            {
                var transform = E3Studio.Services.Matrix3x2.Identity * 
                                E3Studio.Services.Matrix3x2.CreateScale(path.ScaleX, path.ScaleY) * 
                                E3Studio.Services.Matrix3x2.CreateRotation(path.Rotation * Math.PI / 180.0) * 
                                E3Studio.Services.Matrix3x2.CreateTranslation(path.X, path.Y);
                                
                foreach (var seg in polyPath.Segments)
                {
                    hasGeometry = true;
                    var pt = new E3Studio.Models.Point2D(seg.EndPoint.X, seg.EndPoint.Y);
                    pt = transform.Transform(pt);
                    
                    minX = Math.Min(minX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxX = Math.Max(maxX, pt.X);
                    maxY = Math.Max(maxY, pt.Y);
                }
            }
        }
        
        return hasGeometry ? new Rect(minX, minY, Math.Max(0.001, maxX - minX), Math.Max(0.001, maxY - minY)) : Rect.Empty;
    }
    
    private void AutoFitView()
    {
        if (_currentProject == null) return;
        
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasContent = false;
        
        var paths = GetAllVisiblePaths(_currentProject.Root);
        
        foreach (var path in paths)
        {
            if (path is PolyPath polyPath)
            {
                var transform = E3Studio.Services.Matrix3x2.Identity * 
                                E3Studio.Services.Matrix3x2.CreateScale(path.ScaleX, path.ScaleY) * 
                                E3Studio.Services.Matrix3x2.CreateRotation(path.Rotation * Math.PI / 180.0) * 
                                E3Studio.Services.Matrix3x2.CreateTranslation(path.X, path.Y);
                                
                foreach (var seg in polyPath.Segments)
                {
                    hasContent = true;
                    var pt = new E3Studio.Models.Point2D(seg.EndPoint.X, seg.EndPoint.Y);
                    pt = transform.Transform(pt);
                    
                    minX = Math.Min(minX, pt.X);
                    minY = Math.Min(minY, pt.Y);
                    maxX = Math.Max(maxX, pt.X);
                    maxY = Math.Max(maxY, pt.Y);
                }
            }
        }
        
        if (hasContent)
        {
            double w = Math.Max(10, maxX - minX);
            double h = Math.Max(10, maxY - minY);
            
            // Add padding
            w *= 1.2;
            h *= 1.2;
            
            double cx = (minX + maxX) / 2;
            double cy = (minY + maxY) / 2;
            
            double screenW = MainCanvas.ActualWidth > 0 ? MainCanvas.ActualWidth : 800;
            double screenH = MainCanvas.ActualHeight > 0 ? MainCanvas.ActualHeight : 600;
            
            double zoomX = screenW / w;
            double zoomY = screenH / h;
            
            _zoom = Math.Min(zoomX, zoomY);
            if (_zoom < 0.1) _zoom = 0.1;
            if (_zoom > 500) _zoom = 500;
            
            // Center the geometry bounds at screen center.
            // DrawCanvas renders: screenX = (canvasW/2 + panX) + geomX * zoom
            //                     screenY = (canvasH/2 + panY) - geomY * zoom
            // For geomCenter to land at screen center: panX = -geomCx*zoom, panY = geomCy*zoom
            _panOffset = new Point(-cx * _zoom, cy * _zoom);
            
            DrawCanvas();
        }
    }
    
    private List<GeometryPath> GetAllVisiblePaths(ProjectNode node)
    {
        var list = new List<GeometryPath>();
        if (!node.IsVisible) return list;
        
        if (node is GeometryNode g && g.PathData != null) list.Add(g.PathData);
        
        foreach(var c in node.Children) list.AddRange(GetAllVisiblePaths(c));
        return list;
    }
    
    private void RefreshLayersList()
    {
        // Deprecated, handled by TreeView data binding
    }

    private void UpdateStatusBar()
    {
        if (StatusGridText == null || StatusZoomText == null) return;
        double gridMm = SettingsManager.Current?.GridSpacing ?? 10;
        StatusGridText.Text = $"Grid: {gridMm:G}mm";
        StatusZoomText.Text = $"Zoom: {_zoom * 100:F0}%";
    }
    
    #endregion
    
    #region Canvas Interaction
    
    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(MainCanvas);
        
        double factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        double newZoom = Math.Clamp(_zoom * factor, 0.1, 20);
        
        // Zoom towards mouse position
        double dx = (mousePos.X - _panOffset.X) * (1 - newZoom / _zoom);
        double dy = (mousePos.Y - _panOffset.Y) * (1 - newZoom / _zoom);
        
        _panOffset = new Point(_panOffset.X + dx, _panOffset.Y + dy);
        _zoom = newZoom;

        ZoomLevel.Text = $" {_zoom * 100:F0}%";
        UpdateStatusBar();
        DrawCanvas();
        e.Handled = true;
    }
    
    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var mousePos = e.GetPosition(MainCanvas);
        
        // 1. Gizmo Hit Test (Handles)
        _activeGizmoHandle = GizmoHandle.None;
        var hitResult = VisualTreeHelper.HitTest(MainCanvas, mousePos);
        if (hitResult?.VisualHit is Shape shape && shape.Tag is GizmoHandle handle)
        {
            _activeGizmoHandle = handle;
            _lastMousePos = mousePos;
            // Save initial transform state for undo
            SaveTransformStartState();
            MainCanvas.CaptureMouse();
            return;
        }

        // Panning (Middle Click OR Space+Left Click)

        if (e.MiddleButton == MouseButtonState.Pressed || 
            (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space)))
        {
            _isPanning = true;
            _panStart = e.GetPosition(MainCanvas);
            MainCanvas.CaptureMouse();
            MainCanvas.Cursor = Cursors.SizeAll;
            e.Handled = true;
            return;
        }

        // Selection / Tool Logic
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            // Convert mouse to world coordinates
            double cx = MainCanvas.ActualWidth / 2 + _panOffset.X;
            double cy = MainCanvas.ActualHeight / 2 + _panOffset.Y;
            double worldX = (mousePos.X - cx) / _zoom;
            double worldY = -(mousePos.Y - cy) / _zoom; 
            
            // Hit Test
            var clickedPath = HitTest(worldX, worldY);
            
            if (clickedPath != null)
            {
                // Multi-drag support: If clicking on something already selected, don't clear selection
                if (!_multiSelection.Contains(clickedPath))
                {
                    SelectPath(clickedPath);
                }
                
                if (_activeTool == TransformTool.Move || _activeTool == TransformTool.Select)
                {
                    _isDraggingPath = true;
                    _lastMousePos = mousePos;
                    // Save initial transform state for undo
                    SaveTransformStartState();
                    MainCanvas.CaptureMouse();
                }
            }
            else
            {
                // Empty space click -> Start Box Select
                SelectPath(null); // Clear selection
                
                if (_activeTool == TransformTool.Select)
                {
                    _isBoxSelecting = true;
                    _boxSelectStart = mousePos; 
                    _boxSelectEnd = mousePos;
                    MainCanvas.CaptureMouse();
                }
            }
        }
    }
    
    private void SelectPath(GeometryPath? path)
    {
        _ignoreTreeSelection = true;
        try 
        {
            // Sync Multi-select list
            _multiSelection.Clear();
            
            GroupNode? parentGroup = null;
            
            // Check if path belongs to a group, if so select the whole group
            if (path != null && _currentProject != null)
            {
                 parentGroup = FindParentGroup(_currentProject.Root, path);
            }
            
            if (parentGroup != null)
            {
                foreach(var child in parentGroup.Children.OfType<GeometryNode>())
                {
                    _multiSelection.Add(child.PathData);
                }
                parentGroup.IsSelected = true;
                parentGroup.IsExpanded = true;
            }
            else if (path != null) 
            {
                _multiSelection.Add(path);
            }
            
            // Clear all IsSelected flags visually
            if (_currentProject != null) ClearVisualSelection(_currentProject.Root);
            
            foreach(var p in _multiSelection) p.IsSelected = true;
            
            _selectedPath = path; 
            
            if (_multiSelection.Count > 0)
            {
                UpdatePropertiesPanel();
                if (TransformProperties != null) TransformProperties.Visibility = Visibility.Visible;
                if (NoSelectionProperties != null) NoSelectionProperties.Visibility = Visibility.Collapsed;
                
                // Tree Sync
                SyncTreeSelectionFromCanvas();
            }
            else
            {
                if (TransformProperties != null) TransformProperties.Visibility = Visibility.Collapsed;
                if (NoSelectionProperties != null) NoSelectionProperties.Visibility = Visibility.Visible;
            }
            
            DrawCanvas();
        }
        finally 
        {
            _ignoreTreeSelection = false;
        }
    }
    
    private void ClearVisualSelection(ProjectNode node)
    {
        node.IsSelected = false;
        if (node is GeometryNode g) g.PathData.IsSelected = false;
        foreach(var child in node.Children) ClearVisualSelection(child);
    }
    
    private GroupNode? FindParentGroup(ProjectNode root, GeometryPath target)
    {
        if (root is GroupNode group && group.Children.OfType<GeometryNode>().Any(g => g.PathData == target))
            return group;
            
        foreach(var child in root.Children)
        {
            var res = FindParentGroup(child, target);
            if (res != null) return res;
        }
        return null;
    }
    
    private void SyncTreeSelectionFromCanvas()
    {
        if (_selectedPath == null || _currentProject == null) return;
        
        var node = FindNodeForPath(_currentProject.Root, _selectedPath);
        if (node != null)
        {
            node.IsSelected = true;
            // Expand parents? FindParent logic needed
            ExpandParents(node);
        }
    }
    
    private ProjectNode? FindNodeForPath(ProjectNode root, GeometryPath path)
    {
        if (root is GeometryNode g && g.PathData == path) return g;
        foreach(var child in root.Children)
        {
            var res = FindNodeForPath(child, path);
            if (res != null) return res;
        }
        return null;
    }
    
    private void ExpandParents(ProjectNode node)
    {
        if (_currentProject == null) return;
        var current = node;
        while(current != null)
        {
            var p = FindParent(_currentProject.Root, current);
            if (p != null) p.IsExpanded = true;
            current = p;
        }
    }
    
    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.Tag is string tag)
        {
            if (Enum.TryParse(tag, out TransformTool tool))
            {
                _activeTool = tool;
                StatusText.Text = $"Active Tool: {_activeTool}";
            }
        }
        else if (sender is RadioButton rbtn && rbtn.Tag is string rtag) 
        {
             if (Enum.TryParse(rtag, out TransformTool tool))
            {
                _activeTool = tool;
                StatusText.Text = $"Active Tool: {_activeTool}";
            }
        }
    }

    private void UpdatePropertiesPanel()
    {
        if (_selectedPath == null && _multiSelection.Count == 0) return;
        if (PropPosX == null) return; // UI not ready

        if (_multiSelection.Count > 1)
        {
            // Helper to check consistency across selection
            bool consistentX = _multiSelection.All(p => Math.Abs(p.X - _multiSelection[0].X) < 0.001);
            bool consistentY = _multiSelection.All(p => Math.Abs(p.Y - _multiSelection[0].Y) < 0.001);
            bool consistentR = _multiSelection.All(p => Math.Abs(p.Rotation - _multiSelection[0].Rotation) < 0.001);
            bool consistentSX = _multiSelection.All(p => Math.Abs(p.ScaleX - _multiSelection[0].ScaleX) < 0.001);
            bool consistentSY = _multiSelection.All(p => Math.Abs(p.ScaleY - _multiSelection[0].ScaleY) < 0.001);

            PropPosX.Text = consistentX ? _multiSelection[0].X.ToString("F3") : "---";
            PropPosY.Text = consistentY ? _multiSelection[0].Y.ToString("F3") : "---";
            PropRotation.Text = consistentR ? _multiSelection[0].Rotation.ToString("F1") : "---";
            PropScaleX.Text = consistentSX ? _multiSelection[0].ScaleX.ToString("F3") : "---";
            PropScaleY.Text = consistentSY ? _multiSelection[0].ScaleY.ToString("F3") : "---";

            if (PathInfoSection != null) PathInfoSection.Visibility = Visibility.Collapsed;
        }
        else if (_selectedPath != null)
        {
            PropPosX.Text = _selectedPath.X.ToString("F3");
            PropPosY.Text = _selectedPath.Y.ToString("F3");
            PropRotation.Text = _selectedPath.Rotation.ToString("F1");
            PropScaleX.Text = _selectedPath.ScaleX.ToString("F3");
            PropScaleY.Text = _selectedPath.ScaleY.ToString("F3");

            // Path Info section
            if (PathInfoSection != null && _selectedPath is PolyPath polyPath)
            {
                PathInfoSection.Visibility = Visibility.Visible;

                if (PropSegments != null) PropSegments.Text = polyPath.Segments.Count.ToString();
                if (PropClosed != null)   PropClosed.Text   = polyPath.IsClosed ? "Yes" : "No";

                // Compute bounding box in local space
                if (polyPath.Segments.Count > 0)
                {
                    double minX = double.MaxValue, maxX = double.MinValue;
                    double minY = double.MaxValue, maxY = double.MinValue;
                    foreach (var seg in polyPath.Segments)
                    {
                        minX = Math.Min(minX, seg.EndPoint.X);
                        maxX = Math.Max(maxX, seg.EndPoint.X);
                        minY = Math.Min(minY, seg.EndPoint.Y);
                        maxY = Math.Max(maxY, seg.EndPoint.Y);
                    }
                    double bw = (maxX - minX) * _selectedPath.ScaleX;
                    double bh = (maxY - minY) * _selectedPath.ScaleY;
                    if (PropBoundsW != null) PropBoundsW.Text = bw.ToString("F2");
                    if (PropBoundsH != null) PropBoundsH.Text = bh.ToString("F2");
                }
                else
                {
                    if (PropBoundsW != null) PropBoundsW.Text = "—";
                    if (PropBoundsH != null) PropBoundsH.Text = "—";
                }

                // Color preview
                if (ColorPreview != null && !string.IsNullOrEmpty(_selectedPath.Color))
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(_selectedPath.Color);
                        ColorPreview.Background = new SolidColorBrush(color);
                    }
                    catch { /* leave default */ }
                    if (PropColor != null) PropColor.Text = _selectedPath.Color;
                }
            }
            else if (PathInfoSection != null)
            {
                PathInfoSection.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void ColorPreview_Click(object sender, MouseButtonEventArgs e)
    {
        // Focus the hex color input so the user can type a color
        PropColor?.Focus();
        PropColor?.SelectAll();
    }

    private void PropColor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedPath == null || PropColor == null) return;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(PropColor.Text);
            _selectedPath.Color = PropColor.Text;
            if (ColorPreview != null)
                ColorPreview.Background = new SolidColorBrush(color);
            DrawCanvas();
        }
        catch
        {
            PropColor.Text = _selectedPath.Color; // revert invalid entry
        }
    }

    
    private void BtnApplyTransform_Click(object sender, RoutedEventArgs e)
    {
        var targets = _multiSelection.Count > 0 ? _multiSelection : new List<GeometryPath>();
        if (targets.Count == 0 && _selectedPath != null) targets.Add(_selectedPath);
        
        if (targets.Count == 0) return;
        
        // Save start state for undo
        var startStates = new List<(GeometryPath path, double X, double Y, double Rotation, double ScaleX, double ScaleY)>();
        foreach (var path in targets)
        {
            if (path != null)
                startStates.Add((path, path.X, path.Y, path.Rotation, path.ScaleX, path.ScaleY));
        }
        
        foreach(var path in targets)
        {
            if (path == null) continue;
            
            // Only apply if text is not "---" (mixed) or valid number
            if (PropPosX.Text != "---" && double.TryParse(PropPosX.Text, out double x)) path.X = x;
            if (PropPosY.Text != "---" && double.TryParse(PropPosY.Text, out double y)) path.Y = y;
            if (PropRotation.Text != "---" && double.TryParse(PropRotation.Text, out double r)) path.Rotation = r;
            if (PropScaleX.Text != "---" && double.TryParse(PropScaleX.Text, out double sx)) path.ScaleX = sx;
            if (PropScaleY.Text != "---" && double.TryParse(PropScaleY.Text, out double sy)) path.ScaleY = sy;
        }
        
        // Save end state and record undo
        var endStates = new List<(GeometryPath path, double X, double Y, double Rotation, double ScaleX, double ScaleY)>();
        foreach (var path in targets)
        {
            if (path != null)
                endStates.Add((path, path.X, path.Y, path.Rotation, path.ScaleX, path.ScaleY));
        }
        
        // Only record undo if something changed
        bool hasChanges = false;
        for (int i = 0; i < startStates.Count; i++)
        {
            var (_, sx, sy, sr, ssx, ssy) = startStates[i];
            var (_, ex, ey, er, esx, esy) = endStates[i];
            if (Math.Abs(sx - ex) > 0.0001 || Math.Abs(sy - ey) > 0.0001 ||
                Math.Abs(sr - er) > 0.0001 || Math.Abs(ssx - esx) > 0.0001 || Math.Abs(ssy - esy) > 0.0001)
            {
                hasChanges = true;
                break;
            }
        }
        
        if (hasChanges)
        {
            _undoManager.RecordAction($"Apply Transform to {targets.Count} path(s)",
                () => {
                    foreach (var (path, x, y, rot, scx, scy) in startStates)
                    {
                        path.X = x; path.Y = y; path.Rotation = rot; path.ScaleX = scx; path.ScaleY = scy;
                    }
                    UpdatePropertiesPanel();
                    DrawCanvas();
                },
                () => {
                    foreach (var (path, x, y, rot, scx, scy) in endStates)
                    {
                        path.X = x; path.Y = y; path.Rotation = rot; path.ScaleX = scx; path.ScaleY = scy;
                    }
                    UpdatePropertiesPanel();
                    DrawCanvas();
                }
            );
            StatusText.Text = $"Transform applied - Press Ctrl+Z to undo";
        }
        
        DrawCanvas();
    }
    
    private GeometryPath? HitTest(double x, double y)
    {
        if (_currentProject == null || _currentProject.Root == null) return null;
        return HitTestRecursive(_currentProject.Root, E3Studio.Services.Matrix3x2.Identity, new Point(x,y), 15.0 / _zoom);
    }
    
    private GeometryPath? HitTestRecursive(ProjectNode node, E3Studio.Services.Matrix3x2 parentTransform, Point p, double threshold)
    {
        if (!node.IsVisible) return null;
        
        var transform = parentTransform; 
        
        if (node is GeometryNode geoNode && geoNode.PathData is PolyPath polyPath)
        {
             var path = geoNode.PathData;
             var localTransform = transform * 
                             E3Studio.Services.Matrix3x2.CreateScale(path.ScaleX, path.ScaleY) * 
                             E3Studio.Services.Matrix3x2.CreateRotation(path.Rotation * Math.PI / 180.0) * 
                             E3Studio.Services.Matrix3x2.CreateTranslation(path.X, path.Y);
                             
             foreach (var seg in polyPath.Segments)
             {
                 var pt = new E3Studio.Models.Point2D(seg.EndPoint.X, seg.EndPoint.Y);
                 pt = localTransform.Transform(pt);
                 
                 double dist = Math.Sqrt(Math.Pow(pt.X - p.X, 2) + Math.Pow(pt.Y - p.Y, 2));
                 if (dist < threshold) return path;
             }
        }
        
        // Reverse iteration for Z-order (topmost first)
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var res = HitTestRecursive(node.Children[i], transform, p, threshold);
            if (res != null) return res;
        }
        return null;
    }

    private List<GeometryPath> HitTestBox(Rect rect)
    {
        var list = new List<GeometryPath>();
        if (_currentProject != null) HitTestBoxRecursive(_currentProject.Root, E3Studio.Services.Matrix3x2.Identity, rect, list);
        return list;
    }
    
    private void HitTestBoxRecursive(ProjectNode node, E3Studio.Services.Matrix3x2 parentTransform, Rect rect, List<GeometryPath> list)
    {
        if (!node.IsVisible) return;
        var transform = parentTransform;
        
        if (node is GeometryNode geoNode && geoNode.PathData is PolyPath polyPath)
        {
             var path = geoNode.PathData;
             var localTransform = transform * 
                             E3Studio.Services.Matrix3x2.CreateScale(path.ScaleX, path.ScaleY) * 
                             E3Studio.Services.Matrix3x2.CreateRotation(path.Rotation * Math.PI / 180.0) * 
                             E3Studio.Services.Matrix3x2.CreateTranslation(path.X, path.Y);
                             
             foreach (var seg in polyPath.Segments)
             {
                 var pt = new E3Studio.Models.Point2D(seg.EndPoint.X, seg.EndPoint.Y);
                 pt = localTransform.Transform(pt);
                 
                 if (rect.Contains(new Point(pt.X, pt.Y))) 
                 {
                     list.Add(path);
                     break; 
                 }
             }
        }
        
        foreach(var child in node.Children) HitTestBoxRecursive(child, transform, rect, list);
    }

    private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_ignoreTreeSelection) return;
        
        if (e.NewValue is ToolpathNode tpNode)
        {
            // Toolpath selected - highlight it in 3D view
            _selectedToolpath = tpNode.Data;
            Update3DToolpathHighlight();
            
            // Show toolpath info
            if (tpNode.Data != null)
            {
                StatusText.Text = $"Selected: {tpNode.Data.Name} - {tpNode.Data.Type} - {tpNode.Data.Moves.Count} moves";
            }
            return;
        }
        
        _selectedToolpath = null;
        
        if (e.NewValue is ProjectNode node)
        {
            _ignoreTreeSelection = true;
            try
            {
                // Clear existing
                if (_currentProject != null) ClearVisualSelection(_currentProject.Root);
                _multiSelection.Clear();
                
                var paths = GetAllVisiblePaths(node);
                foreach(var p in paths)
                {
                    _multiSelection.Add(p);
                    p.IsSelected = true;
                }
                
                if (_multiSelection.Count > 0)
                {
                    _selectedPath = _multiSelection[0];
                    UpdatePropertiesPanel();
                    if (TransformProperties != null) TransformProperties.Visibility = Visibility.Visible;
                    if (NoSelectionProperties != null) NoSelectionProperties.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _selectedPath = null;
                }
                
                DrawCanvas();
            }
            finally
            {
                _ignoreTreeSelection = false;
            }
        }
    }
    
    private Toolpath? _selectedToolpath = null;
    
    private void Update3DToolpathHighlight()
    {
        if (_currentProject == null || Viewport3D == null) return;
        
        // Update 3D view with highlighted toolpath
        Update3DToolpaths();
    }
    
    // Prevent recursive selection loops
    private bool _ignoreTreeSelection = false;

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var mousePos = e.GetPosition(MainCanvas);
        
        // Transform to world coordinates for display
        double cx = MainCanvas.ActualWidth / 2 + _panOffset.X;
        double cy = MainCanvas.ActualHeight / 2 + _panOffset.Y;
        double worldX = (mousePos.X - cx) / _zoom;
        double worldY = -(mousePos.Y - cy) / _zoom;
        
        CursorPos.Text = $"X: {worldX:F3}  Y: {worldY:F3}";
        
        // Handle Gizmo
        if (_activeGizmoHandle != GizmoHandle.None)
        {
            HandleGizmoInteraction(mousePos);
            _lastMousePos = mousePos;
            return;
        }
        
        // Handle Pan

        if (_isPanning)
        {
            var current = e.GetPosition(MainCanvas);
            var delta = current - _panStart;
            
            _panOffset = new Point(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
            _panStart = current;
            DrawCanvas();
            e.Handled = true;
            return;
        }
        
        // Handle Move/Drag
        if (_isDraggingPath && _selectedPath != null && _activeTool == TransformTool.Move)
        {
            double dx = (mousePos.X - _lastMousePos.X) / _zoom;
            double dy = -(mousePos.Y - _lastMousePos.Y) / _zoom; // Y inverted
            
            // Move ALL selected paths
            foreach(var p in _multiSelection)
            {
                p.X += dx;
                p.Y += dy;
            }
            
            // Ensure primary selection is updated for properties panel (redundant if in list but safe)
            UpdatePropertiesPanel();
            DrawCanvas();
            
            _lastMousePos = mousePos;
        }
        
        // Handle Box Select
        if (_isBoxSelecting)
        {
            _boxSelectEnd = mousePos;
            DrawCanvas();
        }
    }
    
    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeGizmoHandle != GizmoHandle.None)
        {
            // Record undo for gizmo transform
            RecordTransformUndo("Transform");
            _activeGizmoHandle = GizmoHandle.None;
            MainCanvas.ReleaseMouseCapture();
            DrawCanvas();
        }

        if (_isPanning)

        {
            _isPanning = false;
            MainCanvas.ReleaseMouseCapture();
            MainCanvas.Cursor = Cursors.Arrow;
        }
        
        if (_isDraggingPath)
        {
            // Record undo for drag/move
            RecordTransformUndo("Move");
            _isDraggingPath = false;
            MainCanvas.ReleaseMouseCapture();
        }
        
        if (_isBoxSelecting)
        {
            _isBoxSelecting = false;
            MainCanvas.ReleaseMouseCapture();
            
            // Perform Box Selection
            // Calculate Box in World Coords
            double cx = MainCanvas.ActualWidth / 2 + _panOffset.X;
            double cy = MainCanvas.ActualHeight / 2 + _panOffset.Y;
            
            double x1 = (_boxSelectStart.X - cx) / _zoom;
            double y1 = -(_boxSelectStart.Y - cy) / _zoom;
            double x2 = (_boxSelectEnd.X - cx) / _zoom;
            double y2 = -(_boxSelectEnd.Y - cy) / _zoom;
            
            // Normalize Rect
            double minX = Math.Min(x1, x2);
            double maxX = Math.Max(x1, x2);
            double minY = Math.Min(y1, y2); // Y is inverted, so min screen Y is max world Y?
                                            // Wait, logic:
                                            // Screen Y increases down. World Y increases up.
                                            // cy - y * zoom = screenY => y = (cy - screenY) / zoom
                                            // So minScreenY -> maxWorldY. 
                                            // For Rect logic, we just need Min/Max values.
            
            var worldRect = new Rect(minX, Math.Min(y1,y2), Math.Abs(x1 - x2), Math.Abs(y1 - y2));
            
            var captured = HitTestBox(worldRect);
            
            _ignoreTreeSelection = true;
            try
            {
                if (_currentProject != null) ClearVisualSelection(_currentProject.Root);
                _multiSelection.Clear();
                _multiSelection.AddRange(captured);
                
                if (captured.Count > 0)
                {
                    _selectedPath = captured[0];
                    foreach(var p in captured) p.IsSelected = true;
                    
                    UpdatePropertiesPanel();
                    if (TransformProperties != null) TransformProperties.Visibility = Visibility.Visible;
                    if (NoSelectionProperties != null) NoSelectionProperties.Visibility = Visibility.Collapsed;
                    
                    SyncTreeSelectionFromCanvas();
                }
                else
                {
                    _selectedPath = null;
                    if (TransformProperties != null) TransformProperties.Visibility = Visibility.Collapsed;
                    if (NoSelectionProperties != null) NoSelectionProperties.Visibility = Visibility.Visible;
                }
            }
            finally
            {
                _ignoreTreeSelection = false;
            }
            
            DrawCanvas();
        }
    }
    
    private void DrawCanvas()
    {
        if (MainCanvas == null) return;
        
        MainCanvas.Children.Clear();
        
        double w = MainCanvas.ActualWidth > 0 ? MainCanvas.ActualWidth : 800;
        double h = MainCanvas.ActualHeight > 0 ? MainCanvas.ActualHeight : 600;
        
        // Center of canvas
        double cx = w / 2 + _panOffset.X;
        double cy = h / 2 + _panOffset.Y;
        
        // Grid
        double gridSpacing = 10 * _zoom;
        double gridExtent = 300 * _zoom;
        
        var gridBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
        var majorGridBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        
        // Draw grid lines
        for (double i = -gridExtent; i <= gridExtent; i += gridSpacing)
        {
            bool isMajor = Math.Abs((i / _zoom) % 50) < 0.5;
            var brush = isMajor ? majorGridBrush : gridBrush;
            double thickness = isMajor ? 0.5 : 0.25;
            
            // Vertical
            MainCanvas.Children.Add(new Line
            {
                X1 = cx + i, Y1 = cy - gridExtent,
                X2 = cx + i, Y2 = cy + gridExtent,
                Stroke = brush, StrokeThickness = thickness
            });
            
            // Horizontal  
            MainCanvas.Children.Add(new Line
            {
                X1 = cx - gridExtent, Y1 = cy + i,
                X2 = cx + gridExtent, Y2 = cy + i,
                Stroke = brush, StrokeThickness = thickness
            });
        }
        
        // Axes
        var xAxisBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
        var yAxisBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
        
        MainCanvas.Children.Add(new Line
        {
            X1 = cx - gridExtent, Y1 = cy,
            X2 = cx + gridExtent, Y2 = cy,
            Stroke = xAxisBrush, StrokeThickness = 1.5
        });
        
        MainCanvas.Children.Add(new Line
        {
            X1 = cx, Y1 = cy - gridExtent,
            X2 = cx, Y2 = cy + gridExtent,
            Stroke = yAxisBrush, StrokeThickness = 1.5
        });
        
        // Origin
        MainCanvas.Children.Add(new Ellipse
        {
            Width = 8, Height = 8,
            Fill = new SolidColorBrush(Color.FromRgb(0, 212, 170)),
            Margin = new Thickness(cx - 4, cy - 4, 0, 0)
        });
        
        // Stock outline
        if (_currentProject != null)
        {
            var stock = _currentProject.Stock;
            double stockW = stock.Width * _zoom;
            double stockH = stock.Height * _zoom;
            
            var stockRect = new Rectangle
            {
                Width = stockW,
                Height = stockH,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 0, 212, 170)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 3 },
                Fill = new SolidColorBrush(Color.FromArgb(15, 0, 212, 170))
            };
            
            Canvas.SetLeft(stockRect, cx - stockW / 2);
            Canvas.SetTop(stockRect, cy - stockH / 2);
            MainCanvas.Children.Add(stockRect);
        }
        
        // Draw geometry recursively
        if (_currentProject != null)
        {
            if (_currentProject.Root.IsVisible)
            {
                RenderNode(_currentProject.Root, E3Studio.Services.Matrix3x2.Identity, cx, cy, Colors.White);
            }
        }
        
        // Selection Box
        if (_isBoxSelecting)
        {
             var rect = new Rectangle
             {
                 Width = Math.Abs(_boxSelectEnd.X - _boxSelectStart.X),
                 Height = Math.Abs(_boxSelectEnd.Y - _boxSelectStart.Y),
                 Fill = new SolidColorBrush(Color.FromArgb(30, 0, 212, 170)),
                 Stroke = new SolidColorBrush(Color.FromRgb(0, 212, 170)),
                 StrokeThickness = 1,
                 StrokeDashArray = new DoubleCollection { 2, 2 }
             };
             
             Canvas.SetLeft(rect, Math.Min(_boxSelectStart.X, _boxSelectEnd.X));
             Canvas.SetTop(rect, Math.Min(_boxSelectStart.Y, _boxSelectEnd.Y));
             
             MainCanvas.Children.Add(rect);
        }
        
        DrawGizmo(cx, cy);
        
        DrawRulers(cx, cy, gridSpacing);
    }
    
    private void RenderNode(ProjectNode node, E3Studio.Services.Matrix3x2 parentTransform, double cx, double cy, Color parentColor)
    {
        if (!node.IsVisible) return;
        
        Color currentColor = parentColor;
        if (node is LayerNode lNode && !string.IsNullOrEmpty(lNode.Color))
        {
            try
            {
                currentColor = (Color)ColorConverter.ConvertFromString(lNode.Color);
            }
            catch
            {
                // Fallback or keep parent
            }
        }
        
        var transform = parentTransform; 
        
        // Render if Geometry
        if (node is GeometryNode geoNode && geoNode.PathData is PolyPath polyPath && polyPath.Segments.Count > 0)
        {
             var path = geoNode.PathData;
             var strokeBrush = new SolidColorBrush(path.IsSelected ? Colors.Yellow : currentColor);
             
             var polyline = new Polyline
             {
                 Stroke = strokeBrush,
                 StrokeThickness = (path.IsSelected ? 2.5 : 1.5) / _zoom,
                 StrokeLineJoin = PenLineJoin.Round
             };
             
             var localTransform = transform * 
                             E3Studio.Services.Matrix3x2.CreateScale(path.ScaleX, path.ScaleY) * 
                             E3Studio.Services.Matrix3x2.CreateRotation(path.Rotation * Math.PI / 180.0) * 
                             E3Studio.Services.Matrix3x2.CreateTranslation(path.X, path.Y);
             
             foreach (var seg in polyPath.Segments)
             {
                 var pt = new E3Studio.Models.Point2D(seg.EndPoint.X, seg.EndPoint.Y);
                 pt = localTransform.Transform(pt);
                 
                 double screenX = cx + pt.X * _zoom;
                 double screenY = cy - pt.Y * _zoom;
                 polyline.Points.Add(new Point(screenX, screenY));
             }
             
             MainCanvas.Children.Add(polyline);
        }
        
        // Recurse Children
        foreach(var child in node.Children)
        {
            RenderNode(child, transform, cx, cy, currentColor);
        }
    }
    
    private void DrawGizmo(double cx, double cy)
    {
        if (_multiSelection.Count == 0) return;
        _selectionWorldBounds = GetSelectionWorldBounds();
        if (_selectionWorldBounds == Rect.Empty) return;

        // Screen Rect
        double screenX = cx + _selectionWorldBounds.X * _zoom;
        double screenY = cy - (_selectionWorldBounds.Y + _selectionWorldBounds.Height) * _zoom;
        double screenW = _selectionWorldBounds.Width * _zoom;
        double screenH = _selectionWorldBounds.Height * _zoom;

        // Main Bounding Box
        var rect = new Rectangle
        {
            Width = screenW,
            Height = screenH,
            Stroke = new SolidColorBrush(Color.FromRgb(30, 144, 255)), // DodgerBlue
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        Canvas.SetLeft(rect, screenX);
        Canvas.SetTop(rect, screenY);
        MainCanvas.Children.Add(rect);

        // Handles
        DrawHandle(screenX, screenY, GizmoHandle.ScaleNW);
        DrawHandle(screenX + screenW / 2, screenY, GizmoHandle.ScaleN);
        DrawHandle(screenX + screenW, screenY, GizmoHandle.ScaleNE);
        DrawHandle(screenX + screenW, screenY + screenH / 2, GizmoHandle.ScaleE);
        DrawHandle(screenX + screenW, screenY + screenH, GizmoHandle.ScaleSE);
        DrawHandle(screenX + screenW / 2, screenY + screenH, GizmoHandle.ScaleS);
        DrawHandle(screenX, screenY + screenH, GizmoHandle.ScaleSW);
        DrawHandle(screenX, screenY + screenH / 2, GizmoHandle.ScaleW);

        // Rotation Handle
        double rotLineLen = 20;
        var rotLine = new Line
        {
            X1 = screenX + screenW / 2, Y1 = screenY,
            X2 = screenX + screenW / 2, Y2 = screenY - rotLineLen,
            Stroke = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
            StrokeThickness = 1
        };
        MainCanvas.Children.Add(rotLine);
        DrawHandle(screenX + screenW / 2, screenY - rotLineLen, GizmoHandle.Rotate, true);
    }

    private void DrawHandle(double x, double y, GizmoHandle type, bool isCircle = false)
    {
        Shape handle;
        if (isCircle)
            handle = new Ellipse { Width = _gizmoHandleSize, Height = _gizmoHandleSize };
        else
            handle = new Rectangle { Width = _gizmoHandleSize, Height = _gizmoHandleSize };

        handle.Fill = Brushes.White;
        handle.Stroke = new SolidColorBrush(Color.FromRgb(30, 144, 255));
        handle.StrokeThickness = 1;
        handle.Tag = type;

        Canvas.SetLeft(handle, x - _gizmoHandleSize / 2);
        Canvas.SetTop(handle, y - _gizmoHandleSize / 2);
        MainCanvas.Children.Add(handle);
    }

    private void HandleGizmoInteraction(Point mousePos)
    {
        if (_multiSelection.Count == 0) return;

        double dx = (mousePos.X - _lastMousePos.X) / _zoom;
        double dy = -(mousePos.Y - _lastMousePos.Y) / _zoom; // Y inverted

        foreach (var path in _multiSelection)
        {
            switch (_activeGizmoHandle)
            {
                case GizmoHandle.ScaleE:
                    path.ScaleX += dx / Math.Max(1, _selectionWorldBounds.Width);
                    break;
                case GizmoHandle.ScaleW:
                    path.X += dx;
                    path.ScaleX -= dx / Math.Max(1, _selectionWorldBounds.Width);
                    break;
                case GizmoHandle.ScaleN:
                    path.ScaleY += dy / Math.Max(1, _selectionWorldBounds.Height);
                    break;
                case GizmoHandle.ScaleS:
                    path.Y += dy;
                    path.ScaleY -= dy / Math.Max(1, _selectionWorldBounds.Height);
                    break;
                case GizmoHandle.ScaleNW:
                    path.X += dx;
                    path.ScaleX -= dx / Math.Max(1, _selectionWorldBounds.Width);
                    path.ScaleY += dy / Math.Max(1, _selectionWorldBounds.Height);
                    break;
                case GizmoHandle.ScaleNE:
                    path.ScaleX += dx / Math.Max(1, _selectionWorldBounds.Width);
                    path.ScaleY += dy / Math.Max(1, _selectionWorldBounds.Height);
                    break;
                case GizmoHandle.ScaleSE:
                    path.ScaleX += dx / Math.Max(1, _selectionWorldBounds.Width);
                    path.Y += dy;
                    path.ScaleY -= dy / Math.Max(1, _selectionWorldBounds.Height);
                    break;
                case GizmoHandle.ScaleSW:
                    path.X += dx;
                    path.ScaleX -= dx / Math.Max(1, _selectionWorldBounds.Width);
                    path.Y += dy;
                    path.ScaleY -= dy / Math.Max(1, _selectionWorldBounds.Height);
                    break;
                case GizmoHandle.Rotate:

                    // Simple rotation around center
                    var center = new Point(_selectionWorldBounds.X + _selectionWorldBounds.Width / 2, 
                                           _selectionWorldBounds.Y + _selectionWorldBounds.Height / 2);
                    
                    double cx = MainCanvas.ActualWidth / 2 + _panOffset.X;
                    double cy = MainCanvas.ActualHeight / 2 + _panOffset.Y;
                    double wx = (mousePos.X - cx) / _zoom;
                    double wy = -(mousePos.Y - cy) / _zoom;
                    
                    double angle = Math.Atan2(wy - center.Y, wx - center.X) * 180 / Math.PI;
                    path.Rotation = angle - 90; // Offset for top handle
                    break;
            }
        }

        UpdatePropertiesPanel();
        DrawCanvas();
    }

    private void DrawRulers(double cx, double cy, double spacing)

    {
        var rulerBg = new SolidColorBrush(Color.FromRgb(40, 44, 52));
        var rulerText = new SolidColorBrush(Color.FromRgb(150, 150, 150));
        
        var topRuler = new Rectangle { Height = 20, VerticalAlignment = VerticalAlignment.Top, Fill = rulerBg, Opacity = 0.8 };
        MainCanvas.Children.Add(topRuler);
        Canvas.SetLeft(topRuler, 0); Canvas.SetTop(topRuler, 0);
        topRuler.Width = MainCanvas.ActualWidth;

        var leftRuler = new Rectangle { Width = 20, HorizontalAlignment = HorizontalAlignment.Left, Fill = rulerBg, Opacity = 0.8 };
        MainCanvas.Children.Add(leftRuler);
        Canvas.SetLeft(leftRuler, 0); Canvas.SetTop(leftRuler, 0);
        leftRuler.Height = MainCanvas.ActualHeight;

        if (spacing <= 0) spacing = 10;
        for (double x = 0; x < MainCanvas.ActualWidth; x += spacing)
        {
            var tick = new Line
            {
                X1 = x, X2 = x, Y1 = 14, Y2 = 20,
                Stroke = rulerText, StrokeThickness = 1
            };
            MainCanvas.Children.Add(tick);
            if (Math.Abs(x % (spacing * 5)) < 0.01)
            {
                var label = new TextBlock
                {
                    Text = ((int)x).ToString(),
                    Foreground = rulerText,
                    FontSize = 9
                };
                MainCanvas.Children.Add(label);
                Canvas.SetLeft(label, x + 2);
                Canvas.SetTop(label, 1);
            }
        }

        for (double y = 0; y < MainCanvas.ActualHeight; y += spacing)
        {
            var tick = new Line
            {
                X1 = 14, X2 = 20, Y1 = y, Y2 = y,
                Stroke = rulerText, StrokeThickness = 1
            };
            MainCanvas.Children.Add(tick);
            if (Math.Abs(y % (spacing * 5)) < 0.01)
            {
                var label = new TextBlock
                {
                    Text = ((int)y).ToString(),
                    Foreground = rulerText,
                    FontSize = 9
                };
                MainCanvas.Children.Add(label);
                Canvas.SetLeft(label, 1);
                Canvas.SetTop(label, y + 2);
            }
        }
    }
    
    /// <summary>
    /// Save the current transform state for undo tracking
    /// </summary>
    private void SaveTransformStartState()
    {
        _transformStartState.Clear();
        foreach (var path in _multiSelection)
        {
            _transformStartState.Add((path, path.X, path.Y, path.Rotation, path.ScaleX, path.ScaleY));
        }
        StatusText.Text = $"DEBUG: Saved {_transformStartState.Count} paths for undo";
    }
    
    /// <summary>
    /// Record an undo action for the transform operation
    /// </summary>
    private void RecordTransformUndo(string actionName)
    {
        if (_transformStartState.Count == 0) return;
        
        // Check if anything actually changed
        bool hasChanges = false;
        foreach (var (path, startX, startY, startRot, startSX, startSY) in _transformStartState)
        {
            if (Math.Abs(path.X - startX) > 0.0001 || 
                Math.Abs(path.Y - startY) > 0.0001 ||
                Math.Abs(path.Rotation - startRot) > 0.0001 ||
                Math.Abs(path.ScaleX - startSX) > 0.0001 ||
                Math.Abs(path.ScaleY - startSY) > 0.0001)
            {
                hasChanges = true;
                break;
            }
        }
        
        if (!hasChanges) 
        {
            _transformStartState.Clear();
            StatusText.Text = "DEBUG: No changes detected, undo not recorded";
            return;
        }
        
        // Capture start and end states
        var startStates = _transformStartState.ToList();
        var endStates = new List<(GeometryPath path, double X, double Y, double Rotation, double ScaleX, double ScaleY)>();
        foreach (var (path, _, _, _, _, _) in startStates)
        {
            endStates.Add((path, path.X, path.Y, path.Rotation, path.ScaleX, path.ScaleY));
        }
        
        _undoManager.RecordAction($"{actionName} {startStates.Count} path(s)",
            () => {
                // UNDO: Restore start values
                foreach (var (path, x, y, rot, sx, sy) in startStates)
                {
                    path.X = x;
                    path.Y = y;
                    path.Rotation = rot;
                    path.ScaleX = sx;
                    path.ScaleY = sy;
                }
                UpdatePropertiesPanel();
                DrawCanvas();
            },
            () => {
                // REDO: Apply end values
                foreach (var (path, x, y, rot, sx, sy) in endStates)
                {
                    path.X = x;
                    path.Y = y;
                    path.Rotation = rot;
                    path.ScaleX = sx;
                    path.ScaleY = sy;
                }
                UpdatePropertiesPanel();
                DrawCanvas();
            }
        );
        
        _transformStartState.Clear();
        StatusText.Text = $"{actionName} completed - Press Ctrl+Z to undo";
    }
    
    #endregion
    
    #region Context Menu Handlers
    
    private void DeleteNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is ProjectNode node)
        {
            if (_currentProject == null) return;
            if (node == _currentProject.Root) return; 
            
            var parent = FindParent(_currentProject.Root, node);
            if (parent != null)
            {
                parent.Children.Remove(node);
                
                // Cleanup legacy Paths list if applicable
                if (parent is LayerNode layer && node is GeometryNode geo)
                {
                    layer.Paths.Remove(geo.PathData);
                }
                
                // Handle selection
                if (node is GeometryNode gNode && _multiSelection.Contains(gNode.PathData))
                {
                    _multiSelection.Remove(gNode.PathData);
                    if (_selectedPath == gNode.PathData)
                    {
                         _selectedPath = _multiSelection.Count > 0 ? _multiSelection[0] : null;
                         UpdatePropertiesPanel();
                    }
                }
                
                DrawCanvas();
            }
        }
    }
    
    private void RenameNode_Click(object sender, RoutedEventArgs e)
    {
    }
    
    private ProjectNode? FindParent(ProjectNode root, ProjectNode target)
    {
        if (root.Children.Contains(target)) return root;
        foreach(var child in root.Children)
        {
            var res = FindParent(child, target);
            if (res != null) return res;
        }
        return null;
    }
    
    #endregion
    
    #region Dialogs
    
    private void OpenToolLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.ToolLibraryDialog();
        dialog.Owner = this;
        dialog.ShowDialog();
    }
    
    private void OpenMaterialLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.MaterialLibraryDialog();
        dialog.Owner = this;
        dialog.ShowDialog();
    }
    
    private void QuickToolCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (QuickToolCombo.SelectedItem is not ComboBoxItem item) return;
        
        var tag = item.Tag?.ToString() ?? "";
        if (tag == "manage")
        {
            QuickToolCombo.SelectedIndex = 0; // revert to first real tool
            OpenToolLibrary_Click(sender, e);
            return;
        }
        
        var parts = tag.Split(';');
        if (parts.Length == 3 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double diam) &&
            int.TryParse(parts[2], out int num) &&
            Enum.TryParse<ToolType>(parts[1], out var toolType))
        {
            _activeCamTool = new Tool
            {
                Number = num,
                Diameter = diam,
                Type = toolType,
                Name = item.Content?.ToString() ?? tag
            };
            if (StatusText != null)
                StatusText.Text = $"Active tool: {_activeCamTool.Name}";
        }
    }
    
    private void SimulateAll_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null || _toolpaths.Count == 0)
        {
            MessageBox.Show(
                "No toolpaths to simulate.\n\nCreate toolpaths first using the CAM toolbar buttons, then click Simulate.",
                "No Toolpaths", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        // Generate G-Code if not already done
        if (string.IsNullOrEmpty(_lastGeneratedGCode))
            GenerateGCode_Click(sender, e);
        
        // Switch to 3D view
        SwitchTo3DView();
        
        // Ensure simulator is initialized and loaded
        if (_simulator == null)
        {
            InitializeSimulator();
            LoadToolpathsToSimulator();
        }
        else if (_simulator.TotalMoves == 0)
        {
            LoadToolpathsToSimulator();
        }
        
        // Rewind to start, then play
        _simulator?.Stop();
        _simulator?.Play();
        
        StatusText.Text = "Simulation running — use the 3D view controls to pause, step, or adjust speed";
    }
    
    #endregion
    
    #region 3D Simulation Controls
    
    private void InitializeSimulator()
    {
        _simulator = new RealtimeSimulator();
        _simulator.OnUpdate += Simulator_OnUpdate;
        _simulator.OnComplete += Simulator_OnComplete;
        _simulator.OnStart += Simulator_OnStart;
        _simulator.OnPause += Simulator_OnPause;
        _simulator.OnStop += Simulator_OnStop;
        
        // Initialize stock removal simulator
        _stockRemoval = new StockRemovalSimulator();
    }
    
    private void LoadToolpathsToSimulator()
    {
        if (_simulator == null) InitializeSimulator();
        if (_simulator == null || _toolpaths.Count == 0 || _currentProject == null) return;
        
        // Load toolpaths into simulator
        _simulator.Load(_toolpaths, _currentProject.Stock.Thickness);
        
        // Initialize stock removal with stock dimensions
        if (_stockRemoval != null)
        {
            _stockRemoval.InitializeStock(
                _currentProject.Stock.Width,
                _currentProject.Stock.Height,
                _currentProject.Stock.Thickness);
        }
        
        // Update UI with time estimate
        var totalTime = _simulator.EstimatedTotalTime;
        StatusText.Text = $"Simulation loaded: {_simulator.TotalMoves} moves, Est. time: {totalTime.Minutes}:{totalTime.Seconds:D2}";
        SimMoveType.Text = "Ready";
        SimMoveInfo.Text = $"0 / {_simulator.TotalMoves}";
    }
    
    private Point3D _lastSimPos = new Point3D(0, 0, 50);
    private int _stockUpdateCounter = 0;
    
    private void Simulator_OnUpdate(object? sender, SimulatorUpdateEventArgs args)
    {
        Dispatcher.Invoke(() =>
        {
            // Update progress
            SimulationProgress.Value = args.Progress * 100;
            
            // Update move info
            SimMoveInfo.Text = $"{args.CurrentMove + 1} / {args.TotalMoves}";
            SimMoveType.Text = args.MoveType.ToString();
            
            // Update time display
            var elapsed = args.ElapsedTime;
            var remaining = args.RemainingTime;
            StatusText.Text = $"Elapsed: {elapsed.Minutes}:{elapsed.Seconds:D2} | Remaining: {remaining.Minutes}:{remaining.Seconds:D2} | {args.GCode}";
            
            // Update 3D tool position
            Update3DToolPosition(args.Position.X, args.Position.Y, args.Position.Z);
            
            // Add trail line
            AddSimulationTrailLine(args);
            
            // Stock removal simulation - cut along the move path
            if (_stockRemoval != null && args.MoveType != MoveType.Rapid && _currentProject != null)
            {
                var currentPos = args.Position;
                double toolDiameter = _toolpaths.FirstOrDefault()?.Tool?.Diameter ?? 6.0;
                
                // Convert from center-origin to stock-origin coordinates
                // Toolpath coordinates are relative to center (0,0)
                // Stock removal uses coordinates from (0,0) to (Width, Height)
                double offsetX = _currentProject.Stock.Width / 2;
                double offsetY = _currentProject.Stock.Height / 2;
                
                // Only cut when Z is below stock top (cutting)
                double stockTop = _currentProject.Stock.Thickness;
                if (currentPos.Z < stockTop)
                {
                    _stockRemoval.CutLine(
                        _lastSimPos.X + offsetX, _lastSimPos.Y + offsetY, _lastSimPos.Z - stockTop,
                        currentPos.X + offsetX, currentPos.Y + offsetY, currentPos.Z - stockTop,
                        toolDiameter);
                }
                
                // Update 3D stock mesh periodically (every 5 moves for performance)
                _stockUpdateCounter++;
                if (_stockUpdateCounter >= 5)
                {
                    _stockUpdateCounter = 0;
                    UpdateStockRemovalVisual();
                }
            }
            
            _lastSimPos = args.Position;
        });
    }
    
    private void UpdateStockRemovalVisual()
    {
        if (_stockRemoval == null || CutSimulationVisual3D == null) return;
        
        var visual = _stockRemoval.CreateStockVisual();
        CutSimulationVisual3D.Content = visual.Content;
    }
    
    private void Simulator_OnComplete(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PlayPauseIcon.Data = Geometry.Parse("M8,5 L8,19 L19,12 Z"); // Play icon
            SimMoveType.Text = "Complete";
            StatusText.Text = "Simulation completed";
            SimulationProgress.Value = 100;
            
            // Final stock update
            UpdateStockRemovalVisual();
            
            LogOutput("✓ Simulation completed");
        });
    }
    
    private void Simulator_OnStart(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PlayPauseIcon.Data = Geometry.Parse("M6,5 L10,5 L10,19 L6,19 Z M14,5 L18,5 L18,19 L14,19 Z"); // Pause icon
            StatusText.Text = "Simulation playing...";
            LogOutput("▶ Simulation started");
            
            // Hide original stock box, show removal simulation
            if (StockBox3D != null) StockBox3D.Visible = false;
            
            // Initialize stock removal mesh
            if (_stockRemoval != null && _currentProject != null)
            {
                _stockRemoval.InitializeStock(
                    _currentProject.Stock.Width,
                    _currentProject.Stock.Height,
                    _currentProject.Stock.Thickness);
                UpdateStockRemovalVisual();
                LogOutput($"  Stock: {_currentProject.Stock.Width} x {_currentProject.Stock.Height} x {_currentProject.Stock.Thickness} mm");
            }
            
            // Start tool position above stock
            double startZ = _currentProject?.Stock.Thickness ?? 20 + 10;
            _lastSimPos = new Point3D(0, 0, startZ);
            _stockUpdateCounter = 0;
            
            // Clear any existing trails
            ClearSimulationTrails();
        });
    }
    
    private void Simulator_OnPause(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PlayPauseIcon.Data = Geometry.Parse("M8,5 L8,19 L19,12 Z"); // Play icon
            StatusText.Text = "Simulation paused";
        });
    }
    
    private void Simulator_OnStop(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            PlayPauseIcon.Data = Geometry.Parse("M8,5 L8,19 L19,12 Z"); // Play icon
            SimMoveType.Text = "Ready";
            SimulationProgress.Value = 0;
            StatusText.Text = "Simulation stopped";
            
            // Clear trail lines
            ClearSimulationTrails();
            
            // Reset stock removal and show original stock box
            if (_stockRemoval != null)
            {
                _stockRemoval.Reset();
                if (CutSimulationVisual3D != null) CutSimulationVisual3D.Content = null;
            }
            if (StockBox3D != null) StockBox3D.Visible = true;
        });
    }
    
    private Point3D? _lastTrailPoint = null;
    private MoveType? _lastMoveType = null;
    
    private void AddSimulationTrailLine(SimulatorUpdateEventArgs args)
    {
        if (_currentProject == null) return;
        
        // Stock center offset
        double offsetX = _currentProject.Stock.Width / 2;
        double offsetY = _currentProject.Stock.Height / 2;
        
        if (_lastTrailPoint == null)
        {
            // Store with offset applied
            _lastTrailPoint = new Point3D(args.Position.X + offsetX, args.Position.Y + offsetY, args.Position.Z);
            _lastMoveType = args.MoveType;
            return;
        }
        
        // Apply offset to current point
        var currentPoint = new Point3D(args.Position.X + offsetX, args.Position.Y + offsetY, args.Position.Z);
        
        // Only add line if we've moved significantly
        var dist = (currentPoint - _lastTrailPoint.Value).Length;
        if (dist < 0.5) return;
        
        // Create line material based on move type
        System.Windows.Media.Color lineColor;
        switch (args.MoveType)
        {
            case MoveType.Rapid: lineColor = Colors.Red; break;
            case MoveType.Plunge: lineColor = Colors.Yellow; break;
            default: lineColor = Colors.Cyan; break;
        }
        
        var lineMaterial = new DiffuseMaterial(new SolidColorBrush(lineColor));
        
        // Create tube for trail using simple geometry
        var trailMesh = CreateSimpleTubeMesh(_lastTrailPoint.Value, currentPoint, 0.3);
        if (trailMesh == null)
        {
            _lastTrailPoint = currentPoint;
            _lastMoveType = args.MoveType;
            return;
        }
        
        var trailGeometry = new GeometryModel3D
        {
            Geometry = trailMesh,
            Material = lineMaterial,
            BackMaterial = lineMaterial
        };
        
        var trailVisual = new ModelVisual3D { Content = trailGeometry };
        trailVisual.SetValue(FrameworkElement.TagProperty, "SimTrail");
        Viewport3D.Children.Add(trailVisual);
        
        _lastTrailPoint = currentPoint;
        _lastMoveType = args.MoveType;
    }
    
    private static MeshGeometry3D? CreateSimpleTubeMesh(Point3D start, Point3D end, double radius)
    {
        var mesh = new MeshGeometry3D();
        
        var dir = end - start;
        if (dir.Length < 0.001) return null;
        dir.Normalize();
        
        var up = new Vector3D(0, 0, 1);
        if (Math.Abs(Vector3D.DotProduct(dir, up)) > 0.9)
            up = new Vector3D(0, 1, 0);
        
        var right = Vector3D.CrossProduct(dir, up);
        right.Normalize();
        up = Vector3D.CrossProduct(right, dir);
        up.Normalize();
        
        int segments = 6;
        for (int i = 0; i < segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            var offset = right * Math.Cos(angle) * radius + up * Math.Sin(angle) * radius;
            mesh.Positions.Add(start + offset);
            mesh.Positions.Add(end + offset);
        }
        
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int i0 = i * 2;
            int i1 = i * 2 + 1;
            int n0 = next * 2;
            int n1 = next * 2 + 1;
            
            mesh.TriangleIndices.Add(i0);
            mesh.TriangleIndices.Add(i1);
            mesh.TriangleIndices.Add(n1);
            
            mesh.TriangleIndices.Add(i0);
            mesh.TriangleIndices.Add(n1);
            mesh.TriangleIndices.Add(n0);
        }
        
        return mesh;
    }
    
    private void ClearSimulationTrails()
    {
        _lastTrailPoint = null;
        _lastMoveType = null;
        
        var toRemove = Viewport3D.Children
            .OfType<ModelVisual3D>()
            .Where(v => v.GetValue(FrameworkElement.TagProperty)?.ToString() == "SimTrail")
            .ToList();
        
        foreach (var item in toRemove)
        {
            Viewport3D.Children.Remove(item);
        }
    }
    
    private void Update3DToolPosition(double x, double y, double z)
    {
        if (_currentProject == null) return;
        
        // Toolpath coordinates are relative to stock center (0,0)
        // 3D viewport uses stock origin (0,0) at bottom-left
        // So we need to add offset to convert from center-origin to stock-origin
        double offsetX = _currentProject.Stock.Width / 2;
        double offsetY = _currentProject.Stock.Height / 2;
        
        // Clamp Z to valid range
        double clampedZ = Math.Max(0, Math.Min(z, _currentProject.Stock.Thickness + 50));
        
        // Find or create tool visual
        var toolVisual = Viewport3D.Children
            .OfType<TruncatedConeVisual3D>()
            .FirstOrDefault(v => v.GetValue(FrameworkElement.TagProperty)?.ToString() == "SimTool");
        
        if (toolVisual == null)
        {
            var tool = _toolpaths.FirstOrDefault()?.Tool;
            double toolRadius = tool?.Diameter / 2 ?? 3;
            double toolHeight = tool?.FluteLength ?? 20;
            
            toolVisual = new TruncatedConeVisual3D
            {
                BaseRadius = toolRadius,
                TopRadius = toolRadius * 0.3,
                Height = toolHeight,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 180, 180, 200)),
                Origin = new Point3D(x + offsetX, y + offsetY, clampedZ)
            };
            toolVisual.SetValue(FrameworkElement.TagProperty, "SimTool");
            Viewport3D.Children.Add(toolVisual);
        }
        else
        {
            // Update position smoothly
            toolVisual.Origin = new Point3D(x + offsetX, y + offsetY, clampedZ);
        }
        
        // Update info display
        if (ToolPosition3D != null)
        {
            ToolPosition3D.Text = $"X:{x:F2} Y:{y:F2} Z:{z:F2}";
        }
    }
    
    private void SimPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_toolpaths.Count == 0)
        {
            MessageBox.Show("No toolpaths to simulate. Create toolpaths first.", "No Toolpaths", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        if (_simulator == null)
        {
            InitializeSimulator();
            LoadToolpathsToSimulator();
        }
        
        if (_simulator!.IsPlaying)
        {
            _simulator.Pause();
        }
        else
        {
            if (_simulator.TotalMoves == 0)
            {
                LoadToolpathsToSimulator();
            }
            _simulator.Play();
        }
    }
    
    private void SimStop_Click(object sender, RoutedEventArgs e)
    {
        _simulator?.Stop();
    }
    
    private void SimRewind_Click(object sender, RoutedEventArgs e)
    {
        _simulator?.Stop();
        ClearSimulationTrails();
    }
    
    private void SimStepBack_Click(object sender, RoutedEventArgs e)
    {
        _simulator?.StepBackward();
    }
    
    private void SimStepForward_Click(object sender, RoutedEventArgs e)
    {
        if (_simulator == null)
        {
            InitializeSimulator();
            LoadToolpathsToSimulator();
        }
        _simulator?.StepForward();
    }
    
    private void SimSpeed_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_simulator == null || SimSpeedCombo.SelectedItem == null) return;
        
        var item = SimSpeedCombo.SelectedItem as ComboBoxItem;
        if (item?.Tag != null && double.TryParse(item.Tag.ToString(), out double speed))
        {
            _simulator.SetSpeed(speed);
        }
    }
    
    #endregion
    
    #region 3D View Controls
    
    private void View3D_Top_Click(object sender, RoutedEventArgs e)
    {
        if (Viewport3D?.Camera is PerspectiveCamera camera)
        {
            var center = GetStockCenter();
            camera.Position = new Point3D(center.X, center.Y, 300);
            camera.LookDirection = new Vector3D(0, 0, -1);
            camera.UpDirection = new Vector3D(0, 1, 0);
        }
    }
    
    private void View3D_Front_Click(object sender, RoutedEventArgs e)
    {
        if (Viewport3D?.Camera is PerspectiveCamera camera)
        {
            var center = GetStockCenter();
            camera.Position = new Point3D(center.X, -200, center.Z);
            camera.LookDirection = new Vector3D(0, 1, 0);
            camera.UpDirection = new Vector3D(0, 0, 1);
        }
    }
    
    private void View3D_Side_Click(object sender, RoutedEventArgs e)
    {
        if (Viewport3D?.Camera is PerspectiveCamera camera)
        {
            var center = GetStockCenter();
            camera.Position = new Point3D(-200, center.Y, center.Z);
            camera.LookDirection = new Vector3D(1, 0, 0);
            camera.UpDirection = new Vector3D(0, 0, 1);
        }
    }
    
    private void View3D_Iso_Click(object sender, RoutedEventArgs e)
    {
        if (Viewport3D?.Camera is PerspectiveCamera camera)
        {
            var center = GetStockCenter();
            camera.Position = new Point3D(center.X + 150, center.Y - 150, center.Z + 150);
            camera.LookDirection = new Vector3D(-1, 1, -1);
            camera.UpDirection = new Vector3D(0, 0, 1);
        }
    }
    
    private void View3D_ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        Viewport3D?.ZoomExtents(500);
    }
    
    private Point3D GetStockCenter()
    {
        if (_currentProject == null) return new Point3D(100, 75, 10);
        
        var stock = _currentProject.Stock;
        return new Point3D(stock.Width / 2, stock.Height / 2, stock.Thickness / 2);
    }
    
    #endregion
    
    #region New Feature Integrations
    
    /// <summary>
    /// Import STL 3D model file - shows in 3D viewport with transform controls
    /// </summary>
    private void ImportSTL_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "STL Files|*.stl|All Files|*.*",
            Title = "Import STL 3D Model"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                LogOutput($"Importing STL: {System.IO.Path.GetFileName(dialog.FileName)}...");
                
                var importer = new StlImporter();
                importer.Import(dialog.FileName);
                
                if (importer.Triangles.Count == 0)
                {
                    MessageBox.Show("STL Import Error: No triangles found in file", "Import Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var model = new StlModel3D
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
                    FilePath = dialog.FileName,
                    Triangles = importer.Triangles.ToList(),
                    MinX = importer.MinX,
                    MaxX = importer.MaxX,
                    MinY = importer.MinY,
                    MaxY = importer.MaxY,
                    MinZ = importer.MinZ,
                    MaxZ = importer.MaxZ
                };
                
                _stlModels.Add(model);
                _selectedStlModel = model;
                
                AddStlModelToProjectTree(model);
                Update3DStlModels();
                
                LogOutput($"STL loaded: {model.Triangles.Count:N0} triangles");
                LogOutput($"  Bounds: {model.Width:F2} x {model.Height:F2} x {model.Depth:F2} mm");
                
                var result = MessageBox.Show(
                    $"STL loaded successfully!\n\n" +
                    $"Triangles: {model.Triangles.Count:N0}\n" +
                    $"Size: {model.Width:F2} x {model.Height:F2} x {model.Depth:F2} mm\n\n" +
                    $"Options:\n" +
                    $"Yes = Slice at Z=0 for 2D CAM paths\n" +
                    $"No = Keep as 3D model only\n" +
                    $"Cancel = Fit to stock bounds",
                    "STL Import Options", 
                    MessageBoxButton.YesNoCancel, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    var layers = importer.SliceAtZ(0.0);
                    if (layers.Count > 0)
                    {
                        AddLayersToProject(layers, model.Name);
                        LogOutput($"Sliced at Z=0: {layers.Sum(l => l.Paths.Count)} paths created");
                    }
                    else
                    {
                        LogOutput("Warning: No intersection at Z=0. Try different Z height.");
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    if (_currentProject != null)
                    {
                        model.FitToStock(_currentProject.Stock.Width, _currentProject.Stock.Height, _currentProject.Stock.Thickness);
                        Update3DStlModels();
                        LogOutput($"Model fitted to stock: Scale {model.Scale.X:F3}");
                    }
                }
                
                SwitchTo3DView();
                Viewport3D?.ZoomExtents(500);
                
                StatusText.Text = $"STL imported: {model.Name} ({model.Triangles.Count:N0} triangles)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import STL: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LogOutput($"ERROR: STL import failed - {ex.Message}");
            }
        }
    }
    
    private void AddStlModelToProjectTree(StlModel3D model)
    {
        if (_currentProject == null) return;
        
        var wcs = _currentProject.Root.Children.FirstOrDefault(c => c is WCSNode) as WCSNode;
        var modelsFolder = wcs?.Children.FirstOrDefault(c => c.Name == "3D Models") as FolderNode;
        
        if (modelsFolder != null)
        {
            var modelNode = new GeometryNode
            {
                Name = model.Name,
                Type = NodeType.Model
            };
            modelsFolder.Children.Add(modelNode);
        }
    }
    
    private void Update3DStlModels()
    {
        if (Viewport3D == null) return;
        
        // Remove existing STL visual
        if (_stlModelVisual != null)
        {
            Viewport3D.Children.Remove(_stlModelVisual);
            _stlModelVisual = null;
        }
        
        if (_stlModels.Count == 0) return;
        
        _stlModelVisual = new ModelVisual3D();
        var modelGroup = new Model3DGroup();
        
        foreach (var stlModel in _stlModels)
        {
            if (!stlModel.IsVisible || stlModel.Triangles.Count == 0) continue;
            
            try
            {
                var mesh = stlModel.CreateMesh();
                var transform = stlModel.GetTransform3D();
                
                // Create material with transparency
                var color = stlModel.IsSelected ? Colors.Yellow : stlModel.Color;
                var materialColor = System.Windows.Media.Color.FromArgb(
                    (byte)(stlModel.Opacity * 255), color.R, color.G, color.B);
                
                var material = new DiffuseMaterial(new SolidColorBrush(materialColor));
                
                var geometryModel = new GeometryModel3D(mesh, material);
                geometryModel.Transform = transform;
                geometryModel.BackMaterial = material;
                
                modelGroup.Children.Add(geometryModel);
            }
            catch (Exception ex)
            {
                LogOutput($"Warning: Failed to render STL model '{stlModel.Name}': {ex.Message}");
            }
        }
        
        if (modelGroup.Children.Count > 0)
        {
            _stlModelVisual.Content = modelGroup;
            Viewport3D.Children.Add(_stlModelVisual);
        }
    }
    
    /// <summary>
    /// Import Gerber PCB file
    /// </summary>
    private void ImportGerber_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Gerber Files|*.gbr;*.ger;*.gtl;*.gbl;*.gts;*.gbs|All Files|*.*",
            Title = "Import Gerber PCB File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var importer = new GerberImporter();
                var layers = importer.Import(dialog.FileName);
                
                if (layers == null || layers.Count == 0)
                {
                    MessageBox.Show("Gerber Import: No data found", "Import Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Convert layers to project structure
                var wcs = _currentProject?.Root.Children.FirstOrDefault(c => c is WCSNode) as WCSNode;
                var layersFolder = wcs?.Children.FirstOrDefault(c => c.Name == "2D Layers") as FolderNode;
                
                int totalObjects = 0;
                foreach (var layer in layers)
                {
                    var layerNode = new LayerNode { Name = layer.Name, Color = layer.Color };
                    
                    int pathIdx = 1;
                    foreach (var path in layer.Paths)
                    {
                        layerNode.Children.Add(new GeometryNode
                        {
                            Name = $"Path {pathIdx++}",
                            PathData = path,
                            IsVisible = true
                        });
                        totalObjects++;
                    }
                    
                    layersFolder?.Children.Add(layerNode);
                }
                
                DrawCanvas();
                StatusText.Text = $"Imported Gerber: {layers.Count} layers, {totalObjects} objects";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import Gerber: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /// <summary>
    /// Import G-Code for backplotting
    /// </summary>
    private void ImportGCode_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "G-Code Files|*.nc;*.ngc;*.gcode;*.tap|All Files|*.*",
            Title = "Import G-Code (Backplot)"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var importer = new GCodeImporter();
                var toolpaths = importer.Import(dialog.FileName);
                
                if (toolpaths == null || toolpaths.Count == 0)
                {
                    MessageBox.Show("G-Code Import: No toolpaths found", "Import Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Add toolpaths to project
                int totalMoves = 0;
                foreach (var tp in toolpaths)
                {
                    _toolpaths.Add(tp);
                    totalMoves += tp.Moves.Count;
                }
                
                DrawCanvas();
                StatusText.Text = $"Imported G-Code: {toolpaths.Count} toolpaths, {totalMoves} moves";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import G-Code: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /// <summary>
    /// Import PDF vector file
    /// </summary>
    private void ImportPDF_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF Files|*.pdf|All Files|*.*",
            Title = "Import PDF Vector Graphics"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var importer = new PdfImporter();
                var result = importer.Import(dialog.FileName);
                
                if (!result.Success)
                {
                    MessageBox.Show($"PDF Import Error: {result.ErrorMessage}", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var contours = importer.ConvertToContours(result);
                AddContoursToProject(contours, System.IO.Path.GetFileNameWithoutExtension(dialog.FileName));
                
                StatusText.Text = $"Imported PDF: {result.Paths.Count} paths";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import PDF: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /// <summary>
    /// Import STEP/IGES CAD file
    /// </summary>
    private void ImportSTEP_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CAD Files|*.step;*.stp;*.iges;*.igs|STEP Files|*.step;*.stp|IGES Files|*.iges;*.igs|All Files|*.*",
            Title = "Import STEP/IGES File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var importer = new StepIgesImporter();
                var result = importer.Import(dialog.FileName);
                
                if (!result.Success)
                {
                    MessageBox.Show($"Import Error: {result.ErrorMessage}", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Ask for slice Z
                var sliceZ = 0.0;
                var sliceResult = MessageBox.Show(
                    $"Loaded {result.DetectedType}: {result.Curves.Count} curves\n\nSlice at Z=0 for 2D CAM?",
                    "Import", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (sliceResult == MessageBoxResult.Yes)
                {
                    var contours = importer.SliceAtZ(result, sliceZ);
                    AddContoursToProject(contours, System.IO.Path.GetFileNameWithoutExtension(dialog.FileName));
                }
                
                StatusText.Text = $"Imported {result.DetectedType}: {result.Curves.Count} curves";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /// <summary>
    /// Import 3MF additive manufacturing file
    /// </summary>
    private void Import3MF_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "3MF Files|*.3mf|All Files|*.*",
            Title = "Import 3MF Model"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var importer = new ThreeMfImporter();
                var result = importer.Import(dialog.FileName);
                
                if (!result.Success)
                {
                    MessageBox.Show($"3MF Import Error: {result.ErrorMessage}", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Slice at Z=0 for 2D
                var slice = importer.SliceAtZ(result, 0.0);
                AddContoursToProject(slice.Contours, System.IO.Path.GetFileNameWithoutExtension(dialog.FileName));
                
                StatusText.Text = $"Imported 3MF: {result.Objects.Count} objects, " +
                    $"Bounds: {result.Bounds.SizeX:F1} x {result.Bounds.SizeY:F1} x {result.Bounds.SizeZ:F1}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import 3MF: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    /// <summary>
    /// Helper method to add contours to project
    /// </summary>
    private void AddContoursToProject(List<List<Point>> contours, string layerName)
    {
        if (_currentProject == null || contours.Count == 0) return;
        
        var wcs = _currentProject.Root.Children.FirstOrDefault(c => c is WCSNode) as WCSNode;
        var layersFolder = wcs?.Children.FirstOrDefault(c => c.Name == "2D Layers") as FolderNode;
        
        if (layersFolder != null)
        {
            var modelNode = new LayerNode
            {
                Name = layerName,
                Color = "#00D4AA",
                IsExpanded = true
            };
            
            int pathIdx = 1;
            foreach (var contour in contours)
            {
                if (contour.Count < 2) continue;
                
                var polyPath = new PolyPath { Name = $"{layerName}_Path{pathIdx}" };
                bool isClosed = contour.Count > 2 && 
                    Math.Abs(contour[0].X - contour[^1].X) < 0.01 &&
                    Math.Abs(contour[0].Y - contour[^1].Y) < 0.01;
                
                // Add segments using LineSegment (concrete type)
                for (int i = 0; i < contour.Count; i++)
                {
                    polyPath.Segments.Add(new Models.LineSegment
                    {
                        EndPoint = new Point2D(contour[i].X, contour[i].Y)
                    });
                }
                
                if (isClosed && contour.Count > 2)
                {
                    // Close the path
                    polyPath.Segments.Add(new Models.LineSegment
                    {
                        EndPoint = new Point2D(contour[0].X, contour[0].Y)
                    });
                }
                
                modelNode.Children.Add(new GeometryNode
                {
                    Name = $"Path {pathIdx++}",
                    PathData = polyPath,
                    IsVisible = true
                });
            }
            
            layersFolder.Children.Add(modelNode);
        }
        
        CenterGeometryOnStock(contours);
        DrawCanvas();
        AutoFitView();
    }
    
    /// <summary>
    /// Helper method to add layers from STL slicing to project
    /// </summary>
    private void AddLayersToProject(List<Layer> layers, string baseName)
    {
        if (_currentProject == null || layers.Count == 0) return;
        
        // Convert Layer's GeometryPath objects to contours and add to project
        var contours = new List<List<Point>>();
        foreach (var layer in layers)
        {
            foreach (var path in layer.Paths)
            {
                if (path is PolyPath polyPath && polyPath.Segments.Count > 0)
                {
                    var contour = new List<Point>();
                    foreach (var seg in polyPath.Segments)
                    {
                        contour.Add(new Point(seg.EndPoint.X, seg.EndPoint.Y));
                    }
                    if (contour.Count > 0)
                    {
                        contours.Add(contour);
                    }
                }
            }
        }
        
        if (contours.Count > 0)
        {
            AddContoursToProject(contours, baseName);
        }
    }
    
    /// <summary>
    /// Center imported geometry on stock
    /// </summary>
    private void CenterGeometryOnStock(List<List<Point>> contours)
    {
        if (_currentProject == null || contours.Count == 0) return;
        
        // Calculate bounds
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        
        foreach (var contour in contours)
        {
            foreach (var pt in contour)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }
        }
        
        double geomWidth = maxX - minX;
        double geomHeight = maxY - minY;
        double geomCenterX = (minX + maxX) / 2;
        double geomCenterY = (minY + maxY) / 2;
        
        double stockCenterX = _currentProject.Stock.Width / 2;
        double stockCenterY = _currentProject.Stock.Height / 2;
        
        // Calculate offset to center on stock
        _canvasOffsetX = stockCenterX - geomCenterX;
        _canvasOffsetY = stockCenterY - geomCenterY;
    }
    
    /// <summary>
    /// Open G-Code Export dialog with post-processor selection
    /// </summary>
    private void OpenGCodeExportDialog_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null || _toolpaths.Count == 0)
        {
            MessageBox.Show("No toolpaths to export. Please create toolpaths first.", 
                "Export G-Code", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var tools = new List<Tool>();
        foreach (var tp in _toolpaths)
        {
            if (!tools.Any(t => t.Name == tp.ToolName))
            {
                tools.Add(new Tool { Name = tp.ToolName, Diameter = tp.ToolDiameter });
            }
        }
        
        var dialog = new GCodeExportDialog(_toolpaths, _currentProject.Stock, tools);
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true)
        {
            StatusText.Text = $"G-Code exported to {dialog.OutputFilePath}";
        }
    }
    
    /// <summary>
    /// Open Grid and Snap settings
    /// </summary>
    private void OpenGridSnapSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new GridSnapSettingsDialog();
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true)
        {
            // Apply settings
            StatusText.Text = "Grid/Snap settings updated";
            DrawCanvas();
        }
    }
    
    /// <summary>
    /// Optimize toolpath order
    /// </summary>
    private void OptimizeToolpaths_Click(object sender, RoutedEventArgs e)
    {
        if (_toolpaths.Count == 0)
        {
            MessageBox.Show("No toolpaths to optimize.", "Optimize", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var optimizer = new ToolpathOptimizer();
        var settings = new OptimizationSettings
        {
            Algorithm = OptimizationAlgorithm.TwoOpt,
            SafeHeight = 10.0
        };
        
        // Optimize toolpath order
        var optimizedToolpaths = optimizer.OptimizeOrder(_toolpaths.ToList(), settings);
        
        _toolpaths.Clear();
        foreach (var tp in optimizedToolpaths)
        {
            _toolpaths.Add(tp);
        }
        
        // Optimize rapid movements within each toolpath
        for (int i = 0; i < _toolpaths.Count; i++)
        {
            if (_toolpaths[i].Moves.Count > 1)
            {
                _toolpaths[i] = optimizer.OptimizeRapids(_toolpaths[i], settings);
            }
        }
        
        DrawCanvas();
        StatusText.Text = "Toolpaths optimized";
    }
    
    /// <summary>
    /// Run collision simulation
    /// </summary>
    private void RunCollisionCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null || _toolpaths.Count == 0)
        {
            MessageBox.Show("No toolpaths to check.", "Collision Check",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var tool = new Tool 
        { 
            Diameter = _toolpaths[0].ToolDiameter,
            TotalLength = 50,
            Length = 50
        };
        
        var stock = new Stock
        {
            Width = _currentProject.Stock.Width,
            Height = _currentProject.Stock.Height,
            Thickness = _currentProject.Stock.Thickness
        };
        
        var simulator = new CollisionSimulator(tool, stock);
        
        var allMoves = _toolpaths.SelectMany(t => t.Moves).ToList();
        var result = simulator.CheckToolpath(allMoves);
        
        var report = simulator.GenerateReport(result);
        
        MessageBox.Show(report, "Collision Check Result",
            MessageBoxButton.OK, 
            result.HasCollision ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }
    
    #endregion
    
    #region Output & Workflow
    
    private void LogOutput(string message)
    {
        if (OutputLog == null) return;
        
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{timestamp}] {message}\n";
        
        Dispatcher.Invoke(() =>
        {
            OutputLog.AppendText(line);
            OutputLog.ScrollToEnd();
        });
    }
    
    #endregion
    
    #region SVG Align & Distribute
    
    public void AlignLeft()
    {
        if (_multiSelection.Count < 2) return;
        double minX = _multiSelection.Min(p => GetPathMinX(p));
        foreach (var p in _multiSelection)
            p.X += minX - GetPathMinX(p);
        DrawCanvas();
        UpdatePropertiesPanel();
        LogOutput($"Aligned {_multiSelection.Count} objects left");
    }
    
    public void AlignCenter()
    {
        if (_multiSelection.Count < 2) return;
        double avgCx = _multiSelection.Average(p => (GetPathMinX(p) + GetPathMaxX(p)) / 2);
        foreach (var p in _multiSelection)
        {
            double cx = (GetPathMinX(p) + GetPathMaxX(p)) / 2;
            p.X += avgCx - cx;
        }
        DrawCanvas();
        UpdatePropertiesPanel();
        LogOutput($"Aligned {_multiSelection.Count} objects center");
    }
    
    public void AlignRight()
    {
        if (_multiSelection.Count < 2) return;
        double maxX = _multiSelection.Max(p => GetPathMaxX(p));
        foreach (var p in _multiSelection)
            p.X += maxX - GetPathMaxX(p);
        DrawCanvas();
        UpdatePropertiesPanel();
        LogOutput($"Aligned {_multiSelection.Count} objects right");
    }
    
    public void AlignTop()
    {
        if (_multiSelection.Count < 2) return;
        double maxY = _multiSelection.Max(p => GetPathMaxY(p));
        foreach (var p in _multiSelection)
            p.Y += maxY - GetPathMaxY(p);
        DrawCanvas();
        UpdatePropertiesPanel();
        LogOutput($"Aligned {_multiSelection.Count} objects top");
    }
    
    public void AlignMiddle()
    {
        if (_multiSelection.Count < 2) return;
        double avgCy = _multiSelection.Average(p => (GetPathMinY(p) + GetPathMaxY(p)) / 2);
        foreach (var p in _multiSelection)
        {
            double cy = (GetPathMinY(p) + GetPathMaxY(p)) / 2;
            p.Y += avgCy - cy;
        }
        DrawCanvas();
        UpdatePropertiesPanel();
        LogOutput($"Aligned {_multiSelection.Count} objects middle");
    }
    
    public void AlignBottom()
    {
        if (_multiSelection.Count < 2) return;
        double minY = _multiSelection.Min(p => GetPathMinY(p));
        foreach (var p in _multiSelection)
            p.Y += minY - GetPathMinY(p);
        DrawCanvas();
        UpdatePropertiesPanel();
        LogOutput($"Aligned {_multiSelection.Count} objects bottom");
    }
    
    public void DistributeHorizontal()
    {
        if (_multiSelection.Count < 3) return;
        var sorted = _multiSelection.OrderBy(p => (GetPathMinX(p) + GetPathMaxX(p)) / 2).ToList();
        double firstCx = (GetPathMinX(sorted.First()) + GetPathMaxX(sorted.First())) / 2;
        double lastCx = (GetPathMinX(sorted.Last()) + GetPathMaxX(sorted.Last())) / 2;
        double spacing = (lastCx - firstCx) / (sorted.Count - 1);
        
        for (int i = 1; i < sorted.Count - 1; i++)
        {
            double cx = (GetPathMinX(sorted[i]) + GetPathMaxX(sorted[i])) / 2;
            double targetCx = firstCx + spacing * i;
            sorted[i].X += targetCx - cx;
        }
        DrawCanvas();
        UpdatePropertiesPanel();
        LogOutput($"Distributed {_multiSelection.Count} objects horizontally");
    }
    
    public void DistributeVertical()
    {
        if (_multiSelection.Count < 3) return;
        var sorted = _multiSelection.OrderBy(p => (GetPathMinY(p) + GetPathMaxY(p)) / 2).ToList();
        double firstCy = (GetPathMinY(sorted.First()) + GetPathMaxY(sorted.First())) / 2;
        double lastCy = (GetPathMinY(sorted.Last()) + GetPathMaxY(sorted.Last())) / 2;
        double spacing = (lastCy - firstCy) / (sorted.Count - 1);
        
        for (int i = 1; i < sorted.Count - 1; i++)
        {
            double cy = (GetPathMinY(sorted[i]) + GetPathMaxY(sorted[i])) / 2;
            double targetCy = firstCy + spacing * i;
            sorted[i].Y += targetCy - cy;
        }
        DrawCanvas();
        UpdatePropertiesPanel();
        LogOutput($"Distributed {_multiSelection.Count} objects vertically");
    }
    
    private double GetPathMinX(GeometryPath p)
    {
        if (p is PolyPath poly && poly.Segments.Count > 0)
            return p.X + poly.Segments.Min(s => s.EndPoint.X) * p.ScaleX;
        return p.X;
    }
    
    private double GetPathMaxX(GeometryPath p)
    {
        if (p is PolyPath poly && poly.Segments.Count > 0)
            return p.X + poly.Segments.Max(s => s.EndPoint.X) * p.ScaleX;
        return p.X;
    }
    
    private double GetPathMinY(GeometryPath p)
    {
        if (p is PolyPath poly && poly.Segments.Count > 0)
            return p.Y + poly.Segments.Min(s => s.EndPoint.Y) * p.ScaleY;
        return p.Y;
    }
    
    private double GetPathMaxY(GeometryPath p)
    {
        if (p is PolyPath poly && poly.Segments.Count > 0)
            return p.Y + poly.Segments.Max(s => s.EndPoint.Y) * p.ScaleY;
        return p.Y;
    }
    
    #endregion
    
    #region STL 3D Transform
    
    public void MoveStlModel(double dx, double dy, double dz)
    {
        if (_selectedStlModel == null) return;
        _selectedStlModel.Position = new Point3D(
            _selectedStlModel.Position.X + dx,
            _selectedStlModel.Position.Y + dy,
            _selectedStlModel.Position.Z + dz);
        Update3DStlModels();
        LogOutput($"STL moved: +{dx:F2}, +{dy:F2}, +{dz:F2}");
    }
    
    public void RotateStlModel(double rx, double ry, double rz)
    {
        if (_selectedStlModel == null) return;
        _selectedStlModel.Rotation = new Vector3D(
            _selectedStlModel.Rotation.X + rx,
            _selectedStlModel.Rotation.Y + ry,
            _selectedStlModel.Rotation.Z + rz);
        Update3DStlModels();
        LogOutput($"STL rotated: +{rx:F1}°, +{ry:F1}°, +{rz:F1}°");
    }
    
    public void ScaleStlModel(double sx, double sy, double sz)
    {
        if (_selectedStlModel == null) return;
        _selectedStlModel.Scale = new Vector3D(sx, sy, sz);
        Update3DStlModels();
        LogOutput($"STL scaled: {sx:F3}x, {sy:F3}x, {sz:F3}x");
    }
    
    public void ScaleStlUniform(double factor)
    {
        if (_selectedStlModel == null) return;
        _selectedStlModel.Scale = new Vector3D(factor, factor, factor);
        Update3DStlModels();
        LogOutput($"STL uniform scale: {factor:F3}x");
    }
    
    public void CenterStlOnStock()
    {
        if (_selectedStlModel == null || _currentProject == null) return;
        _selectedStlModel.CenterOnStock(
            _currentProject.Stock.Width,
            _currentProject.Stock.Height,
            _currentProject.Stock.Thickness);
        Update3DStlModels();
        LogOutput("STL centered on stock");
    }
    
    public void FitStlToStock()
    {
        if (_selectedStlModel == null || _currentProject == null) return;
        _selectedStlModel.FitToStock(
            _currentProject.Stock.Width,
            _currentProject.Stock.Height,
            _currentProject.Stock.Thickness);
        Update3DStlModels();
        LogOutput($"STL fitted to stock: Scale {_selectedStlModel.Scale.X:F3}");
    }
    
    public void ResetStlTransform()
    {
        if (_selectedStlModel == null) return;
        _selectedStlModel.Position = new Point3D(0, 0, 0);
        _selectedStlModel.Rotation = new Vector3D(0, 0, 0);
        _selectedStlModel.Scale = new Vector3D(1, 1, 1);
        Update3DStlModels();
        LogOutput("STL transform reset");
    }
    
    #endregion
}