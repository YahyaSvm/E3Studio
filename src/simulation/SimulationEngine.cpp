#include "SimulationEngine.h"
#include "../core/Logger.h"
#include <future>
#include <algorithm>

namespace e3::simulation {

// ─── DexelColumn ─────────────────────────────────────────────────────────────

void DexelColumn::subtract(double toolRadius, double zBottom, double zTop) {
    (void)toolRadius;
    std::vector<std::pair<double,double>> result;
    for (const auto& seg : segments) {
        if (seg.second <= zBottom || seg.first >= zTop) {
            result.push_back(seg);
        } else {
            if (seg.first < zBottom) {
                result.push_back({seg.first, zBottom});
            }
            if (seg.second > zTop) {
                result.push_back({zTop, seg.second});
            }
        }
    }
    segments = std::move(result);
}

void DexelColumn::merge() {
    if (segments.empty()) return;
    std::sort(segments.begin(), segments.end());
    std::vector<std::pair<double,double>> merged;
    merged.push_back(segments[0]);
    for (size_t i = 1; i < segments.size(); ++i) {
        if (segments[i].first <= merged.back().second) {
            merged.back().second = std::max(merged.back().second, segments[i].second);
        } else {
            merged.push_back(segments[i]);
        }
    }
    segments = std::move(merged);
}

// ─── StockModel ──────────────────────────────────────────────────────────────

StockModel StockModel::fromBoundingBox(
    const geometry::BoundingBox& bbox,
    double resolution)
{
    StockModel model;
    model.bbox = bbox;
    model.gridResolution = resolution;

    geometry::Vec3 size = bbox.size();
    model.gridX = std::max(1, static_cast<int>(std::ceil(size.x / resolution)));
    model.gridY = std::max(1, static_cast<int>(std::ceil(size.y / resolution)));

    model.columns.resize(model.gridX * model.gridY);
    for (int iy = 0; iy < model.gridY; ++iy) {
        for (int ix = 0; ix < model.gridX; ++ix) {
            auto& col = model.columnAt(ix, iy);
            col.x = bbox.min.x + (ix + 0.5) * resolution;
            col.y = bbox.min.y + (iy + 0.5) * resolution;
            col.segments.push_back({bbox.min.z, bbox.max.z});
        }
    }

    return model;
}

geometry::Mesh StockModel::toMesh() const {
    geometry::Mesh mesh;
    const double res = gridResolution;
    const double halfRes = res * 0.5;

    for (int iy = 0; iy < gridY; ++iy) {
        for (int ix = 0; ix < gridX; ++ix) {
            const auto& col = columns[iy * gridX + ix];
            double topZ = col.segments.empty() ? bbox.min.z : col.segments.back().second;
            if (topZ <= bbox.min.z + 0.001) continue;

            double x = col.x, y = col.y;
            double x0 = x - halfRes, x1 = x + halfRes;
            double y0 = y - halfRes, y1 = y + halfRes;

            size_t idx = mesh.vertices.size();
            mesh.vertices.push_back({x0, y0, topZ});
            mesh.vertices.push_back({x1, y0, topZ});
            mesh.vertices.push_back({x1, y1, topZ});
            mesh.vertices.push_back({x0, y1, topZ});
            mesh.triangles.push_back({static_cast<int>(idx), static_cast<int>(idx+1), static_cast<int>(idx+2)});
            mesh.triangles.push_back({static_cast<int>(idx), static_cast<int>(idx+2), static_cast<int>(idx+3)});
        }
    }
    mesh.computeNormals();
    return mesh;
}

DexelColumn& StockModel::columnAt(int ix, int iy) {
    return columns[iy * gridX + ix];
}

const DexelColumn& StockModel::columnAt(int ix, int iy) const {
    return columns[iy * gridX + ix];
}

double StockModel::remainingVolume() const {
    double volume = 0.0;
    const double cellArea = gridResolution * gridResolution;
    for (const auto& col : columns) {
        for (const auto& seg : col.segments) {
            volume += (seg.second - seg.first) * cellArea;
        }
    }
    return volume;
}

// ─── SimulationEngine ────────────────────────────────────────────────────────

void SimulationEngine::setup(
    const geometry::BoundingBox& stockBbox,
    const toolpath::Toolpath& toolpath,
    double toolDiameter,
    double gridResolution)
{
    m_stock = StockModel::fromBoundingBox(stockBbox, gridResolution);
    m_toolpath = toolpath;
    m_toolDiameter = toolDiameter;
    m_initialVolume = stockBbox.size().x * stockBbox.size().y * stockBbox.size().z;
    m_paused = false;
    m_stop = false;
}

void SimulationEngine::runAsync(
    std::function<void(const SimulationFrame&)> onFrame,
    std::function<void()> onComplete)
{
    if (m_simThread.joinable()) {
        m_stop = true;
        m_simThread.join();
    }
    m_paused = false;
    m_stop = false;

    m_simThread = std::thread([this, onFrame, onComplete]() {
        const double toolRadius = m_toolDiameter / 2.0;
        const size_t total = m_toolpath.moves.size();

        for (size_t i = 0; i < total && !m_stop; ++i) {
            while (m_paused && !m_stop) {
                std::this_thread::sleep_for(std::chrono::milliseconds(50));
            }
            if (m_stop) break;

            applyMove(m_toolpath.moves[i], toolRadius);

            SimulationFrame frame;
            frame.moveIndex = i;
            frame.progress = static_cast<float>(i + 1) / static_cast<float>(total);
            frame.remainingVolume = m_stock.remainingVolume();
            frame.hasCollision = false;
            frame.hasGouge = false;

            if (onFrame) onFrame(frame);
        }

        if (onComplete) onComplete();
    });
}

SimulationFrame SimulationEngine::stepTo(size_t moveIndex) {
    SimulationFrame frame{};
    const double toolRadius = m_toolDiameter / 2.0;
    const size_t total = m_toolpath.moves.size();
    if (total == 0) return frame;

    moveIndex = std::min(moveIndex, total - 1);

    // Reset stock to initial state before re-applying moves
    m_stock = StockModel::fromBoundingBox(m_stock.bbox, m_stock.gridResolution);
    for (size_t i = 0; i <= moveIndex; ++i) {
        applyMove(m_toolpath.moves[i], toolRadius);
    }

    frame.moveIndex = moveIndex;
    frame.progress = static_cast<float>(moveIndex + 1) / static_cast<float>(total);
    frame.remainingVolume = m_stock.remainingVolume();
    frame.hasCollision = false;
    frame.hasGouge = false;

    return frame;
}

geometry::Mesh SimulationEngine::getCurrentMesh() const {
    return m_stock.toMesh();
}

// ─── Private Helpers ─────────────────────────────────────────────────────────

void SimulationEngine::applyMove(const toolpath::Move& move, double toolRadius) {
    if (move.type == toolpath::Move::Type::Rapid || move.type == toolpath::Move::Type::Retract) {
        return;
    }

    geometry::Vec3 from = move.position;
    geometry::Vec3 to = move.position;
    applyToolSweep(from, to, toolRadius);
}

void SimulationEngine::applyToolSweep(
    const geometry::Vec3& /*from*/,
    const geometry::Vec3& to,
    double toolRadius)
{
    const double res = m_stock.gridResolution;
    const double r2  = toolRadius * toolRadius;

    int ixMin = static_cast<int>((to.x - toolRadius - m_stock.bbox.min.x) / res);
    int ixMax = static_cast<int>((to.x + toolRadius - m_stock.bbox.min.x) / res) + 1;
    int iyMin = static_cast<int>((to.y - toolRadius - m_stock.bbox.min.y) / res);
    int iyMax = static_cast<int>((to.y + toolRadius - m_stock.bbox.min.y) / res) + 1;

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
                const double zBottom = to.z - 75.0;
                const double zTop    = to.z;
                col.subtract(toolRadius, zBottom, zTop);
            }
        }
    }
}

} // namespace e3::simulation
