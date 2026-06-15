#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Core :: ProjectManager
// CAM projesinin tüm state'ini yönetir:
//   - Yüklü modeller, operasyonlar, takım yolları, makine konfigürasyonu
//   - JSON formatında kayıt/yükleme
//   - Parametrik güncelleme (CAD değişince toolpath otomatik yenileme)
// ─────────────────────────────────────────────────────────────────────────────
#include <string>
#include <vector>
#include <unordered_map>
#include <memory>
#include <optional>
#include <filesystem>
#include <nlohmann/json.hpp>

namespace e3::core {

// ─── Takım Tanımı ─────────────────────────────────────────────────────────
struct Tool {
    std::string id;
    std::string name;
    enum class Type { FlatEndmill, BallEndmill, BullNose, Drill, Tap } type;
    double diameter;          // mm
    double cornerRadius;      // mm (bullnose için)
    double flutes;
    double overallLength;     // mm
    double cuttingLength;     // mm
    std::string material;     // "Carbide", "HSS"

    nlohmann::json toJson() const;
    static Tool fromJson(const nlohmann::json& j);
};

// ─── Operasyon Tanımı ─────────────────────────────────────────────────────
struct Operation {
    std::string id;
    std::string name;
    enum class Type {
        Pocket2D, Contour2D,       // 2.5D
        SurfaceFinishing,           // 3D
        AdaptiveClearing,           // 3D Trochoidal
        Drilling, Threading         // Delik
    } type;

    std::string toolId;
    std::string geometryRef;   // hangi yüzey/kenar

    // İşleme parametreleri
    double feedrateXY;         // mm/min
    double feedrateZ;          // mm/min
    double spindleSpeed;       // rpm
    double depthOfCut;         // mm (eksenel)
    double stepover;           // mm (radyal) veya % çap
    double stockToLeave;       // mm — kalacak stok (roughing)
    double tolerance;          // mm — yol doğruluk toleransı

    bool isDirty = true;       // true ise yeniden hesaplanmalı
    std::string toolpathId;    // hesaplanmış toolpath referansı

    nlohmann::json toJson() const;
    static Operation fromJson(const nlohmann::json& j);
};

// ─── Model Referansı ──────────────────────────────────────────────────────
struct ModelRef {
    std::string id;
    std::string filePath;
    enum class Role { Workpiece, Stock, Fixture } role;
    std::array<double, 16> transform; // 4x4 matrix, row-major
};

// ─── Makine Konfigürasyonu ────────────────────────────────────────────────
struct MachineConfig {
    std::string id;
    std::string name;
    int axes;           // 3, 4, 5
    double maxFeedXY;   // mm/min
    double maxFeedZ;
    double maxSpindle;  // rpm
    double minSpindle;
    double workEnvX;    // mm — çalışma hacmi
    double workEnvY;
    double workEnvZ;
    std::string postProcessorId;
};

// ─── Proje ────────────────────────────────────────────────────────────────
struct Project {
    std::string id;
    std::string name;
    std::string createdAt;
    std::string updatedAt;

    MachineConfig machine;
    std::vector<ModelRef> models;
    std::vector<Tool> toolLibrary;
    std::vector<Operation> operations;
    std::string outputDir;         // G-Code çıktı klasörü

    nlohmann::json toJson() const;
    static Project fromJson(const nlohmann::json& j);
};

// ─── Yönetici ─────────────────────────────────────────────────────────────
class ProjectManager {
public:
    static ProjectManager& instance() {
        static ProjectManager mgr;
        return mgr;
    }

    // Yeni boş proje
    void newProject(const std::string& name);

    // Dosyadan yükle
    bool loadProject(const std::filesystem::path& path);

    // Diske kaydet
    bool saveProject(const std::filesystem::path& path);
    bool saveProjectIncremental(); // Son konuma kaydet

    // Operasyon CRUD
    std::string addOperation(Operation op);
    bool updateOperation(const std::string& id, Operation op);
    bool removeOperation(const std::string& id);
    std::optional<Operation> findOperation(const std::string& id) const;

    // Takım CRUD
    std::string addTool(Tool tool);
    std::optional<Tool> findTool(const std::string& id) const;

    // Model referans yönetimi
    std::string addModel(ModelRef model);

    // Kirli operasyonları döner — yeniden hesaplanmalı
    std::vector<std::string> getDirtyOperationIds() const;

    // Belirli bir operasyon toolpath güncellendi
    void markOperationClean(const std::string& id, const std::string& toolpathId);

    // Aktif proje
    Project* currentProject() { return m_project.get(); }
    const Project* currentProject() const { return m_project.get(); }

    bool hasProject() const { return m_project != nullptr; }

private:
    ProjectManager() = default;
    std::unique_ptr<Project> m_project;
    std::filesystem::path m_lastSavePath;
};

} // namespace e3::core
