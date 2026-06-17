#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Simulation :: SimulationEngine
// GPU destekli gerçek zamanlı talaş kaldırma simülasyonu.
// Stock model bir voksel grid (veya dexel) olarak tutulur.
// Her hareket adımında takımın geçtiği vokseller malzeme olarak işaretlenir.
// ─────────────────────────────────────────────────────────────────────────────
#include "../toolpath/Toolpath.h"
#include "../geometry/GeometryKernel.h"
#include <vector>
#include <functional>
#include <atomic>
#include <thread>

namespace e3::simulation {

// ─── Dexel (Depth Pixel) Modeli ──────────────────────────────────────────
// Her XY hücresi için Z aralıklarını tutar — voksel'den çok daha verimli
struct DexelColumn {
    double x, y;
    std::vector<std::pair<double,double>> segments; // (zBottom, zTop) çiftleri

    void subtract(double toolRadius, double zBottom, double zTop);
    void merge(); // örtüşen segmentleri birleştir
};

struct StockModel {
    std::vector<DexelColumn> columns;
    double gridResolution; // mm — her dexel hücresinin boyutu
    geometry::BoundingBox bbox;
    int gridX, gridY;

    static StockModel fromBoundingBox(
        const geometry::BoundingBox& bbox,
        double resolution = 0.2); // 0.2mm çözünürlük

    // Three.js'e gönderilecek güncel mesh
    geometry::Mesh toMesh() const;

    DexelColumn& columnAt(int ix, int iy);
    const DexelColumn& columnAt(int ix, int iy) const;

    double remainingVolume() const;
};

// ─── Simülasyon Sonucu ────────────────────────────────────────────────────
struct SimulationFrame {
    size_t moveIndex;
    float  progress;       // 0.0-1.0
    double remainingVolume; // mm³
    bool   hasCollision;
    bool   hasGouge;       // parça iç yüzeyine kesme var mı
};

// ─── Simülasyon Motoru ────────────────────────────────────────────────────
class SimulationEngine {
public:
    // Başlangıç stok + toolpath ver
    void setup(
        const geometry::BoundingBox& stockBbox,
        const toolpath::Toolpath& toolpath,
        double toolDiameter,
        double gridResolution = 0.2);

    // Tüm simülasyonu koştur (asenkron)
    void runAsync(
        std::function<void(const SimulationFrame&)> onFrame,
        std::function<void()> onComplete);

    // Adım adım koştur (UI scrubbing için)
    SimulationFrame stepTo(size_t moveIndex);

    void pause()  { m_paused = true; }
    void resume() { m_paused = false; }
    void stop()   { m_stop = true; }

    // Güncel stock mesh'i döner (Three.js görselleştirme için)
    geometry::Mesh getCurrentMesh() const;

    double getInitialVolume() const { return m_initialVolume; }

private:
    void applyMove(const toolpath::Move& move, double toolRadius);
    void applyToolSweep(
        const geometry::Vec3& from,
        const geometry::Vec3& to,
        double toolRadius);

    StockModel    m_stock;
    toolpath::Toolpath m_toolpath;
    double        m_toolDiameter = 10.0;
    double        m_initialVolume = 0.0;

    std::atomic<bool> m_paused{false};
    std::atomic<bool> m_stop{false};
    std::thread       m_simThread;
};

} // namespace e3::simulation
