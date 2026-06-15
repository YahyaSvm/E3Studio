using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using E3Studio.Models;
using E3Studio.Services;

namespace E3Studio.Dialogs;

public partial class SettingsDialog : Window
{
    private List<Machine> _machines;
    private AppSettings _settings;
    
    public bool SettingsChanged { get; private set; } = false;
    
    public SettingsDialog()
    {
        InitializeComponent();
        
        _settings = SettingsManager.Current;
        _machines = _settings.GetAllMachines();
        
        LoadSettings();
    }
    
    private void LoadSettings()
    {
        // Load machines
        CmbMachine.ItemsSource = _machines;
        CmbMachine.DisplayMemberPath = "Name";
        
        if (_settings.SelectedMachine != null)
        {
            var index = _machines.FindIndex(m => m.Id == _settings.SelectedMachine.Id);
            CmbMachine.SelectedIndex = index >= 0 ? index : 0;
        }
        else if (_machines.Count > 0)
        {
            CmbMachine.SelectedIndex = 0;
        }
        
        // Safe heights
        TxtSafeZ.Text = _settings.GCodeSettings.SafeZ.ToString("F1");
        TxtRapidClearance.Text = _settings.GCodeSettings.RapidClearance.ToString("F1");
        TxtToolChangeZ.Text = _settings.GCodeSettings.ToolChangeHeight.ToString("F1");
        
        // G-Code settings
        SelectComboByContent(CmbFileExtension, _settings.GCodeSettings.FileExtension);
        SelectComboByContent(CmbDecimals, _settings.GCodeSettings.DecimalPlaces.ToString());
        CmbLineEnding.SelectedIndex = _settings.GCodeSettings.LineEnding == "\n" ? 1 : 0;
        TxtSpindleWarmup.Text = _settings.GCodeSettings.SpindleWarmupTime.ToString("F1");
        
        ChkLineNumbers.IsChecked = _settings.GCodeSettings.IncludeLineNumbers;
        ChkComments.IsChecked = _settings.GCodeSettings.IncludeComments;
        ChkCannedCycles.IsChecked = _settings.GCodeSettings.UseCannedCycles;
        ChkToolLengthComp.IsChecked = _settings.GCodeSettings.UseToolLengthComp;
        ChkUseCoolant.IsChecked = _settings.GCodeSettings.UseCoolant;
        ChkArcsAsArcs.IsChecked = _settings.GCodeSettings.OutputArcsAsArcs;
        
        // Toolpath defaults
        TxtDefaultFeed.Text = _settings.DefaultFeedRate.ToString("F0");
        TxtDefaultPlunge.Text = _settings.DefaultPlungeRate.ToString("F0");
        TxtDefaultRPM.Text = _settings.DefaultSpindleRPM.ToString("F0");
        TxtDefaultDepth.Text = _settings.DefaultDepthPerPass.ToString("F1");
        
        // Interface (Grid spacing is now in GridSnapSettingsDialog)
        CmbUnits.SelectedIndex = _settings.DefaultUnits == "in" ? 1 : 0;
        ChkShowGrid.IsChecked = _settings.ShowGrid;
        ChkShowRulers.IsChecked = _settings.ShowRulers;
        ChkShowOrigin.IsChecked = _settings.ShowOrigin;
        ChkSnapToGrid.IsChecked = _settings.SnapToGrid;
        ChkAutoFit.IsChecked = _settings.AutoFitOnImport;
        
        // Files
        ChkAutoSave.IsChecked = _settings.AutoSaveEnabled;
        TxtAutoSaveInterval.Text = _settings.AutoSaveInterval.ToString();
        RecentList.ItemsSource = SettingsManager.GetValidRecentProjects();
    }
    
