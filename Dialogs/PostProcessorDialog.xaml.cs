using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using E3Studio.Services;
using E3Studio.Services.PostProcessor;

namespace E3Studio.Dialogs;

public partial class PostProcessorDialog : Window
{
    private List<PostProcessorInfo> _postProcessors = new();
    private PostProcessorInfo? _selectedPostProcessor;
    
    public PostProcessorDialog()
    {
        InitializeComponent();
        LoadPostProcessors();
    }
    
    private void LoadPostProcessors()
    {
        // Load built-in post processors
        _postProcessors = new List<PostProcessorInfo>
        {
            new PostProcessorInfo { Name = "GRBL", Controller = "GRBL 1.1", Description = "GRBL firmware for CNC routers", FileExtension = ".gcode", IsBuiltIn = true },
            new PostProcessorInfo { Name = "Klipper", Controller = "Klipper", Description = "Klipper firmware for 3D printers and CNC", FileExtension = ".gcode", IsBuiltIn = true },
            new PostProcessorInfo { Name = "Mach3/Mach4", Controller = "Mach3", Description = "Mach3/Mach4 CNC controller", FileExtension = ".tap", IsBuiltIn = true },
            new PostProcessorInfo { Name = "LinuxCNC", Controller = "LinuxCNC", Description = "LinuxCNC (EMC2) controller", FileExtension = ".ngc", IsBuiltIn = true },
            new PostProcessorInfo { Name = "Fanuc", Controller = "Fanuc", Description = "Fanuc CNC controller", FileExtension = ".nc", IsBuiltIn = true },
            new PostProcessorInfo { Name = "Haas", Controller = "Haas", Description = "Haas CNC milling machines", FileExtension = ".nc", IsBuiltIn = true },
            new PostProcessorInfo { Name = "Heidenhain", Controller = "Heidenhain", Description = "Heidenhain TNC controller", FileExtension = ".h", IsBuiltIn = true },
            new PostProcessorInfo { Name = "Siemens Sinumerik", Controller = "Sinumerik", Description = "Siemens Sinumerik CNC controller", FileExtension = ".mpf", IsBuiltIn = true },
        };
        
        // Load custom post processors from settings
        var customPPs = SettingsManager.Current?.CustomPostProcessors ?? new List<PostProcessorInfo>();
        _postProcessors.AddRange(customPPs);
        
        RefreshList();
    }
    
    private void RefreshList()
    {
        PostProcessorList.Items.Clear();
        
        foreach (var pp in _postProcessors)
        {
            var panel = new StackPanel();
            
            var nameText = new TextBlock 
            { 
                Text = pp.Name + (pp.IsBuiltIn ? " (Built-in)" : ""), 
                FontWeight = FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush")
            };
            panel.Children.Add(nameText);
            
            var detailText = new TextBlock
            {
                Text = $"{pp.Controller} • {pp.FileExtension}",
                Foreground = (System.Windows.Media.Brush)FindResource("TextLowBrush"),
                FontSize = 10
            };
            panel.Children.Add(detailText);
            
            var item = new ListBoxItem { Tag = pp, Content = panel };
            PostProcessorList.Items.Add(item);
        }
        
        if (PostProcessorList.Items.Count > 0)
        {
            PostProcessorList.SelectedIndex = 0;
        }
    }
    
