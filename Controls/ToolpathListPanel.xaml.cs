using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using E3Studio.Models;

namespace E3Studio.Controls;

public partial class ToolpathListPanel : UserControl
{
    public ObservableCollection<ToolpathViewModel> Toolpaths { get; } = new();
    
    public event EventHandler<Toolpath>? ToolpathSelected;
    public event EventHandler<Toolpath>? ToolpathDoubleClicked;
    public event EventHandler? AddToolpathRequested;
    public event EventHandler<Toolpath>? EditToolpathRequested;
    public event EventHandler<Toolpath>? DeleteToolpathRequested;
    public event EventHandler<List<Toolpath>>? ToolpathOrderChanged;
    public event EventHandler<Toolpath>? ToolpathVisibilityChanged;
    
    private Point _dragStartPoint;
    private bool _isDragging = false;
    
    public ToolpathListPanel()
    {
        InitializeComponent();
        ToolpathList.ItemsSource = Toolpaths;
    }
    
    public void SetToolpaths(IEnumerable<Toolpath> toolpaths)
    {
        Toolpaths.Clear();
        foreach (var tp in toolpaths)
        {
            Toolpaths.Add(new ToolpathViewModel(tp));
        }
    }
    
    public void AddToolpath(Toolpath toolpath)
    {
        Toolpaths.Add(new ToolpathViewModel(toolpath));
    }
    
    public void RemoveToolpath(Toolpath toolpath)
    {
        var vm = Toolpaths.FirstOrDefault(t => t.Toolpath == toolpath);
        if (vm != null)
        {
            Toolpaths.Remove(vm);
        }
    }
    
    public void UpdateToolpath(Toolpath toolpath)
    {
        var vm = Toolpaths.FirstOrDefault(t => t.Toolpath == toolpath);
        if (vm != null)
        {
            vm.Update();
        }
    }
    
    public Toolpath? GetSelectedToolpath()
    {
        return (ToolpathList.SelectedItem as ToolpathViewModel)?.Toolpath;
    }
    
    public IEnumerable<Toolpath> GetSelectedToolpaths()
    {
        return ToolpathList.SelectedItems.Cast<ToolpathViewModel>().Select(vm => vm.Toolpath);
    }
    
