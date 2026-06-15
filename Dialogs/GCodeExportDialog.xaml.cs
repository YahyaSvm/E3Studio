using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using E3Studio.Models;
using E3Studio.Services.PostProcessor;
using Microsoft.Win32;

namespace E3Studio.Dialogs;

public partial class GCodeExportDialog : Window
{
    private readonly List<Toolpath> _toolpaths;
    private readonly Stock _stock;
    private readonly List<Tool> _tools;
    
    public string GeneratedGCode { get; private set; } = "";
    public string OutputFilePath { get; private set; } = "";
    
    private readonly Dictionary<string, string> _postDescriptions = new()
    {
        ["Generic"] = "Generic Fanuc-style G-code compatible with most controllers",
        ["Grbl"] = "GRBL 1.1+ compatible. Supports spindle PWM, real-time status, and soft limits",
        ["Mach3"] = "Mach3 compatible with tool change macros and custom M-codes",
        ["Mach4"] = "Mach4 compatible with advanced scripting support",
        ["LinuxCNC"] = "LinuxCNC (EMC2) compatible with tool table support",
        ["Fanuc"] = "Fanuc CNC compatible with canned cycles and subprograms",
        ["Haas"] = "HAAS NGC compatible with setting variables and coolant control",
        ["Mazak"] = "Mazak Mazatrol compatible format"
    };
    
    public GCodeExportDialog(List<Toolpath> toolpaths, Stock stock, List<Tool> tools)
    {
        InitializeComponent();
        
        _toolpaths = toolpaths;
        _stock = stock;
        _tools = tools;
        
        // Set default output path
        OutputPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\output.nc";
    }
    
    private void PostProcessorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = (PostProcessorCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        if (selected != null && _postDescriptions.TryGetValue(selected, out var desc))
        {
            PostDescription.Text = desc;
        }
        
        // Update file extension based on post
        FileExtension.Text = selected switch
        {
            "Grbl" => ".nc",
            "LinuxCNC" => ".ngc",
            "Fanuc" => ".nc",
            "Haas" => ".nc",
            "Mazak" => ".eia",
            _ => ".nc"
        };
    }
    
    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "G-Code Files (*.nc;*.ngc;*.gcode)|*.nc;*.ngc;*.gcode|All Files (*.*)|*.*",
            DefaultExt = FileExtension.Text.TrimStart('.'),
            FileName = Path.GetFileName(OutputPath.Text)
        };
        
        if (dialog.ShowDialog() == true)
        {
            OutputPath.Text = dialog.FileName;
        }
    }
    
    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            GeneratedGCode = GenerateGCode();
            
            // Show first 200 lines in preview
            var lines = GeneratedGCode.Split('\n');
            var preview = string.Join("\n", lines.Take(200));
            if (lines.Length > 200)
            {
                preview += $"\n\n... ({lines.Length - 200} more lines)";
            }
            
            GCodePreview.Text = preview;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generating G-code: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(OutputPath.Text))
        {
            MessageBox.Show("Please specify an output file.", "Export",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        try
        {
            GeneratedGCode = GenerateGCode();
            
            // Write to file
            var lineEnding = ((ComboBoxItem)LineEndingCombo.SelectedItem).Content.ToString()!.Contains("Windows") 
                ? "\r\n" : "\n";
            var content = GeneratedGCode.Replace("\n", lineEnding);
            
            File.WriteAllText(OutputPath.Text, content);
            OutputFilePath = OutputPath.Text;
            
            MessageBox.Show($"G-code exported successfully!\n\n{OutputPath.Text}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting G-code: {ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private string GenerateGCode()
    {
        // Get selected post-processor
        var postType = (PostProcessorCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Generic";
        var post = PostProcessorFactory.Create(postType);
        
        // Configure options
        var options = GetExportOptions();
        
        // Generate G-code
        return post.Generate(_toolpaths, _stock, _tools, options);
    }
    
    private PostProcessorOptions GetExportOptions()
    {
        int precision = PrecisionCombo.SelectedIndex switch
        {
            0 => 2,
            1 => 3,
            2 => 4,
            _ => 3
        };
        
        double.TryParse(SafeHeight.Text, out double safeHeight);
        
        return new PostProcessorOptions
        {
            UseMetric = ((ComboBoxItem)UnitsCombo.SelectedItem).Content.ToString()!.Contains("Metric"),
            IncludeLineNumbers = ChkLineNumbers.IsChecked == true,
            IncludeComments = ChkComments.IsChecked == true,
            PauseOnToolChange = ChkToolChangePause.IsChecked == true,
            SpindleDelay = ChkSpindleDelay.IsChecked == true ? 2.0 : 0,
            UseArcCommands = ChkArcOutput.IsChecked == true,
            AbsoluteIJ = ChkAbsoluteIJ.IsChecked == true,
            ModalGCodes = ChkModalGroups.IsChecked == true,
            DecimalPlaces = precision,
            SafeHeight = safeHeight
        };
    }
}
