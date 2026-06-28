using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using E3Studio.Models;
using E3Studio.Services;

namespace E3Studio.Dialogs;

public partial class ToolLibraryDialog : Window
{
    private List<Tool> _tools = new();
    private Tool? _selectedTool;
    
    public ToolLibraryDialog()
    {
        InitializeComponent();
        LoadTools();
    }
    
    private void LoadTools()
    {
        _tools = SettingsManager.Current?.Tools ?? GetDefaultTools();
        RefreshToolList();
    }
    
    private List<Tool> GetDefaultTools()
    {
        return new List<Tool>
        {
            new Tool { Id = 1, Number = 1, Name = "Flat Endmill 3.175mm (1/8\")", Type = ToolType.Endmill, Diameter = 3.175, Flutes = 2, FluteLength = 10, TotalLength = 38 },
            new Tool { Id = 2, Number = 2, Name = "Flat Endmill 6mm", Type = ToolType.Endmill, Diameter = 6, Flutes = 2, FluteLength = 20, TotalLength = 50 },
            new Tool { Id = 3, Number = 3, Name = "Ball Nose 3.175mm", Type = ToolType.BallNose, Diameter = 3.175, Flutes = 2, FluteLength = 12, TotalLength = 38 },
            new Tool { Id = 4, Number = 4, Name = "Ball Nose 6mm", Type = ToolType.BallNose, Diameter = 6, Flutes = 2, FluteLength = 20, TotalLength = 50 },
            new Tool { Id = 5, Number = 5, Name = "V-Bit 60° 6mm", Type = ToolType.VBit, Diameter = 6, VAngle = 60, Flutes = 1, FluteLength = 15, TotalLength = 40 },
            new Tool { Id = 6, Number = 6, Name = "V-Bit 90° 6mm", Type = ToolType.VBit, Diameter = 6, VAngle = 90, Flutes = 1, FluteLength = 15, TotalLength = 40 },
            new Tool { Id = 7, Number = 7, Name = "Drill Bit 3mm", Type = ToolType.Drill, Diameter = 3, Flutes = 2, FluteLength = 15, TotalLength = 40 },
            new Tool { Id = 8, Number = 8, Name = "Drill Bit 6mm", Type = ToolType.Drill, Diameter = 6, Flutes = 2, FluteLength = 20, TotalLength = 50 },
            new Tool { Id = 9, Number = 9, Name = "Engraver 0.2mm", Type = ToolType.Engraver, Diameter = 0.2, VAngle = 30, Flutes = 1, FluteLength = 5, TotalLength = 30 },
        };
    }
    
    private void RefreshToolList()
    {
        if (ToolList == null || ToolTypeFilter == null) return;
        
        ToolList.Items.Clear();
        
        var filter = (ToolTypeFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Tools";
        
        var filteredTools = filter switch
        {
            "Endmills" => _tools.Where(t => t.Type == ToolType.Endmill),
            "Ball Nose" => _tools.Where(t => t.Type == ToolType.BallNose),
            "V-Bits" => _tools.Where(t => t.Type == ToolType.VBit),
            "Drills" => _tools.Where(t => t.Type == ToolType.Drill),
            _ => _tools
        };
        
        foreach (var tool in filteredTools)
        {
            var item = new ListBoxItem { Tag = tool, Content = CreateToolItem(tool) };
            ToolList.Items.Add(item);
        }
        
        if (ToolList.Items.Count > 0)
        {
            ToolList.SelectedIndex = 0;
        }
    }
    
    private StackPanel CreateToolItem(Tool tool)
    {
        var panel = new StackPanel();
        
        var nameText = new TextBlock 
        { 
            Text = tool.Name, 
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)FindResource("TextBrush")
        };
        panel.Children.Add(nameText);
        
        var detailText = new TextBlock
        {
            Text = $"{tool.Flutes} Flute • {tool.FluteLength}mm DOC • Ø{tool.Diameter}mm",
            Foreground = (Brush)FindResource("TextLowBrush"),
            FontSize = 10
        };
        panel.Children.Add(detailText);
        
        return panel;
    }
    
