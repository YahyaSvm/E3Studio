using System.Windows;
using System.Windows.Controls;
using E3Studio.Models;

namespace E3Studio.Dialogs;

public partial class StockSetupDialog : Window
{
    public Stock Stock { get; private set; }
    public Material? SelectedMaterial { get; private set; }
    public bool Applied { get; private set; } = false;
    
    // Default materials
    private readonly List<Material> _defaultMaterials = new()
    {
        new Material { Id = 1, Name = "Hardwood (Oak, Maple)", Category = "Wood", FeedRate = 800, PlungeRate = 200, SpindleRPM = 12000, DepthPerPass = 1.5 },
        new Material { Id = 2, Name = "Softwood (Pine, Cedar)", Category = "Wood", FeedRate = 1200, PlungeRate = 300, SpindleRPM = 12000, DepthPerPass = 2.5 },
        new Material { Id = 3, Name = "Plywood", Category = "Wood", FeedRate = 1000, PlungeRate = 250, SpindleRPM = 12000, DepthPerPass = 2.0 },
        new Material { Id = 4, Name = "MDF", Category = "Wood", FeedRate = 1500, PlungeRate = 400, SpindleRPM = 18000, DepthPerPass = 3.0 },
        new Material { Id = 5, Name = "Acrylic", Category = "Plastic", FeedRate = 600, PlungeRate = 150, SpindleRPM = 18000, DepthPerPass = 1.0 },
        new Material { Id = 6, Name = "PVC/Foam", Category = "Plastic", FeedRate = 2000, PlungeRate = 500, SpindleRPM = 12000, DepthPerPass = 5.0 },
        new Material { Id = 7, Name = "Aluminum", Category = "Metal", FeedRate = 400, PlungeRate = 100, SpindleRPM = 8000, DepthPerPass = 0.3 },
    };

    public StockSetupDialog()
    {
        InitializeComponent();
        Stock = new Stock();
        LoadDefaults();
        
        // Wire up events
        CmbMaterial.SelectionChanged += CmbMaterial_SelectionChanged;
        OriginTL.Checked += Origin_Changed;
        OriginTC.Checked += Origin_Changed;
        OriginTR.Checked += Origin_Changed;
        OriginBL.Checked += Origin_Changed;
        OriginBC.Checked += Origin_Changed;
        OriginBR.Checked += Origin_Changed;
    }
    
    public StockSetupDialog(Stock existingStock) : this()
    {
        Stock = existingStock;
        LoadFromStock();
    }
    
    private void LoadDefaults()
    {
        TxtWidth.Text = Stock.Width.ToString("F1");
        TxtHeight.Text = Stock.Height.ToString("F1");
        TxtThickness.Text = Stock.Thickness.ToString("F1");
        
        UpdateOriginSelection();
        UpdateMaterialInfo(0);
    }
    
    private void LoadFromStock()
    {
        TxtWidth.Text = Stock.Width.ToString("F1");
        TxtHeight.Text = Stock.Height.ToString("F1");
        TxtThickness.Text = Stock.Thickness.ToString("F1");
        
        UpdateOriginSelection();
        
        if (Stock.Material != null)
        {
            // Find matching material or select custom
            int idx = _defaultMaterials.FindIndex(m => m.Name == Stock.Material.Name);
            CmbMaterial.SelectedIndex = idx >= 0 ? idx : 7; // 7 = Custom
            UpdateMaterialInfo(CmbMaterial.SelectedIndex);
        }
    }
    
    private void UpdateOriginSelection()
    {
        switch (Stock.ZeroPoint)
        {
            case StockOrigin.TopLeft: OriginTL.IsChecked = true; break;
            case StockOrigin.TopCenter: OriginTC.IsChecked = true; break;
            case StockOrigin.TopRight: OriginTR.IsChecked = true; break;
            case StockOrigin.BottomLeft: OriginBL.IsChecked = true; break;
            case StockOrigin.BottomCenter: OriginBC.IsChecked = true; break;
            case StockOrigin.BottomRight: OriginBR.IsChecked = true; break;
        }
        UpdatePreview();
    }
    