    private void PostProcessorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PostProcessorList.SelectedItem is ListBoxItem item && item.Tag is PostProcessorInfo pp)
        {
            _selectedPostProcessor = pp;
            LoadPostProcessorDetails(pp);
        }
    }
    
    private void LoadPostProcessorDetails(PostProcessorInfo pp)
    {
        TxtName.Text = pp.Name;
        TxtVersion.Text = pp.Version;
        TxtDescription.Text = pp.Description;
        TxtController.Text = pp.Controller;
        TxtFileExtension.Text = pp.FileExtension;
        
        ChkLineNumbers.IsChecked = pp.UseLineNumbers;
        ChkG54.IsChecked = pp.UseG54;
        ChkG90.IsChecked = pp.UseG90;
        ChkG21.IsChecked = pp.UseMetric;
        
        ChkToolChange.IsChecked = pp.SupportToolChange;
        TxtToolChangeCmd.Text = pp.ToolChangeCommand;
        TxtSpindleWarmup.Text = pp.SpindleWarmupTime.ToString("F1");
        
        ChkCoolant.IsChecked = pp.SupportCoolant;
        TxtCoolantOn.Text = pp.CoolantOnCommand;
        TxtCoolantOff.Text = pp.CoolantOffCommand;
        
        TxtSpindleCw.Text = pp.SpindleCwCommand;
        TxtSpindleCcw.Text = pp.SpindleCcwCommand;
        TxtSpindleStop.Text = pp.SpindleStopCommand;
        
        TxtProgramEnd.Text = pp.ProgramEndCommand;
        
        TxtHeader.Text = pp.HeaderGCode;
        TxtFooter.Text = pp.FooterGCode;
        
        // Disable editing for built-in post processors
        var isEnabled = !pp.IsBuiltIn;
        TxtName.IsEnabled = isEnabled;
        TxtController.IsEnabled = isEnabled;
        TxtFileExtension.IsEnabled = isEnabled;
        ChkLineNumbers.IsEnabled = isEnabled;
        ChkG54.IsEnabled = isEnabled;
        ChkG90.IsEnabled = isEnabled;
        ChkG21.IsEnabled = isEnabled;
        ChkToolChange.IsEnabled = isEnabled;
        ChkCoolant.IsEnabled = isEnabled;
        TxtToolChangeCmd.IsEnabled = isEnabled;
        TxtSpindleWarmup.IsEnabled = isEnabled;
        TxtCoolantOn.IsEnabled = isEnabled;
        TxtCoolantOff.IsEnabled = isEnabled;
        TxtSpindleCw.IsEnabled = isEnabled;
        TxtSpindleCcw.IsEnabled = isEnabled;
        TxtSpindleStop.IsEnabled = isEnabled;
        TxtProgramEnd.IsEnabled = isEnabled;
        TxtHeader.IsEnabled = isEnabled;
        TxtFooter.IsEnabled = isEnabled;
    }
    
    private void AddPostProcessor_Click(object sender, RoutedEventArgs e)
    {
        var newPP = new PostProcessorInfo
        {
            Name = "Custom Post Processor",
            Version = "1.0",
            Controller = "Custom",
            FileExtension = ".nc"
        };
        
        _postProcessors.Add(newPP);
        
        if (SettingsManager.Current != null)
        {
            SettingsManager.Current.CustomPostProcessors ??= new List<PostProcessorInfo>();
            SettingsManager.Current.CustomPostProcessors.Add(newPP);
            SettingsManager.Save();
        }
        
        RefreshList();
        PostProcessorList.SelectedIndex = PostProcessorList.Items.Count - 1;
    }
    
    private void DeletePostProcessor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPostProcessor == null) return;
        
        if (_selectedPostProcessor.IsBuiltIn)
        {
            MessageBox.Show("Cannot delete built-in post processors.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var result = MessageBox.Show($"Delete post processor '{_selectedPostProcessor.Name}'?", "Confirm Delete", 
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _postProcessors.Remove(_selectedPostProcessor);
            
            if (SettingsManager.Current?.CustomPostProcessors != null)
            {
                SettingsManager.Current.CustomPostProcessors.Remove(_selectedPostProcessor);
                SettingsManager.Save();
            }
            
            _selectedPostProcessor = null;
            RefreshList();
        }
    }
    
    private void ImportPostProcessor_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Post Processor Files (*.post *.cfg *.json)|*.post;*.cfg;*.json|All Files (*.*)|*.*",
            Title = "Import Post Processor"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var imported = JsonSerializer.Deserialize<PostProcessorInfo>(json);
                if (imported == null)
                    throw new InvalidOperationException("Invalid post processor file.");
                imported.IsBuiltIn = false;
                _postProcessors.Add(imported);
                RefreshList();
                MessageBox.Show($"Imported post processor: {imported.Name}", "Import Post Processor",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void ExportPostProcessor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPostProcessor == null)
        {
            MessageBox.Show("No post processor selected.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Post Processor Files (*.post)|*.post|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Export Post Processor",
            FileName = $"{_selectedPostProcessor.Name}.post"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                SyncDetailsToSelected();
                var json = JsonSerializer.Serialize(_selectedPostProcessor, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show($"Exported post processor: {_selectedPostProcessor.Name}",
                    "Export Post Processor", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void SyncDetailsToSelected()
    {
        if (_selectedPostProcessor == null) return;

        _selectedPostProcessor.Name = TxtName.Text;
        _selectedPostProcessor.Version = TxtVersion.Text;
        _selectedPostProcessor.Description = TxtDescription.Text;
        _selectedPostProcessor.Controller = TxtController.Text;
        _selectedPostProcessor.FileExtension = TxtFileExtension.Text;

        _selectedPostProcessor.UseLineNumbers = ChkLineNumbers.IsChecked ?? false;
        _selectedPostProcessor.UseG54 = ChkG54.IsChecked ?? false;
        _selectedPostProcessor.UseG90 = ChkG90.IsChecked ?? false;
        _selectedPostProcessor.UseMetric = ChkG21.IsChecked ?? false;

        _selectedPostProcessor.SupportToolChange = ChkToolChange.IsChecked ?? false;
        _selectedPostProcessor.ToolChangeCommand = TxtToolChangeCmd.Text;
        _selectedPostProcessor.SpindleWarmupTime = double.TryParse(TxtSpindleWarmup.Text, out var sw) ? sw : 3.0;

        _selectedPostProcessor.SupportCoolant = ChkCoolant.IsChecked ?? false;
        _selectedPostProcessor.CoolantOnCommand = TxtCoolantOn.Text;
        _selectedPostProcessor.CoolantOffCommand = TxtCoolantOff.Text;

        _selectedPostProcessor.SpindleCwCommand = TxtSpindleCw.Text;
        _selectedPostProcessor.SpindleCcwCommand = TxtSpindleCcw.Text;
        _selectedPostProcessor.SpindleStopCommand = TxtSpindleStop.Text;

        _selectedPostProcessor.ProgramEndCommand = TxtProgramEnd.Text;
        _selectedPostProcessor.HeaderGCode = TxtHeader.Text;
        _selectedPostProcessor.FooterGCode = TxtFooter.Text;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPostProcessor != null && !_selectedPostProcessor.IsBuiltIn)
        {
            SyncDetailsToSelected();
            SettingsManager.Save();
        }
        
        RefreshList();
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class PostProcessorInfo
{
    public string Name { get; set; } = "Custom";
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = "";
    public string Controller { get; set; } = "Custom";
    public string FileExtension { get; set; } = ".nc";
    public bool IsBuiltIn { get; set; } = false;
    
    // G-Code format
    public bool UseLineNumbers { get; set; } = false;
    public bool UseG54 { get; set; } = true;
    public bool UseG90 { get; set; } = true;
    public bool UseMetric { get; set; } = true;
    
    // Tool change
    public bool SupportToolChange { get; set; } = true;
    public string ToolChangeCommand { get; set; } = "M6";
    public double SpindleWarmupTime { get; set; } = 3.0;
    
    // Coolant
    public bool SupportCoolant { get; set; } = true;
    public string CoolantOnCommand { get; set; } = "M8";
    public string CoolantOffCommand { get; set; } = "M9";
    
    // Spindle
    public string SpindleCwCommand { get; set; } = "M3";
    public string SpindleCcwCommand { get; set; } = "M4";
    public string SpindleStopCommand { get; set; } = "M5";
    
    // Program end
    public string ProgramEndCommand { get; set; } = "M30";
    
    // Custom header/footer
    public string HeaderGCode { get; set; } = "";
    public string FooterGCode { get; set; } = "";
    
    public override string ToString() => Name;
}