    private void SelectComboByContent(ComboBox combo, string content)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Content?.ToString() == content)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }
    
    private void UpdateMachineDetails(Machine machine)
    {
        TxtManufacturer.Text = machine.Manufacturer;
        TxtModel.Text = machine.Model;
        TxtMaxX.Text = machine.MaxX.ToString("F0");
        TxtMaxY.Text = machine.MaxY.ToString("F0");
        TxtMaxZ.Text = machine.MaxZ.ToString("F0");
        TxtMaxRPM.Text = machine.MaxSpindleRPM.ToString("F0");
        TxtMaxFeed.Text = machine.MaxFeedRate.ToString("F0");
        TxtPostProcessor.Text = machine.PostProcessor.ToString();
        
        ChkToolChanger.IsChecked = machine.HasToolChanger;
        ChkCoolant.IsChecked = machine.HasCoolant;
        ChkProbe.IsChecked = machine.HasProbe;
        
        // Update safe heights from machine
        TxtSafeZ.Text = machine.SafeZ.ToString("F1");
        TxtRapidClearance.Text = machine.RapidClearanceZ.ToString("F1");
        TxtToolChangeZ.Text = machine.ToolChangeZ.ToString("F1");
    }
    
    private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard against null during initialization
        if (MachinePanel == null || GCodePanel == null || ToolpathPanel == null || 
            InterfacePanel == null || FilesPanel == null)
            return;
            
        if (TabList.SelectedItem is ListBoxItem item)
        {
            var tag = item.Tag?.ToString();
            
            MachinePanel.Visibility = tag == "Machine" ? Visibility.Visible : Visibility.Collapsed;
            GCodePanel.Visibility = tag == "GCode" ? Visibility.Visible : Visibility.Collapsed;
            ToolpathPanel.Visibility = tag == "Toolpath" ? Visibility.Visible : Visibility.Collapsed;
            InterfacePanel.Visibility = tag == "Interface" ? Visibility.Visible : Visibility.Collapsed;
            FilesPanel.Visibility = tag == "Files" ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    
    private void CmbMachine_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbMachine.SelectedItem is Machine machine)
        {
            UpdateMachineDetails(machine);
        }
    }
    
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Reset all settings to defaults?", "Confirm Reset",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
            
        if (result == MessageBoxResult.Yes)
        {
            SettingsManager.Reset();
            _settings = SettingsManager.Current;
            _machines = _settings.GetAllMachines();
            LoadSettings();
        }
    }
    
    private void ClearRecent_Click(object sender, RoutedEventArgs e)
    {
        _settings.RecentProjects.Clear();
        RecentList.ItemsSource = null;
    }
    
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Save machine
        if (CmbMachine.SelectedItem is Machine machine)
        {
            _settings.SelectedMachine = machine;
            _settings.SelectedMachineId = machine.Id;
        }
        
        // Safe heights
        if (double.TryParse(TxtSafeZ.Text, out var safeZ))
            _settings.GCodeSettings.SafeZ = safeZ;
        if (double.TryParse(TxtRapidClearance.Text, out var rapidClearance))
            _settings.GCodeSettings.RapidClearance = rapidClearance;
        if (double.TryParse(TxtToolChangeZ.Text, out var toolChangeZ))
            _settings.GCodeSettings.ToolChangeHeight = toolChangeZ;
        
        // G-Code settings
        if (CmbFileExtension.SelectedItem is ComboBoxItem extItem)
            _settings.GCodeSettings.FileExtension = extItem.Content?.ToString() ?? ".nc";
        if (CmbDecimals.SelectedItem is ComboBoxItem decItem && int.TryParse(decItem.Content?.ToString(), out var dec))
            _settings.GCodeSettings.DecimalPlaces = dec;
        _settings.GCodeSettings.LineEnding = CmbLineEnding.SelectedIndex == 1 ? "\n" : "\r\n";
        if (double.TryParse(TxtSpindleWarmup.Text, out var warmup))
            _settings.GCodeSettings.SpindleWarmupTime = warmup;
            
        _settings.GCodeSettings.IncludeLineNumbers = ChkLineNumbers.IsChecked == true;
        _settings.GCodeSettings.IncludeComments = ChkComments.IsChecked == true;
        _settings.GCodeSettings.UseCannedCycles = ChkCannedCycles.IsChecked == true;
        _settings.GCodeSettings.UseToolLengthComp = ChkToolLengthComp.IsChecked == true;
        _settings.GCodeSettings.UseCoolant = ChkUseCoolant.IsChecked == true;
        _settings.GCodeSettings.OutputArcsAsArcs = ChkArcsAsArcs.IsChecked == true;
        
        // Toolpath defaults
        if (double.TryParse(TxtDefaultFeed.Text, out var feed))
            _settings.DefaultFeedRate = feed;
        if (double.TryParse(TxtDefaultPlunge.Text, out var plunge))
            _settings.DefaultPlungeRate = plunge;
        if (double.TryParse(TxtDefaultRPM.Text, out var rpm))
            _settings.DefaultSpindleRPM = rpm;
        if (double.TryParse(TxtDefaultDepth.Text, out var depth))
            _settings.DefaultDepthPerPass = depth;
        
        // Interface (Grid spacing is in GridSnapSettingsDialog)
        _settings.DefaultUnits = CmbUnits.SelectedIndex == 1 ? "in" : "mm";
        _settings.ShowGrid = ChkShowGrid.IsChecked == true;
        _settings.ShowRulers = ChkShowRulers.IsChecked == true;
        _settings.ShowOrigin = ChkShowOrigin.IsChecked == true;
        _settings.SnapToGrid = ChkSnapToGrid.IsChecked == true;
        _settings.AutoFitOnImport = ChkAutoFit.IsChecked == true;
        
        // Files
        _settings.AutoSaveEnabled = ChkAutoSave.IsChecked == true;
        if (int.TryParse(TxtAutoSaveInterval.Text, out var interval))
            _settings.AutoSaveInterval = interval;
        
        // Save to file
        SettingsManager.Save(_settings);
        
        SettingsChanged = true;
        DialogResult = true;
        Close();
    }
}
