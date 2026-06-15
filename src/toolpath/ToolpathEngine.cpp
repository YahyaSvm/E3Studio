#include "ToolpathEngine.h"
#include "../core/Application.h"
#include "../core/EventBus.h"
#include "operations/PocketOperation.h"
#include "operations/AdaptiveClearingOperation.h"
#include <thread>

namespace e3::toolpath {

std::future<Toolpath> ToolpathEngine::computeAsync(const std::string& operationId) {
    auto& app   = core::Application::instance();
    auto& pool  = app.threadPool();
    auto& proj  = core::ProjectManager::instance();

    auto opOpt = proj.findOperation(operationId);
    if (!opOpt) {
        std::promise<Toolpath> p;
        p.set_value(Toolpath{});
        return p.get_future();
    }

    core::Operation op = *opOpt;

    auto toolOpt = proj.findTool(op.toolId);
    if (!toolOpt) {
        std::promise<Toolpath> p;
        p.set_value(Toolpath{});
        return p.get_future();
    }
    core::Tool tool = *toolOpt;

    return pool.submit([this, op, tool]() -> Toolpath {
        return computeInternal(op, tool);
    });
}

void ToolpathEngine::computeAllDirty() {
    auto& proj = core::ProjectManager::instance();
    auto dirty = proj.getDirtyOperationIds();
    for (const auto& id : dirty) {
        std::thread([this, fut = computeAsync(id), &proj, id]() mutable {
            Toolpath tp = fut.get();
            if (tp.isEmpty()) return;

            const std::string tpId  = tp.id;
            const size_t    nMoves  = tp.moveCount();
            const double    estTime = tp.estimatedTime;

            {
                std::lock_guard<std::mutex> lk(m_cacheMutex);
                m_cache[tpId] = std::move(tp);
            }
            proj.markOperationClean(id, tpId);
            core::EventBus::instance().publish(core::ToolpathGeneratedEvent{
                id, nMoves, estTime
            });
        }).detach();
    }
}

Toolpath ToolpathEngine::computeInternal(
    const core::Operation& op,
    const core::Tool& tool)
{
    std::unique_ptr<IOperation> operation;

    switch (op.type) {
        case core::Operation::Type::Pocket2D:
            operation = std::make_unique<operations::PocketOperation>();
            break;
        case core::Operation::Type::AdaptiveClearing:
            operation = std::make_unique<operations::AdaptiveClearingOperation>();
            break;
        default:
            E3_LOG_WARN("Bilinmeyen operasyon tipi: {}", static_cast<int>(op.type));
            return {};
    }

    auto progressCb = [this, &op](float p) {
        if (m_progressCb) m_progressCb(op.id, p);
    };

    return operation->compute(op, tool, progressCb);
}

const Toolpath* ToolpathEngine::getToolpath(const std::string& toolpathId) const {
    std::lock_guard<std::mutex> lk(m_cacheMutex);
    auto it = m_cache.find(toolpathId);
    return it != m_cache.end() ? &it->second : nullptr;
}

// ─── Toolpath::computeStats ───────────────────────────────────────────────
void Toolpath::computeStats() {
    cuttingLength = 0; rapidLength = 0; estimatedTime = 0;
    minZ = std::numeric_limits<double>::max();
    maxZ = std::numeric_limits<double>::lowest();

    geometry::Vec3 prev{0,0,0};
    bool first = true;

    for (const auto& m : moves) {
        if (!first) {
            double dx   = m.position.x - prev.x;
            double dy   = m.position.y - prev.y;
            double dz   = m.position.z - prev.z;
            double dist = std::sqrt(dx*dx + dy*dy + dz*dz);

            bool isRapid = (m.type == Move::Type::Rapid ||
                            m.type == Move::Type::Retract);
            if (isRapid) {
                rapidLength   += dist;
                estimatedTime += dist / 5000.0;  // 5 m/min G0 sabit
            } else {
                cuttingLength += dist;
                double feed    = m.feedrate > 1.0 ? m.feedrate : 1000.0;
                estimatedTime += dist / feed;    // mm / (mm/min) = min
            }
        }
        minZ  = std::min(minZ, m.position.z);
        maxZ  = std::max(maxZ, m.position.z);
        prev  = m.position;
        first = false;
    }
}

} // namespace e3::toolpath