    private void ToolpathList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = GetSelectedToolpath();
        if (selected != null)
        {
            ToolpathSelected?.Invoke(this, selected);
        }
    }
    
    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        AddToolpathRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedToolpath();
        if (selected != null)
        {
            EditToolpathRequested?.Invoke(this, selected);
        }
    }
    
    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedToolpath();
        if (selected != null)
        {
            var result = MessageBox.Show(
                $"Delete toolpath '{selected.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                DeleteToolpathRequested?.Invoke(this, selected);
            }
        }
    }
    
    private void BtnReorder_Click(object sender, RoutedEventArgs e)
    {
        // Show reorder context menu
        var menu = new ContextMenu();
        
        var moveUp = new MenuItem { Header = "Move Up" };
        moveUp.Click += (s, args) => MoveSelectedToolpath(-1);
        menu.Items.Add(moveUp);
        
        var moveDown = new MenuItem { Header = "Move Down" };
        moveDown.Click += (s, args) => MoveSelectedToolpath(1);
        menu.Items.Add(moveDown);
        
        menu.Items.Add(new Separator());
        
        var moveTop = new MenuItem { Header = "Move to Top" };
        moveTop.Click += (s, args) => MoveSelectedToolpathToPosition(0);
        menu.Items.Add(moveTop);
        
        var moveBottom = new MenuItem { Header = "Move to Bottom" };
        moveBottom.Click += (s, args) => MoveSelectedToolpathToPosition(Toolpaths.Count - 1);
        menu.Items.Add(moveBottom);
        
        menu.IsOpen = true;
    }
    
    private void MoveSelectedToolpath(int direction)
    {
        var vm = ToolpathList.SelectedItem as ToolpathViewModel;
        if (vm == null) return;
        
        int index = Toolpaths.IndexOf(vm);
        int newIndex = index + direction;
        
        if (newIndex >= 0 && newIndex < Toolpaths.Count)
        {
            Toolpaths.Move(index, newIndex);
            NotifyOrderChanged();
        }
    }
    
    private void MoveSelectedToolpathToPosition(int position)
    {
        var vm = ToolpathList.SelectedItem as ToolpathViewModel;
        if (vm == null) return;
        
        int index = Toolpaths.IndexOf(vm);
        if (index != position)
        {
            Toolpaths.Move(index, position);
            NotifyOrderChanged();
        }
    }
    
    private void NotifyOrderChanged()
    {
        var orderedList = Toolpaths.Select(vm => vm.Toolpath).ToList();
        ToolpathOrderChanged?.Invoke(this, orderedList);
    }
    
    private void Visibility_Changed(object sender, RoutedEventArgs e)
    {
        var checkbox = sender as CheckBox;
        var vm = (checkbox?.DataContext as ToolpathViewModel);
        if (vm != null)
        {
            ToolpathVisibilityChanged?.Invoke(this, vm.Toolpath);
        }
    }
    
    private void ContextMenu_Click(object sender, MouseButtonEventArgs e)
    {
        var textBlock = sender as TextBlock;
        var vm = textBlock?.DataContext as ToolpathViewModel;
        if (vm == null) return;
        
        var menu = new ContextMenu();
        
        var edit = new MenuItem { Header = "Edit..." };
        edit.Click += (s, args) => EditToolpathRequested?.Invoke(this, vm.Toolpath);
        menu.Items.Add(edit);
        
        var duplicate = new MenuItem { Header = "Duplicate" };
        duplicate.Click += (s, args) => DuplicateToolpath(vm.Toolpath);
        menu.Items.Add(duplicate);
        
        menu.Items.Add(new Separator());
        
        var regenerate = new MenuItem { Header = "Regenerate" };
        regenerate.Click += (s, args) => RegenerateToolpath(vm.Toolpath);
        menu.Items.Add(regenerate);
        
        menu.Items.Add(new Separator());
        
        var delete = new MenuItem { Header = "Delete", Foreground = Brushes.Red };
        delete.Click += (s, args) => DeleteToolpathRequested?.Invoke(this, vm.Toolpath);
        menu.Items.Add(delete);
        
        menu.IsOpen = true;
    }
    
    private void DuplicateToolpath(Toolpath original)
    {
        // Clone toolpath
        var copy = new Toolpath
        {
            Name = $"{original.Name} (Copy)",
            Type = original.Type,
            ToolId = original.ToolId,
            Tool = original.Tool,
            CutDepth = original.CutDepth,
            StepDown = original.StepDown,
            FeedRate = original.FeedRate,
            PlungeRate = original.PlungeRate,
            SpindleRPM = original.SpindleRPM,
            Offset = original.Offset,
            ClimbCut = original.ClimbCut,
            Moves = new List<ToolpathMove>(original.Moves)
        };
        
        AddToolpath(copy);
    }
    
    private void RegenerateToolpath(Toolpath toolpath)
    {
        // This would trigger recalculation
        MessageBox.Show("Regenerate toolpath: " + toolpath.Name);
    }
    
    private void BtnExpandAll_Click(object sender, RoutedEventArgs e)
    {
        // For tree-style expansion if implemented
    }
    
    private void BtnCollapseAll_Click(object sender, RoutedEventArgs e)
    {
        // For tree-style collapse if implemented
    }
    
    // Drag and drop support
    private void ToolpathList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        
        var point = e.GetPosition(null);
        var diff = _dragStartPoint - point;
        
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var listBox = sender as ListBox;
            var item = ToolpathList.SelectedItem;
            
            if (item != null && !_isDragging)
            {
                _isDragging = true;
                DragDrop.DoDragDrop(listBox!, item, DragDropEffects.Move);
                _isDragging = false;
            }
        }
    }
    
    private void ToolpathList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ToolpathViewModel))) return;
        
        var droppedData = e.Data.GetData(typeof(ToolpathViewModel)) as ToolpathViewModel;
        if (droppedData == null) return;
        
        var target = GetItemAtPosition(e.GetPosition(ToolpathList));
        if (target == null || target == droppedData) return;
        
        int removeIndex = Toolpaths.IndexOf(droppedData);
        int targetIndex = Toolpaths.IndexOf(target);
        
        if (removeIndex < targetIndex)
        {
            Toolpaths.Insert(targetIndex + 1, droppedData);
            Toolpaths.RemoveAt(removeIndex);
        }
        else
        {
            Toolpaths.RemoveAt(removeIndex);
            Toolpaths.Insert(targetIndex, droppedData);
        }
        
        NotifyOrderChanged();
    }
    
    private ToolpathViewModel? GetItemAtPosition(Point position)
    {
        var element = ToolpathList.InputHitTest(position) as DependencyObject;
        while (element != null && element != ToolpathList)
        {
            if (element is ListBoxItem item)
            {
                return item.Content as ToolpathViewModel;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}

public class ToolpathViewModel : ViewModelBase
{
    public Toolpath Toolpath { get; }
    
    public string Name => Toolpath.Name;
    public bool IsVisible
    {
        get => Toolpath.IsVisible;
        set
        {
            Toolpath.IsVisible = value;
            OnPropertyChanged();
        }
    }
    
    public string TypeIcon => Toolpath.Type switch
    {
        ToolpathType.Profile => "⬡",
        ToolpathType.Pocket => "▢",
        ToolpathType.Drill => "●",
        ToolpathType.VCarve => "V",
        ToolpathType.Engrave => "E",
        _ => "?"
    };
    
    public Brush IconColor => Toolpath.Type switch
    {
        ToolpathType.Profile => new SolidColorBrush(Color.FromRgb(0, 180, 130)),
        ToolpathType.Pocket => new SolidColorBrush(Color.FromRgb(180, 130, 0)),
        ToolpathType.Drill => new SolidColorBrush(Color.FromRgb(180, 0, 130)),
        ToolpathType.VCarve => new SolidColorBrush(Color.FromRgb(130, 0, 180)),
        ToolpathType.Engrave => new SolidColorBrush(Color.FromRgb(0, 130, 180)),
        _ => Brushes.Gray
    };
    
    public string ToolName => Toolpath.Tool?.Name ?? $"Tool #{Toolpath.ToolId}";
    public string DepthInfo => $"{Toolpath.CutDepth:F2}mm";
    public bool IsCalculated => Toolpath.Moves.Count > 0;
    
    public ToolpathViewModel(Toolpath toolpath)
    {
        Toolpath = toolpath;
    }
    
    public void Update()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(ToolName));
        OnPropertyChanged(nameof(DepthInfo));
        OnPropertyChanged(nameof(IsCalculated));
    }
}

public class ViewModelBase : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
