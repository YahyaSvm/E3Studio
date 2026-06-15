namespace E3Studio.Services.PostProcessor;

/// <summary>
/// Post-processor çıktı ayarları
/// </summary>
public class PostProcessorOptions
{
    /// <summary>
    /// Metrik sistem kullan (mm). False ise inch kullanılır.
    /// </summary>
    public bool UseMetric { get; set; } = true;
    
    /// <summary>
    /// Satır numaraları ekle (N10, N20, vb.)
    /// </summary>
    public bool IncludeLineNumbers { get; set; } = false;
    
    /// <summary>
    /// Yorum satırları ekle
    /// </summary>
    public bool IncludeComments { get; set; } = true;
    
    /// <summary>
    /// Takım değişikliğinde durakla
    /// </summary>
    public bool PauseOnToolChange { get; set; } = true;
    
    /// <summary>
    /// Spindle açılış gecikmesi (saniye, 0=devre dışı)
    /// </summary>
    public double SpindleDelay { get; set; } = 0;
    
    /// <summary>
    /// Arc komutları kullan (G02/G03). False ise lineer segmentlere dönüştür.
    /// </summary>
    public bool UseArcCommands { get; set; } = true;
    
    /// <summary>
    /// Arc merkez koordinatları mutlak mı (true) yoksa artımlı mı (false)
    /// </summary>
    public bool AbsoluteIJ { get; set; } = false;
    
    /// <summary>
    /// Modal G-kodları kullan (tekrarlanan G kodlarını atla)
    /// </summary>
    public bool ModalGCodes { get; set; } = true;
    
    /// <summary>
    /// Ondalık basamak sayısı
    /// </summary>
    public int DecimalPlaces { get; set; } = 3;
    
    /// <summary>
    /// Güvenli yükseklik (Z)
    /// </summary>
    public double SafeHeight { get; set; } = 10.0;
    
    /// <summary>
    /// G-kod dosya uzantısı
    /// </summary>
    public string FileExtension { get; set; } = "nc";
    
    /// <summary>
    /// Program numarası (bazı kontroller için)
    /// </summary>
    public int ProgramNumber { get; set; } = 1;
    
    /// <summary>
    /// Maksimum satır uzunluğu (0=sınırsız)
    /// </summary>
    public int MaxLineLength { get; set; } = 0;
    
    /// <summary>
    /// Soğutma sıvısı kullan
    /// </summary>
    public bool UseCoolant { get; set; } = true;
    
    /// <summary>
    /// Soğutma tipi: Flood (M08), Mist (M07), Through (M88)
    /// </summary>
    public CoolantType CoolantType { get; set; } = CoolantType.Flood;
    
    /// <summary>
    /// Başlangıç konumuna dön
    /// </summary>
    public bool ReturnToHome { get; set; } = true;
    
    /// <summary>
    /// İş koordinat sistemi (G54-G59)
    /// </summary>
    public int WorkOffset { get; set; } = 54;
}

/// <summary>
/// Soğutma sıvısı tipi
/// </summary>
public enum CoolantType
{
    /// <summary>Soğutma kapalı</summary>
    Off = 0,
    
    /// <summary>Taşma soğutma (M08)</summary>
    Flood = 1,
    
    /// <summary>Sis soğutma (M07)</summary>
    Mist = 2,
    
    /// <summary>Takım içi soğutma (M88)</summary>
    Through = 3,
    
    /// <summary>Hava üfleme (M51)</summary>
    Air = 4
}
