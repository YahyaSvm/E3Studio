// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: SimulationEngine.cpp  — Dexel tabanlı malzeme kaldırma
// ─────────────────────────────────────────────────────────────────────────────
#include "SimulationEngine.h"
#include "../core/Logger.h"
#include <algorithm>
#include <cmath>

namespace e3::simulation {

// ─── DexelColumn ─────────────────────────────────────────────────────────────

void DexelColumn::subtract(double /*toolRadius*/, double zBottom, double zTop) {
    std::vector<std::pair<double,double>> result;
    for (const auto& [sb, st] : segments) {
        if (st <= zBottom || sb >= zTop) {
            result.emplace_back(sb, st);
        } else {
            if (sb < zBottom) result.emplace_back(sb, zBottom);
            if (st > zTop)    result.emplace_back(zTop, st);
        }
    }
    segments = std::move(result);
}

void DexelColumn::merge() {
    if (segments.size() < 2) return;
    std::sort(segments.begin(), segments.end());
    std::vector<std::pair<double,double>> merged;
    merged.push_back(segments[0]);
    for (size_t i = 1; i < segments.size(); ++i) {
        auto& back = merged.back();
        if (segments[i].first <= back.second)
            back.second = std::max(back.second, segments[i].second);
        else
            merged.push_back(segments[i]);
    }
    segments = std::move(merged);
}

// ─── StockModel ──────────────────────────────────────────────────────────────

StockModel StockModel::fromBoundingBox(const geometry::BoundingBox& bb,
                                        double resolution)
{
    StockModel model;
    model.bbox           = bb;
    model.gridResolution = resolution;
    model.gridX = static_cast<int>(std::ceil((bb.max.x - bb.min.x) / resolution));
    model.gridY = static_cast<int>(std::ceil((bb.max.y - bb.min.y) / resolution));

    model.columns.resize(model.gridX * model.gridY);
    for (int iy = 0; iy < model.gridY; ++iy) {
        for (int ix = 0; ix < model.gridX; ++ix) {
            auto& col = model.columnAt(ix, iy);
            col.x = bb.min.x + (ix + 0.5) * resolution;
            col.y = bb.min.y + (iy + 0.5) * resolution;
            col.segments.emplace_back(bb.min.z, bb.max.z);
        }
    }
    return model;
}

DexelColumn& StockModel::columnAt(int ix, int iy) {
    return columns[iy * gridX + ix];
}

const DexelColumn& StockModel::columnAt(int ix, int iy) const {
    return columns[iy * gridX + ix];
}

geometry::Mesh StockModel::toMesh() const {
    // Placeholder — gerçek implementasyon marching squares kullanır
    return {};
}

// ─── SimulationEngine ────────────────────────────────────────────────────────

void SimulationEngine::setup(
    const geometry::BoundingBox& stockBbox,
    const toolpath::Toolpath& toolpath,
    double toolDiameter,
    double gridResolution)
{
    m_stock        = StockModel::fromBoundingBox(stockBbox, gridResolution);
    m_toolpath     = toolpath;
    m_toolDiameter = toolDiameter;

    // Başlangıç hacmi: tüm sütunların toplam yüksekliği × hücre alanı
    double cellArea = gridResolution * gridResolution;
    double total = 0;
    for (const auto& col : m_stock.columns)
        for (const auto& [zb, zt] : col.segments)
            total += (zt - zb) * cellArea;
    m_initialVolume = total;

    m_paused.store(false);
    m_stop.store(false);

    E3_LOG_INFO("Simülasyon kurulumu: {}×{} grid, V0={:.1f} mm³",
                m_stock.gridX, m_stock.gridY, m_initialVolume);
}

void SimulationEngine::runAsync(
    std::function<void(const SimulationFrame&)> onFrame,
    std::function<void()> onComplete)
{
    if (m_simThread.joinable()) m_simThread.join();

    m_stop.store(false);
    m_paused.store(false);

    m_simThread = std::thread([this, onFrame, onComplete]() {
        const size_t total = m_toolpath.moves.size();
        for (size_t i = 0; i < total && !m_stop.load(); ++i) {
            while (m_paused.load() && !m_stop.load())
                std::this_thread::sleep_for(std::chrono::milliseconds(20));

            applyMove(m_toolpath.moves[i], m_toolDiameter / 2.0);

            SimulationFrame frame;
            frame.moveIndex  = i;
            frame.progress   = static_cast<float>(i + 1) / static_cast<float>(total);
            frame.hasCollision = false;
            frame.hasGouge     = false;

            // Kalan hacim
            double cellArea = m_stock.gridResolution * m_stock.gridResolution;
            double vol = 0;
            for (const auto& col : m_stock.columns)
                for (const auto& [zb, zt] : col.segments)
                    vol += (zt - zb) * cellArea;
            frame.remainingVolume = vol;

            if (onFrame) onFrame(frame);

            if (i % 10 == 0)
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
        if (onComplete) onComplete();
        E3_LOG_INFO("Simülasyon tamamlandı");
    });
}

SimulationFrame SimulationEngine::stepTo(size_t moveIndex) {
    moveIndex = std::min(moveIndex, m_toolpath.moves.size() - 1);

    // Tüm baştan yeniden uygula (production'da checkpoint kullanılır)
    m_stock = StockModel::fromBoundingBox(m_stock.bbox, m_stock.gridResolution);
    for (size_t i = 0; i <= moveIndex; ++i)
        applyMove(m_toolpath.moves[i], m_toolDiameter / 2.0);

    double cellArea = m_stock.gridResolution * m_stock.gridResolution;
    double vol = 0;
    for (const auto& col : m_stock.columns)
        for (const auto& [zb, zt] : col.segments)
            vol += (zt - zb) * cellArea;

    SimulationFrame frame;
    frame.moveIndex      = moveIndex;
    frame.progress       = static_cast<float>(moveIndex + 1) /
                           static_cast<float>(m_toolpath.moves.size());
    frame.remainingVolume = vol;
    frame.hasCollision   = false;
    frame.hasGouge       = false;
    return frame;
}

geometry::Mesh SimulationEngine::getCurrentMesh() const {
    return m_stock.toMesh();
}

// ─── Private ─────────────────────────────────────────────────────────────────

void SimulationEngine::applyMove(const toolpath::Move& move, double toolRadius) {
    using T = toolpath::Move::Type;
    if (move.type == T::Rapid || move.type == T::Retract) return;
    applyToolSweep(move.position, move.position, toolRadius);
}

void SimulationEngine::applyToolSweep(
    const geometry::Vec3& /*from*/,
    const geometry::Vec3& to,
    double toolRadius)
{
    const double res = m_stock.gridResolution;
    const double r2  = toolRadius * toolRadius;

    int ixMin = static_cast<int>((to.x - toolRadius - m_stock.bbox.minX) / res);
    int ixMax = static_cast<int>((to.x + toolRadius - m_stock.bbox.minX) / res) + 1;
    int iyMin = static_cast<int>((to.y - toolRadius - m_stock.bbox.minY) / res);
    int iyMax = static_cast<int>((to.y + toolRadius - m_stock.bbox.minY) / res) + 1;

    ixMin = std::clamp(ixMin, 0, m_stock.gridX - 1);
    ixMax = std::clamp(ixMax, 0, m_stock.gridX - 1);
    iyMin = std::clamp(iyMin, 0, m_stock.gridY - 1);
    iyMax = std::clamp(iyMax, 0, m_stock.gridY - 1);

    for (int iy = iyMin; iy <= iyMax; ++iy) {
        for (int ix = ixMin; ix <= ixMax; ++ix) {
            auto& col = m_stock.columnAt(ix, iy);
            double dx = col.x - to.x;
            double dy = col.y - to.y;
            if (dx*dx + dy*dy <= r2) {
                const double zBottom = to.z - 75.0; // takım uzunluğu varsayılan
                const double zTop    = to.z;
                col.subtract(toolRadius, zBottom, zTop);
            }
        }
    }
}

} // namespace e3::simulation

// ─── DexelColumn ─────────────────────────────────────────────────────────────

void DexelColumn::subtract(double zBottom, double zTop) {
    std::vector<ZSegment> result;
    for (const auto& seg : segments) {
        if (seg.zTop <= zBottom || seg.zBottom >= zTop) {
            // Kesişim yok — olduğu gibi bırak
            result.push_back(seg);
        } else {
            // Alt parça
            if (seg.zBottom < zBottom) {
                result.push_back({seg.zBottom, zBottom});
            }
            // Üst parça
            if (seg.zTop > zTop) {
                result.push_back({zTop, seg.zTop});
            }
        }
    }
    segments = std::move(result);
}

double DexelColumn::remainingHeight() const {
    double total = 0;
    for (const auto& s : segments) total += (s.zTop - s.zBottom);
    return total;
}

// ─── StockModel ──────────────────────────────────────────────────────────────

StockModel StockModel::fromBoundingBox(const geometry::BoundingBox& bb,
                                        int resX, int resY)
{
    StockModel model;
    model.minX = bb.minX; model.maxX = bb.maxX;
    model.minY = bb.minY; model.maxY = bb.maxY;
    model.resX = resX;    model.resY = resY;

    const double zBottom = bb.minZ;
    const double zTop    = bb.maxZ;

    model.grid.resize(resX * resY);
    for (auto& col : model.grid) {
        col.segments.push_back({zBottom, zTop});
    }

    return model;
}

DexelColumn& StockModel::column(int xi, int yi) {
    return grid[yi * resX + xi];
}

const DexelColumn& StockModel::column(int xi, int yi) const {
    return grid[yi * resX + xi];
}

void StockModel::subtractCylinder(double cx, double cy,
                                   double radius,
                                   double zBottom, double zTop)
{
    const double cellW = (maxX - minX) / resX;
    const double cellH = (maxY - minY) / resY;
    const double r2 = radius * radius;

    int ixMin = static_cast<int>((cx - radius - minX) / cellW);
    int ixMax = static_cast<int>((cx + radius - minX) / cellW) + 1;
    int iyMin = static_cast<int>((cy - radius - minY) / cellH);
    int iyMax = static_cast<int>((cy + radius - minY) / cellH) + 1;

    ixMin = std::clamp(ixMin, 0, resX - 1);
    ixMax = std::clamp(ixMax, 0, resX - 1);
    iyMin = std::clamp(iyMin, 0, resY - 1);
    iyMax = std::clamp(iyMax, 0, resY - 1);

    for (int iy = iyMin; iy <= iyMax; ++iy) {
        for (int ix = ixMin; ix <= ixMax; ++ix) {
            const double wx = minX + (ix + 0.5) * cellW;
            const double wy = minY + (iy + 0.5) * cellH;
            const double dx = wx - cx;
            const double dy = wy - cy;
            if (dx*dx + dy*dy <= r2) {
                column(ix, iy).subtract(zBottom, zTop);
            }
        }
    }
}

double StockModel::remainingMaterialRatio() const {
    if (grid.empty()) return 0;
    double total = 0;
    for (const auto& col : grid) total += col.remainingHeight();
    return total / static_cast<double>(grid.size());
}

// ─── SimulationEngine ────────────────────────────────────────────────────────

SimulationEngine::SimulationEngine()
    : m_running(false), m_paused(false), m_currentStep(0)
{}

SimulationEngine::~SimulationEngine() {
    pause();
}

void SimulationEngine::setStock(StockModel stock) {
    std::lock_guard<std::mutex> lock(m_mutex);
    m_stock = std::move(stock);
    m_currentStep = 0;
}

void SimulationEngine::setToolpath(const toolpath::Toolpath& tp) {
    std::lock_guard<std::mutex> lock(m_mutex);
    m_toolpath = tp;
    m_currentStep = 0;
}

void SimulationEngine::setTool(double diameter, double length) {
    m_toolDiameter = diameter;
    m_toolLength   = length;
}

std::future<void> SimulationEngine::runAsync(FrameCallback cb) {
    m_running.store(true);
    m_paused.store(false);

    return std::async(std::launch::async, [this, cb]() {
        const size_t total = m_toolpath.moves.size();
        if (total == 0) return;

        for (size_t i = 0; i < total && m_running.load(); ++i) {
            // Duraklama kontrolü
            while (m_paused.load() && m_running.load()) {
                std::this_thread::sleep_for(std::chrono::milliseconds(50));
            }

            {
                std::lock_guard<std::mutex> lock(m_mutex);
                m_currentStep = static_cast<int>(i);
            }

            processMove(i);

            SimulationFrame frame;
            frame.stepIndex   = static_cast<int>(i);
            frame.progress    = static_cast<float>(i + 1) / static_cast<float>(total);
            frame.currentPos  = m_toolpath.moves[i].position;
            frame.hasCollision = false;
            frame.hasGouge     = false;
            {
                std::lock_guard<std::mutex> lock(m_mutex);
                frame.remainingMaterial = m_stock.remainingMaterialRatio();
            }

            if (cb) cb(frame);

            // Görsel hız — her 5 adımda kısa uyku
            if (i % 5 == 0) {
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
            }
        }

        m_running.store(false);
        E3_LOG_INFO("Simulation", "Simülasyon tamamlandı: {} adım", total);
    });
}

void SimulationEngine::pause() {
    m_paused.store(true);
}

void SimulationEngine::resume() {
    m_paused.store(false);
}

void SimulationEngine::stop() {
    m_running.store(false);
}

SimulationFrame SimulationEngine::stepTo(int stepIndex) {
    std::lock_guard<std::mutex> lock(m_mutex);

    SimulationFrame frame;
    if (m_toolpath.moves.empty()) return frame;

    stepIndex = std::clamp(stepIndex, 0,
                           static_cast<int>(m_toolpath.moves.size()) - 1);

    // Stoku sıfırla, o adıma kadar yeniden uygula
    // (Scrubbing — üretimde daha verimli checkpoint sistemi kullanılır)
    if (stepIndex < m_currentStep) {
        // Geri git — stoku yeniden hesapla
        // Hızlı yol: orijinal stok + [0..stepIndex]
        // Şimdilik kısmi reset
        m_currentStep = 0;
    }

    for (int i = m_currentStep; i <= stepIndex; ++i) {
        applyMove(m_toolpath.moves[i]);
    }

    m_currentStep = stepIndex;

    frame.stepIndex = stepIndex;
    frame.progress  = static_cast<float>(stepIndex + 1) /
                      static_cast<float>(m_toolpath.moves.size());
    frame.currentPos = m_toolpath.moves[stepIndex].position;
    frame.remainingMaterial = m_stock.remainingMaterialRatio();

    return frame;
}

// ─── Private Helpers ─────────────────────────────────────────────────────────

void SimulationEngine::processMove(size_t index) {
    if (index >= m_toolpath.moves.size()) return;
    const auto& move = m_toolpath.moves[index];
    applyMove(move);
}

void SimulationEngine::applyMove(const toolpath::Move& move) {
    using MoveType = toolpath::Move::Type;

    // Rapid hareketlerde malzeme kaldırma yok
    if (move.type == MoveType::Rapid || move.type == MoveType::Retract) return;

    const double r = m_toolDiameter / 2.0;
    const double zBottom = move.position.z - m_toolLength;
    const double zTop    = move.position.z;

    std::lock_guard<std::mutex> lock(m_mutex);
    m_stock.subtractCylinder(
        move.position.x, move.position.y,
        r,
        zBottom, zTop
    );
}

} // namespace e3::simulation