    private void ToolList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ToolList.SelectedItem is ListBoxItem item && item.Tag is Tool tool)
        {
            _selectedTool = tool;
            LoadToolDetails(tool);
        }
    }
    
    private void LoadToolDetails(Tool tool)
    {
        TxtToolName.Text = tool.Name;
        
        // Select type
        ToolTypeCombo.SelectedIndex = tool.Type switch
        {
            ToolType.Endmill => 0,
            ToolType.BallNose => 1,
            ToolType.VBit => 2,
            ToolType.Drill => 3,
            ToolType.Engraver => 4,
            _ => 0
        };
        
        TxtDiameter.Text = tool.Diameter.ToString("F3");
        TxtFlutes.Text = tool.Flutes.ToString();
        TxtFluteLength.Text = tool.FluteLength.ToString("F1");
        TxtTotalLength.Text = tool.TotalLength.ToString("F1");
        TxtVAngle.Text = tool.VAngle.ToString("F0");
        TxtNotes.Text = tool.Notes;
        
        // Show/hide V-angle field
        VAnglePanel.Visibility = tool.Type == ToolType.VBit || tool.Type == ToolType.Engraver 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        
        // Update preview
        UpdateToolPreview(tool);
    }
    
    private void UpdateToolPreview(Tool tool)
    {
        PreviewCanvas.Children.Clear();
        
        double scale = 80.0 / Math.Max(tool.TotalLength, 1);
        double shankWidth = Math.Max(tool.Diameter * scale * 0.6, 8);
        double fluteWidth = Math.Max(tool.Diameter * scale, 10);
        double shankHeight = (tool.TotalLength - tool.FluteLength) * scale;
        double fluteHeight = tool.FluteLength * scale;
        
        double centerX = 40;
        double topY = 10;
        
        // Shank
        var shank = new System.Windows.Shapes.Rectangle
        {
            Width = shankWidth,
            Height = shankHeight,
            Fill = (Brush)FindResource("TextMutedBrush"),
            RadiusX = 2,
            RadiusY = 2
        };
        Canvas.SetLeft(shank, centerX - shankWidth / 2);
        Canvas.SetTop(shank, topY);
        PreviewCanvas.Children.Add(shank);
        
        // Flute
        var flute = new System.Windows.Shapes.Rectangle
        {
            Width = fluteWidth,
            Height = fluteHeight,
            Fill = (Brush)FindResource("AccentBrush"),
            RadiusX = 3,
            RadiusY = 3
        };
        Canvas.SetLeft(flute, centerX - fluteWidth / 2);
        Canvas.SetTop(flute, topY + shankHeight);
        PreviewCanvas.Children.Add(flute);
        
        // Tip based on type
        if (tool.Type == ToolType.BallNose)
        {
            var tip = new System.Windows.Shapes.Ellipse
            {
                Width = fluteWidth,
                Height = fluteWidth / 2,
                Fill = (Brush)FindResource("AccentBrush")
            };
            Canvas.SetLeft(tip, centerX - fluteWidth / 2);
            Canvas.SetTop(tip, topY + shankHeight + fluteHeight - fluteWidth / 4);
            PreviewCanvas.Children.Add(tip);
        }
        else if (tool.Type == ToolType.VBit || tool.Type == ToolType.Engraver)
        {
            double tipHeight = fluteWidth * 0.6;
            var tip = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse($"M0,0 L{fluteWidth / 2},{tipHeight} L{fluteWidth},0 Z"),
                Fill = (Brush)FindResource("AccentBrush")
            };
            Canvas.SetLeft(tip, centerX - fluteWidth / 2);
            Canvas.SetTop(tip, topY + shankHeight + fluteHeight);
            PreviewCanvas.Children.Add(tip);
        }
        else if (tool.Type == ToolType.Drill)
        {
            double tipHeight = fluteWidth * 0.4;
            var tip = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse($"M0,0 L{fluteWidth / 2},{tipHeight} L{fluteWidth},0 Z"),
                Fill = (Brush)FindResource("AccentBrush")
            };
            Canvas.SetLeft(tip, centerX - fluteWidth / 2);
            Canvas.SetTop(tip, topY + shankHeight + fluteHeight);
            PreviewCanvas.Children.Add(tip);
        }
        
        // Diameter label
        var diameterLabel = new TextBlock
        {
            Text = $"Ø{tool.Diameter}mm",
            FontSize = 9,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Canvas.SetLeft(diameterLabel, centerX - 25);
        Canvas.SetTop(diameterLabel, topY + shankHeight + fluteHeight + 15);
        PreviewCanvas.Children.Add(diameterLabel);
    }
    
    private void ToolTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshToolList();
    }
    
    private void AddTool_Click(object sender, RoutedEventArgs e)
    {
        var newTool = new Tool
        {
            Id = _tools.Count > 0 ? _tools.Max(t => t.Id) + 1 : 1,
            Number = _tools.Count > 0 ? _tools.Max(t => t.Number) + 1 : 1,
            Name = $"New Tool {_tools.Count + 1}",
            Type = ToolType.Endmill,
            Diameter = 3.175,
            Flutes = 2,
            FluteLength = 10,
            TotalLength = 38
        };
        
        _tools.Add(newTool);
        RefreshToolList();
        ToolList.SelectedIndex = ToolList.Items.Count - 1;
    }
    
    private void DuplicateTool_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTool == null) return;
        
        var duplicate = new Tool
        {
            Id = _tools.Max(t => t.Id) + 1,
            Number = _tools.Max(t => t.Number) + 1,
            Name = _selectedTool.Name + " (Copy)",
            Type = _selectedTool.Type,
            Diameter = _selectedTool.Diameter,
            Flutes = _selectedTool.Flutes,
            FluteLength = _selectedTool.FluteLength,
            TotalLength = _selectedTool.TotalLength,
            VAngle = _selectedTool.VAngle,
            Notes = _selectedTool.Notes
        };
        
        _tools.Add(duplicate);
        RefreshToolList();
        ToolList.SelectedIndex = ToolList.Items.Count - 1;
    }
    
    private void DeleteTool_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTool == null) return;
        
        var result = MessageBox.Show($"Delete tool '{_selectedTool.Name}'?", "Confirm Delete", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _tools.Remove(_selectedTool);
            _selectedTool = null;
            RefreshToolList();
        }
    }
    
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTool != null)
        {
            _selectedTool.Name = TxtToolName.Text;
            _selectedTool.Diameter = double.TryParse(TxtDiameter.Text, out var d) ? d : _selectedTool.Diameter;
            _selectedTool.Flutes = int.TryParse(TxtFlutes.Text, out var f) ? f : _selectedTool.Flutes;
            _selectedTool.FluteLength = double.TryParse(TxtFluteLength.Text, out var fl) ? fl : _selectedTool.FluteLength;
            _selectedTool.TotalLength = double.TryParse(TxtTotalLength.Text, out var tl) ? tl : _selectedTool.TotalLength;
            _selectedTool.VAngle = double.TryParse(TxtVAngle.Text, out var va) ? va : _selectedTool.VAngle;
            _selectedTool.Notes = TxtNotes.Text;
            
            _selectedTool.Type = ToolTypeCombo.SelectedIndex switch
            {
                0 => ToolType.Endmill,
                1 => ToolType.BallNose,
                2 => ToolType.VBit,
                3 => ToolType.Drill,
                4 => ToolType.Engraver,
                _ => ToolType.Endmill
            };
        }
        
        // Save to settings
        if (SettingsManager.Current != null)
        {
            SettingsManager.Current.Tools = _tools;
            SettingsManager.Save();
        }
        
        RefreshToolList();
    }
    
    private void ImportTools_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Tool Files (*.tools)|*.tools|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Tool Library"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var imported = JsonSerializer.Deserialize<List<Tool>>(json);
                if (imported == null || imported.Count == 0)
                    throw new InvalidOperationException("No tools found in file.");
                _tools.AddRange(imported);
                if (SettingsManager.Current != null)
                    SettingsManager.Current.Tools = _tools;
                RefreshToolList();
                MessageBox.Show($"Imported {imported.Count} tool(s).", "Import Tools",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import tools: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public enum ToolTypeFilter
{
    All,
    Endmills,
    BallNose,
    VBits,
    Drills
}
