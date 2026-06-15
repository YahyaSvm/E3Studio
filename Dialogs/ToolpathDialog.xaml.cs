using System.Windows;
using System.Windows.Controls;
using E3Studio.Models;

namespace E3Studio.Dialogs;

public partial class ToolpathDialog : Window
{
    public Toolpath Result { get; private set; } = new();
    public bool Created { get; private set; } = false;
    
    private readonly List<GeometryPath> _sourcePaths;
    private readonly Stock _stock;
    
    // Default tools
    private readonly List<Tool> _defaultTools = new()
    {
        new Tool { Id = 1, Name = "Flat Endmill 3.175mm", Diameter = 3.175, FluteLength = 10, Flutes = 2, Type = ToolType.Endmill },
        new Tool { Id = 2, Name = "Flat Endmill 6mm", Diameter = 6, FluteLength = 20, Flutes = 2, Type = ToolType.Endmill },
        new Tool { Id = 3, Name = "Ball Nose 3.175mm", Diameter = 3.175, FluteLength = 12, Flutes = 2, Type = ToolType.BallNose },
        new Tool { Id = 4, Name = "V-Bit 60° 6mm", Diameter = 6, VAngle = 60, Flutes = 1, Type = ToolType.VBit },
    };

    public ToolpathDialog(List<GeometryPath> sourcePaths, Stock stock, ToolpathType initialType = ToolpathType.Profile)
    {
        InitializeComponent();
        
        _sourcePaths = sourcePaths;
        _stock = stock;
        
        // Set initial type
        switch (initialType)
        {
            case ToolpathType.Profile:
                TypeProfile.IsChecked = true;
                break;
            case ToolpathType.Pocket:
                TypePocket.IsChecked = true;
                break;
            case ToolpathType.Drill:
                TypeDrill.IsChecked = true;
                break;
        }
        
        UpdateUI();
        UpdatePassCount();
        
        TxtSelectedPaths.Text = $"{_sourcePaths.Count} path(s) selected";
        
        // Set default depth from stock
        TxtFinalDepth.Text = _stock.Thickness.ToString("F1");
        
        // Wire up events
        TxtFinalDepth.TextChanged += Depth_TextChanged;
        TxtDepthPerPass.TextChanged += Depth_TextChanged;
    }
    
    private void ToolpathType_Changed(object sender, RoutedEventArgs e)
    {
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        if (ProfileOptions == null) return; // Not initialized yet
        
        bool isProfile = TypeProfile.IsChecked == true;
        bool isPocket = TypePocket.IsChecked == true;
        bool isDrill = TypeDrill.IsChecked == true;
        
        ProfileOptions.Visibility = isProfile ? Visibility.Visible : Visibility.Collapsed;
        PocketOptions.Visibility = isPocket ? Visibility.Visible : Visibility.Collapsed;
        TabOptions.Visibility = isProfile ? Visibility.Visible : Visibility.Collapsed;
        
        // Update title
        if (isProfile) TitleText.Text = "Create Profile Toolpath";
        else if (isPocket) TitleText.Text = "Create Pocket Toolpath";
        else if (isDrill) TitleText.Text = "Create Drill Toolpath";
        
        // Update default name
        string typeName = isProfile ? "Profile" : (isPocket ? "Pocket" : "Drill");
        TxtName.Text = $"{typeName} 1";
    }
    
