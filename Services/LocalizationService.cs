using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace E3Studio.Services;

/// <summary>
/// Localization service for multi-language support
/// </summary>
public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();
    
    private Dictionary<string, Dictionary<string, string>> _translations = new();
    private string _currentLanguage = "en";
    
    public event EventHandler? LanguageChanged;
    
    public string CurrentLanguage => _currentLanguage;
    
    public IEnumerable<LanguageInfo> AvailableLanguages => new[]
    {
        new LanguageInfo("en", "English", "🇬🇧"),
        new LanguageInfo("tr", "Türkçe", "🇹🇷"),
        new LanguageInfo("de", "Deutsch", "🇩🇪"),
        new LanguageInfo("fr", "Français", "🇫🇷"),
        new LanguageInfo("es", "Español", "🇪🇸"),
        new LanguageInfo("zh", "中文", "🇨🇳"),
        new LanguageInfo("ja", "日本語", "🇯🇵"),
        new LanguageInfo("ko", "한국어", "🇰🇷"),
        new LanguageInfo("ru", "Русский", "🇷🇺"),
        new LanguageInfo("pt", "Português", "🇧🇷")
    };
    
    private LocalizationService()
    {
        InitializeDefaultTranslations();
    }
    
    private void InitializeDefaultTranslations()
    {
        // English (default)
        _translations["en"] = new Dictionary<string, string>
        {
            // Menu
            ["Menu.File"] = "File",
            ["Menu.Edit"] = "Edit",
            ["Menu.View"] = "View",
            ["Menu.Draw"] = "Draw",
            ["Menu.CAM"] = "CAM",
            ["Menu.Machine"] = "Machine",
            ["Menu.Help"] = "Help",
            
            // File Menu
            ["Menu.File.New"] = "New Project",
            ["Menu.File.Open"] = "Open Project",
            ["Menu.File.Save"] = "Save Project",
            ["Menu.File.SaveAs"] = "Save As...",
            ["Menu.File.Import"] = "Import",
            ["Menu.File.Export"] = "Export",
            ["Menu.File.Exit"] = "Exit",
            
            // Edit Menu
            ["Menu.Edit.Undo"] = "Undo",
            ["Menu.Edit.Redo"] = "Redo",
            ["Menu.Edit.Cut"] = "Cut",
            ["Menu.Edit.Copy"] = "Copy",
            ["Menu.Edit.Paste"] = "Paste",
            ["Menu.Edit.Delete"] = "Delete",
            ["Menu.Edit.SelectAll"] = "Select All",
            
            // View Menu
            ["Menu.View.ZoomIn"] = "Zoom In",
            ["Menu.View.ZoomOut"] = "Zoom Out",
            ["Menu.View.ZoomFit"] = "Zoom to Fit",
            ["Menu.View.ZoomSelection"] = "Zoom to Selection",
            ["Menu.View.Grid"] = "Show Grid",
            ["Menu.View.Origin"] = "Show Origin",
            
            // Draw Menu
            ["Menu.Draw.Line"] = "Line",
            ["Menu.Draw.Rectangle"] = "Rectangle",
            ["Menu.Draw.Circle"] = "Circle",
            ["Menu.Draw.Arc"] = "Arc",
            ["Menu.Draw.Polyline"] = "Polyline",
            ["Menu.Draw.Text"] = "Text",
            
            // CAM Menu
            ["Menu.CAM.CreateToolpath"] = "Create Toolpath",
            ["Menu.CAM.Profile"] = "Profile",
            ["Menu.CAM.Pocket"] = "Pocket",
            ["Menu.CAM.Drill"] = "Drill",
            ["Menu.CAM.VCarve"] = "V-Carve",
            ["Menu.CAM.Engrave"] = "Engrave",
            ["Menu.CAM.GenerateGCode"] = "Generate G-Code",
            ["Menu.CAM.Simulate"] = "Simulate",
            
            // Machine Menu
            ["Menu.Machine.Connect"] = "Connect",
            ["Menu.Machine.Disconnect"] = "Disconnect",
            ["Menu.Machine.Home"] = "Home Machine",
            ["Menu.Machine.Zero"] = "Set Zero",

            ["Menu.Machine.Probe"] = "Probe",
            ["Menu.Machine.Settings"] = "Machine Settings",
            
            // Tool Library
            ["ToolLibrary.Title"] = "Tool Library",
            ["ToolLibrary.Name"] = "Name",
            ["ToolLibrary.Type"] = "Type",
            ["ToolLibrary.Diameter"] = "Diameter",
            ["ToolLibrary.FluteLength"] = "Flute Length",
            ["ToolLibrary.TotalLength"] = "Total Length",
            ["ToolLibrary.Flutes"] = "Number of Flutes",
            ["ToolLibrary.Material"] = "Material",
            
            // Toolpath Dialog
            ["Toolpath.Title"] = "Create Toolpath",
            ["Toolpath.Type"] = "Toolpath Type",
            ["Toolpath.Tool"] = "Tool",
            ["Toolpath.CutDepth"] = "Cut Depth",
            ["Toolpath.StepDown"] = "Step Down",
            ["Toolpath.FeedRate"] = "Feed Rate",
            ["Toolpath.PlungeRate"] = "Plunge Rate",
            ["Toolpath.SpindleRPM"] = "Spindle RPM",
            ["Toolpath.Offset"] = "Offset",
            ["Toolpath.Direction"] = "Cut Direction",
            ["Toolpath.Tabs"] = "Tabs",
            ["Toolpath.LeadIn"] = "Lead-In",
            ["Toolpath.LeadOut"] = "Lead-Out",
            
            // Stock Setup
            ["Stock.Title"] = "Stock Setup",
            ["Stock.Width"] = "Width",
            ["Stock.Height"] = "Height",
            ["Stock.Thickness"] = "Thickness",
            ["Stock.Material"] = "Material",
            ["Stock.Origin"] = "Origin Position",
            
            // Simulation
            ["Simulation.Play"] = "Play",
            ["Simulation.Pause"] = "Pause",
            ["Simulation.Stop"] = "Stop",
            ["Simulation.Speed"] = "Speed",
            ["Simulation.Reset"] = "Reset",
            
            // Common
            ["Common.OK"] = "OK",
            ["Common.Cancel"] = "Cancel",
            ["Common.Apply"] = "Apply",
            ["Common.Close"] = "Close",
            ["Common.Add"] = "Add",
            ["Common.Remove"] = "Remove",
            ["Common.Edit"] = "Edit",
            ["Common.Delete"] = "Delete",
            ["Common.Save"] = "Save",
            ["Common.Load"] = "Load",
            
            // Units
            ["Units.mm"] = "mm",
            ["Units.inch"] = "inch",
            ["Units.mmPerMin"] = "mm/min",
            ["Units.inchPerMin"] = "in/min",
            ["Units.rpm"] = "RPM",
            ["Units.degrees"] = "°",
            
            // Status
            ["Status.Ready"] = "Ready",
            ["Status.Connecting"] = "Connecting...",
            ["Status.Connected"] = "Connected",
            ["Status.Disconnected"] = "Disconnected",
            ["Status.Running"] = "Running",
            ["Status.Paused"] = "Paused",
            ["Status.Error"] = "Error",
            
            // Messages
            ["Message.UnsavedChanges"] = "You have unsaved changes. Do you want to save?",
            ["Message.ConfirmDelete"] = "Are you sure you want to delete?",
            ["Message.ExportSuccess"] = "Export completed successfully.",
            ["Message.ImportSuccess"] = "Import completed successfully.",
            ["Message.ConnectionFailed"] = "Failed to connect to machine."
        };
        
        // Turkish
        _translations["tr"] = new Dictionary<string, string>
        {
            // Menu
            ["Menu.File"] = "Dosya",
            ["Menu.Edit"] = "Düzenle",
            ["Menu.View"] = "Görünüm",
            ["Menu.Draw"] = "Çizim",
            ["Menu.CAM"] = "CAM",
            ["Menu.Machine"] = "Makine",
            ["Menu.Help"] = "Yardım",
            
            // File Menu
            ["Menu.File.New"] = "Yeni Proje",
            ["Menu.File.Open"] = "Projeyi Aç",
            ["Menu.File.Save"] = "Projeyi Kaydet",
            ["Menu.File.SaveAs"] = "Farklı Kaydet...",
            ["Menu.File.Import"] = "İçe Aktar",
            ["Menu.File.Export"] = "Dışa Aktar",
            ["Menu.File.Exit"] = "Çıkış",
            
            // Edit Menu
            ["Menu.Edit.Undo"] = "Geri Al",
            ["Menu.Edit.Redo"] = "Yinele",
            ["Menu.Edit.Cut"] = "Kes",
            ["Menu.Edit.Copy"] = "Kopyala",
            ["Menu.Edit.Paste"] = "Yapıştır",
            ["Menu.Edit.Delete"] = "Sil",
            ["Menu.Edit.SelectAll"] = "Tümünü Seç",
            
            // View Menu
            ["Menu.View.ZoomIn"] = "Yakınlaştır",
            ["Menu.View.ZoomOut"] = "Uzaklaştır",
            ["Menu.View.ZoomFit"] = "Tümünü Göster",
            ["Menu.View.ZoomSelection"] = "Seçime Yakınlaş",
            ["Menu.View.Grid"] = "Izgarayı Göster",
            ["Menu.View.Origin"] = "Orijini Göster",
            
            // Draw Menu
            ["Menu.Draw.Line"] = "Çizgi",
            ["Menu.Draw.Rectangle"] = "Dikdörtgen",
            ["Menu.Draw.Circle"] = "Daire",
            ["Menu.Draw.Arc"] = "Yay",
            ["Menu.Draw.Polyline"] = "Çoklu Çizgi",
            ["Menu.Draw.Text"] = "Metin",
            
            // CAM Menu
            ["Menu.CAM.CreateToolpath"] = "Takım Yolu Oluştur",
            ["Menu.CAM.Profile"] = "Profil",
            ["Menu.CAM.Pocket"] = "Cep",
            ["Menu.CAM.Drill"] = "Delme",
            ["Menu.CAM.VCarve"] = "V-Oyma",
            ["Menu.CAM.Engrave"] = "Gravür",
            ["Menu.CAM.GenerateGCode"] = "G-Code Üret",
            ["Menu.CAM.Simulate"] = "Simülasyon",
            
            // Machine Menu
            ["Menu.Machine.Connect"] = "Bağlan",
            ["Menu.Machine.Disconnect"] = "Bağlantıyı Kes",
            ["Menu.Machine.Home"] = "Makineyi Sıfırla",
            ["Menu.Machine.Zero"] = "Sıfır Noktası Ayarla",

            ["Menu.Machine.Probe"] = "Prob",
            ["Menu.Machine.Settings"] = "Makine Ayarları",
            
            // Tool Library
            ["ToolLibrary.Title"] = "Takım Kütüphanesi",
            ["ToolLibrary.Name"] = "Ad",
            ["ToolLibrary.Type"] = "Tip",
            ["ToolLibrary.Diameter"] = "Çap",
            ["ToolLibrary.FluteLength"] = "Kesici Uzunluk",
            ["ToolLibrary.TotalLength"] = "Toplam Uzunluk",
            ["ToolLibrary.Flutes"] = "Ağız Sayısı",
            ["ToolLibrary.Material"] = "Malzeme",
            
            // Toolpath Dialog
            ["Toolpath.Title"] = "Takım Yolu Oluştur",
            ["Toolpath.Type"] = "Takım Yolu Tipi",
            ["Toolpath.Tool"] = "Takım",
            ["Toolpath.CutDepth"] = "Kesim Derinliği",
            ["Toolpath.StepDown"] = "Kademe Derinliği",
            ["Toolpath.FeedRate"] = "İlerleme Hızı",
            ["Toolpath.PlungeRate"] = "Dalma Hızı",
            ["Toolpath.SpindleRPM"] = "Mil Devri",
            ["Toolpath.Offset"] = "Ofset",
            ["Toolpath.Direction"] = "Kesim Yönü",
            ["Toolpath.Tabs"] = "Tutucular",
            ["Toolpath.LeadIn"] = "Giriş Hareketi",
            ["Toolpath.LeadOut"] = "Çıkış Hareketi",
            
            // Stock Setup
            ["Stock.Title"] = "Stok Ayarları",
            ["Stock.Width"] = "Genişlik",
            ["Stock.Height"] = "Yükseklik",
            ["Stock.Thickness"] = "Kalınlık",
            ["Stock.Material"] = "Malzeme",
            ["Stock.Origin"] = "Orijin Konumu",
            
            // Simulation
            ["Simulation.Play"] = "Oynat",
            ["Simulation.Pause"] = "Duraklat",
            ["Simulation.Stop"] = "Durdur",
            ["Simulation.Speed"] = "Hız",
            ["Simulation.Reset"] = "Sıfırla",
            
            // Common
            ["Common.OK"] = "Tamam",
            ["Common.Cancel"] = "İptal",
            ["Common.Apply"] = "Uygula",
            ["Common.Close"] = "Kapat",
            ["Common.Add"] = "Ekle",
            ["Common.Remove"] = "Kaldır",
            ["Common.Edit"] = "Düzenle",
            ["Common.Delete"] = "Sil",
            ["Common.Save"] = "Kaydet",
            ["Common.Load"] = "Yükle",
            
            // Units
            ["Units.mm"] = "mm",
            ["Units.inch"] = "inç",
            ["Units.mmPerMin"] = "mm/dk",
            ["Units.inchPerMin"] = "inç/dk",
            ["Units.rpm"] = "dev/dk",
            ["Units.degrees"] = "°",
            
            // Status
            ["Status.Ready"] = "Hazır",
            ["Status.Connecting"] = "Bağlanıyor...",
            ["Status.Connected"] = "Bağlı",
            ["Status.Disconnected"] = "Bağlı Değil",
            ["Status.Running"] = "Çalışıyor",
            ["Status.Paused"] = "Duraklatıldı",
            ["Status.Error"] = "Hata",
            
            // Messages
            ["Message.UnsavedChanges"] = "Kaydedilmemiş değişiklikler var. Kaydetmek ister misiniz?",
            ["Message.ConfirmDelete"] = "Silmek istediğinizden emin misiniz?",
            ["Message.ExportSuccess"] = "Dışa aktarma başarıyla tamamlandı.",
            ["Message.ImportSuccess"] = "İçe aktarma başarıyla tamamlandı.",
            ["Message.ConnectionFailed"] = "Makineye bağlanılamadı."
        };
        
        // German
        _translations["de"] = new Dictionary<string, string>
        {
            ["Menu.File"] = "Datei",
            ["Menu.Edit"] = "Bearbeiten",
            ["Menu.View"] = "Ansicht",
            ["Menu.Draw"] = "Zeichnen",
            ["Menu.CAM"] = "CAM",
            ["Menu.Machine"] = "Maschine",
            ["Menu.Help"] = "Hilfe",
            ["Menu.File.New"] = "Neues Projekt",
            ["Menu.File.Open"] = "Projekt öffnen",
            ["Menu.File.Save"] = "Projekt speichern",
            ["Common.OK"] = "OK",
            ["Common.Cancel"] = "Abbrechen",
            ["Status.Ready"] = "Bereit"
        };
    }
    
    public void SetLanguage(string languageCode)
    {
        if (_translations.ContainsKey(languageCode))
        {
            _currentLanguage = languageCode;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    
    public string Get(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var langDict) &&
            langDict.TryGetValue(key, out var value))
        {
            return value;
        }
        
        // Fallback to English
        if (_translations.TryGetValue("en", out var enDict) &&
            enDict.TryGetValue(key, out var enValue))
        {
            return enValue;
        }
        
        return key;
    }
    
    public string this[string key] => Get(key);
    
    public void LoadTranslations(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var translations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            if (translations != null)
            {
                foreach (var lang in translations)
                {
                    _translations[lang.Key] = lang.Value;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load translations: {ex.Message}");
        }
    }
    
    public void SaveTranslations(string filePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(_translations, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save translations: {ex.Message}");
        }
    }
}

public class LanguageInfo
{
    public string Code { get; }
    public string Name { get; }
    public string Flag { get; }
    
    public LanguageInfo(string code, string name, string flag)
    {
        Code = code;
        Name = name;
        Flag = flag;
    }
    
    public override string ToString() => $"{Flag} {Name}";
}
