using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace E3Studio.Controls;

public partial class LayerPanel : UserControl
{
    public ObservableCollection<LayerViewModel> Layers { get; } = new();
    
    public event EventHandler<LayerEventArgs>? LayerVisibilityChanged;
    public event EventHandler<LayerEventArgs>? LayerSelectionChanged;
    public event EventHandler<LayerEventArgs>? LayerColorChanged;
    public event EventHandler<LayerEventArgs>? LayerLockChanged;
    public event EventHandler<LayerEventArgs>? LayerAdded;
    public event EventHandler<LayerEventArgs>? LayerDeleted;
    public event EventHandler<LayerMergeEventArgs>? LayersMerged;
    public event EventHandler<LayerOrderEventArgs>? LayerOrderChanged;
    
    private Point _dragStartPoint;
    private LayerViewModel? _draggedItem;
    
    public LayerPanel()
    {
        // Register converter
        Resources.Add("VisibilityToForegroundConverter", new VisibilityToForegroundConverter());
        
        InitializeComponent();
        
        LayerList.ItemsSource = Layers;
        
        // Add default layers
        AddDefaultLayers();
    }
    
    private void AddDefaultLayers()
    {
        Layers.Add(new LayerViewModel 
        { 
            Name = "Layer 0", 
            Color = Colors.White, 
            IsVisible = true,
            ObjectCount = 0
        });
    }
    
    public LayerViewModel? SelectedLayer => LayerList.SelectedItem as LayerViewModel;
    
    public void AddLayer(string name, Color color)
    {
        var layer = new LayerViewModel
        {
            Name = name,
            Color = color,
            IsVisible = true,
            ObjectCount = 0
        };
        
        Layers.Add(layer);
        LayerList.SelectedItem = layer;
        
        LayerAdded?.Invoke(this, new LayerEventArgs(layer));
    }
    
    public void UpdateLayerObjectCount(string layerName, int count)
    {
        var layer = Layers.FirstOrDefault(l => l.Name == layerName);
        if (layer != null)
        {
            layer.ObjectCount = count;
        }
    }
    
    private void BtnAddLayer_Click(object sender, RoutedEventArgs e)
    {
        var newIndex = Layers.Count;
        var colors = new[] { Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow, 
            Colors.Orange, Colors.Purple, Colors.Cyan, Colors.Magenta };
        var color = colors[newIndex % colors.Length];
        
        AddLayer($"Layer {newIndex}", color);
    }
    
    private void BtnDeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        if (LayerList.SelectedItem is LayerViewModel layer)
        {
            if (Layers.Count <= 1)
            {
                MessageBox.Show("Cannot delete the last layer.", "Delete Layer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var result = MessageBox.Show($"Delete layer '{layer.Name}' and all its objects?",
                "Delete Layer", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                Layers.Remove(layer);
                LayerDeleted?.Invoke(this, new LayerEventArgs(layer));
            }
        }
    }
    
    private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayerList.SelectedItem is LayerViewModel layer)
        {
            LayerSelectionChanged?.Invoke(this, new LayerEventArgs(layer));
        }
    }
    
    private void Visibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is LayerViewModel layer)
        {
            LayerVisibilityChanged?.Invoke(this, new LayerEventArgs(layer));
        }
    }
    
    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is LayerViewModel layer)
        {
            LayerLockChanged?.Invoke(this, new LayerEventArgs(layer));
        }
    }
    
    private void ColorMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string colorStr)
        {
            // Find the layer from context menu's placement target
            var contextMenu = menuItem.Parent as ContextMenu;
            var rect = contextMenu?.PlacementTarget as Rectangle;
            var layer = rect?.DataContext as LayerViewModel;
            
            if (layer != null)
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                layer.Color = color;
                LayerColorChanged?.Invoke(this, new LayerEventArgs(layer));
            }
        }
    }
    
    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var layer in Layers)
        {
            layer.IsVisible = true;
        }
        LayerVisibilityChanged?.Invoke(this, new LayerEventArgs(null!));
    }
    
    private void HideAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var layer in Layers)
        {
            layer.IsVisible = false;
        }
        LayerVisibilityChanged?.Invoke(this, new LayerEventArgs(null!));
    }
    
    private void Merge_Click(object sender, RoutedEventArgs e)
    {
        // Get selected layers (if multi-select is enabled) or prompt
        if (LayerList.SelectedItem is LayerViewModel targetLayer)
        {
            if (Layers.Count < 2)
            {
                MessageBox.Show("Need at least 2 layers to merge.", "Merge Layers",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // For now, merge with next layer
            var index = Layers.IndexOf(targetLayer);
            if (index < Layers.Count - 1)
            {
                var sourceLayer = Layers[index + 1];
                
                LayersMerged?.Invoke(this, new LayerMergeEventArgs(sourceLayer, targetLayer));
                
                targetLayer.ObjectCount += sourceLayer.ObjectCount;
                Layers.Remove(sourceLayer);
            }
        }
    }
    
    #region Drag and Drop
    
    private void LayerList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }
    
    private void LayerList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        
        var mousePos = e.GetPosition(null);
        var diff = _dragStartPoint - mousePos;
        
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (listBoxItem != null)
            {
                _draggedItem = listBoxItem.Content as LayerViewModel;
                if (_draggedItem != null)
                {
                    DragDrop.DoDragDrop(listBoxItem, _draggedItem, DragDropEffects.Move);
                }
            }
        }
    }
    
    private void LayerList_Drop(object sender, DragEventArgs e)
    {
        if (_draggedItem == null) return;
        
        var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (targetItem != null)
        {
            var targetLayer = targetItem.Content as LayerViewModel;
            if (targetLayer != null && targetLayer != _draggedItem)
            {
                var oldIndex = Layers.IndexOf(_draggedItem);
                var newIndex = Layers.IndexOf(targetLayer);
                
                Layers.Move(oldIndex, newIndex);
                
                LayerOrderChanged?.Invoke(this, new LayerOrderEventArgs(_draggedItem, oldIndex, newIndex));
            }
        }
        
        _draggedItem = null;
    }
    
    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T t) return t;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
    
    #endregion
}

public class LayerViewModel : INotifyPropertyChanged
{
    private string _name = "";
    private Color _color;
    private bool _isVisible = true;
    private bool _isLocked;
    private int _objectCount;
    
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }
    
    public Color Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(nameof(Color)); }
    }
    
    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
    }
    
    public bool IsLocked
    {
        get => _isLocked;
        set { _isLocked = value; OnPropertyChanged(nameof(IsLocked)); }
    }
    
    public int ObjectCount
    {
        get => _objectCount;
        set { _objectCount = value; OnPropertyChanged(nameof(ObjectCount)); }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class LayerEventArgs : EventArgs
{
    public LayerViewModel Layer { get; }
    public LayerEventArgs(LayerViewModel layer) => Layer = layer;
}

public class LayerMergeEventArgs : EventArgs
{
    public LayerViewModel SourceLayer { get; }
    public LayerViewModel TargetLayer { get; }
    public LayerMergeEventArgs(LayerViewModel source, LayerViewModel target)
    {
        SourceLayer = source;
        TargetLayer = target;
    }
}

public class LayerOrderEventArgs : EventArgs
{
    public LayerViewModel Layer { get; }
    public int OldIndex { get; }
    public int NewIndex { get; }
    public LayerOrderEventArgs(LayerViewModel layer, int oldIndex, int newIndex)
    {
        Layer = layer;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }
}

public class VisibilityToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool visible && visible 
            ? new SolidColorBrush(Colors.White) 
            : new SolidColorBrush(Color.FromRgb(128, 128, 128));
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