    private void Origin_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }
    
    private void UpdatePreview()
    {
        if (PreviewOrigin == null) return;
        
        // Stock rect: Canvas.Left="20" Canvas.Top="15", Width="100" Height="60"
        double left = 20, top = 15, width = 100, height = 60;
        double originX = 0, originY = 0;
        
        if (OriginTL.IsChecked == true) { originX = left; originY = top; }
        else if (OriginTC.IsChecked == true) { originX = left + width/2; originY = top; }
        else if (OriginTR.IsChecked == true) { originX = left + width; originY = top; }
        else if (OriginBL.IsChecked == true) { originX = left; originY = top + height; }
        else if (OriginBC.IsChecked == true) { originX = left + width/2; originY = top + height; }
        else if (OriginBR.IsChecked == true) { originX = left + width; originY = top + height; }
        
        Canvas.SetLeft(PreviewOrigin, originX - 4);
        Canvas.SetTop(PreviewOrigin, originY - 4);
        
        // Update axes
        PreviewAxisX.X1 = originX;
        PreviewAxisX.Y1 = originY;
        PreviewAxisX.X2 = originX + 20;
        PreviewAxisX.Y2 = originY;
        
        PreviewAxisY.X1 = originX;
        PreviewAxisY.Y1 = originY;
        PreviewAxisY.X2 = originX;
        PreviewAxisY.Y2 = originY - 20;
    }
    
    private void CmbMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMaterialInfo(CmbMaterial.SelectedIndex);
    }
    
    private void UpdateMaterialInfo(int index)
    {
        if (index < 0 || index >= _defaultMaterials.Count)
        {
            // Custom material
            TxtFeedRate.Text = "- mm/min";
            TxtPlungeRate.Text = "- mm/min";
            TxtSpindleRPM.Text = "- RPM";
            TxtDepthPerPass.Text = "- mm";
            SelectedMaterial = null;
            return;
        }
        
        var mat = _defaultMaterials[index];
        SelectedMaterial = mat;
        
        TxtFeedRate.Text = $"{mat.FeedRate} mm/min";
        TxtPlungeRate.Text = $"{mat.PlungeRate} mm/min";
        TxtSpindleRPM.Text = $"{mat.SpindleRPM} RPM";
        TxtDepthPerPass.Text = $"{mat.DepthPerPass} mm";
    }
    
    private StockOrigin GetSelectedOrigin()
    {
        if (OriginTL.IsChecked == true) return StockOrigin.TopLeft;
        if (OriginTC.IsChecked == true) return StockOrigin.TopCenter;
        if (OriginTR.IsChecked == true) return StockOrigin.TopRight;
        if (OriginBL.IsChecked == true) return StockOrigin.BottomLeft;
        if (OriginBC.IsChecked == true) return StockOrigin.BottomCenter;
        if (OriginBR.IsChecked == true) return StockOrigin.BottomRight;
        return StockOrigin.TopCenter;
    }
    
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        // Validate and apply
        if (!double.TryParse(TxtWidth.Text, out double width) || width <= 0)
        {
            MessageBox.Show("Please enter a valid width.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!double.TryParse(TxtHeight.Text, out double height) || height <= 0)
        {
            MessageBox.Show("Please enter a valid height.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!double.TryParse(TxtThickness.Text, out double thickness) || thickness <= 0)
        {
            MessageBox.Show("Please enter a valid thickness.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        Stock.Width = width;
        Stock.Height = height;
        Stock.Thickness = thickness;
        Stock.ZeroPoint = GetSelectedOrigin();
        Stock.Material = SelectedMaterial;
        
        Applied = true;
        DialogResult = true;
        Close();
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
    
    private void OpenMaterialLibrary_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new MaterialLibraryDialog();
        dialog.Owner = this;
        dialog.ShowDialog();
    }
}