    private void Tool_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Could update recommended speeds based on tool
    }
    
    private void Depth_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePassCount();
    }
    
    private void UpdatePassCount()
    {
        if (TxtPassCount == null) return;
        
        if (double.TryParse(TxtFinalDepth.Text, out double finalDepth) &&
            double.TryParse(TxtDepthPerPass.Text, out double depthPerPass) &&
            depthPerPass > 0)
        {
            int passes = (int)Math.Ceiling(finalDepth / depthPerPass);
            TxtPassCount.Text = $"{passes} pass{(passes != 1 ? "es" : "")}";
        }
        else
        {
            TxtPassCount.Text = "- passes";
        }
    }
    
    private void Create_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Please enter a toolpath name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!double.TryParse(TxtFinalDepth.Text, out double finalDepth) || finalDepth <= 0)
        {
            MessageBox.Show("Please enter a valid final depth.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!double.TryParse(TxtDepthPerPass.Text, out double depthPerPass) || depthPerPass <= 0)
        {
            MessageBox.Show("Please enter a valid depth per pass.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!double.TryParse(TxtFeedRate.Text, out double feedRate) || feedRate <= 0)
        {
            MessageBox.Show("Please enter a valid feed rate.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Build toolpath
        Result = new Toolpath
        {
            Name = TxtName.Text,
            Type = GetSelectedType(),
            Tool = CmbTool.SelectedIndex >= 0 && CmbTool.SelectedIndex < _defaultTools.Count 
                   ? _defaultTools[CmbTool.SelectedIndex] 
                   : _defaultTools[0],
            FinalDepth = finalDepth,
            CutDepth = depthPerPass,
            FeedRate = feedRate,
            PlungeRate = double.TryParse(TxtPlungeRate.Text, out var pr) ? pr : 200,
            SpindleRPM = double.TryParse(TxtSpindleRPM.Text, out var rpm) ? rpm : 12000,
            Side = GetSelectedSide(),
            StepOver = SliderStepover?.Value ?? 40,
            
            // Entry Mode
            EntryMode = GetSelectedEntryMode(),
            HelixDiameter = double.TryParse(TxtHelixDiameter.Text, out var hd) ? hd : 5.0,
            RampAngle = double.TryParse(TxtRampAngle.Text, out var ra) ? ra : 3.0,
            
            // Lead-In/Out
            LeadInType = GetSelectedLeadType(CmbLeadInType),
            LeadOutType = GetSelectedLeadType(CmbLeadOutType),
            LeadInRadius = double.TryParse(TxtLeadInRadius.Text, out var lir) ? lir : 0,
            LeadOutRadius = double.TryParse(TxtLeadOutRadius.Text, out var lor) ? lor : 0,
            
            // Arc Fitting
            UseArcFitting = ChkArcFitting.IsChecked == true,
            ArcTolerance = double.TryParse(TxtArcTolerance.Text, out var at) ? at : 0.01
        };
        
        // Tabs
        if (ChkEnableTabs.IsChecked == true)
        {
            Result.TabCount = int.TryParse(TxtTabCount.Text, out var tc) ? tc : 4;
            Result.TabWidth = double.TryParse(TxtTabWidth.Text, out var tw) ? tw : 5;
            Result.TabHeight = double.TryParse(TxtTabHeight.Text, out var th) ? th : 2;
        }
        else
        {
            Result.TabCount = 0;
        }
        
        Created = true;
        DialogResult = true;
        Close();
    }
    
    private ToolpathType GetSelectedType()
    {
        if (TypePocket.IsChecked == true) return ToolpathType.Pocket;
        if (TypeDrill.IsChecked == true) return ToolpathType.Drill;
        return ToolpathType.Profile;
    }
    
    private ProfileSide GetSelectedSide()
    {
        if (SideInside.IsChecked == true) return ProfileSide.Inside;
        if (SideOnLine.IsChecked == true) return ProfileSide.OnLine;
        return ProfileSide.Outside;
    }
    
    private EntryMode GetSelectedEntryMode()
    {
        if (EntryRamp.IsChecked == true) return EntryMode.Ramp;
        if (EntryHelix.IsChecked == true) return EntryMode.Helix;
        return EntryMode.Plunge;
    }
    
    private LeadType GetSelectedLeadType(ComboBox comboBox)
    {
        return comboBox.SelectedIndex switch
        {
            1 => LeadType.Line,
            2 => LeadType.Arc,
            3 => LeadType.Tangent,
            _ => LeadType.None
        };
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
