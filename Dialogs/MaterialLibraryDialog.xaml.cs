using System.Windows;
using System.Windows.Controls;

namespace E3Studio.Dialogs;

public partial class MaterialLibraryDialog : Window
{
    public MaterialLibraryDialog()
    {
        InitializeComponent();
    }
    
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void MaterialList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MaterialList.SelectedItem is ListBoxItem item)
        {
            TxtMaterialName.Text = item.Content?.ToString() ?? "";
        }
    }
}
