#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Toolpath :: ToolpathEngine
// Tüm operasyon tiplerini koordine eder.
// ProjectManager'dan operasyonu alır → hesaplar → EventBus'a yayınlar.
// Tüm hesaplamalar ThreadPool üzerinde asenkron çalışır.
// ─────────────────────────────────────────────────────────────────────────────
#include "Toolpath.h"
#include "../core/ProjectManager.h"
#include <future>
#include <mutex>
#include <thread>
#include <unordered_map>
#include <memory>
#include <functional>

namespace e3::toolpath {

class IOperation;

class ToolpathEngine {
public:
    static ToolpathEngine& instance() {
        static ToolpathEngine engine;
        return engine;
    }

    // Tek operasyon için asenkron hesaplama başlat
    std::future<Toolpath> computeAsync(const std::string& operationId);

    // Tüm kirli operasyonları sıraya al
    void computeAllDirty();

    // Hesaplanan toolpath'e eriş
    const Toolpath* getToolpath(const std::string& toolpathId) const;

    // Progress callback (0.0-1.0)
    using ProgressCallback = std::function<void(const std::string& opId, float progress)>;
    void setProgressCallback(ProgressCallback cb) { m_progressCb = cb; }

private:
    ToolpathEngine() = default;
    Toolpath computeInternal(const core::Operation& op, const core::Tool& tool);

    mutable std::mutex                        m_cacheMutex;
    std::unordered_map<std::string, Toolpath> m_cache;
    ProgressCallback m_progressCb;
};

// ─── Operasyon Arayüzü ────────────────────────────────────────────────────
class IOperation {
public:
    virtual ~IOperation() = default;
    virtual Toolpath compute(
        const core::Operation& params,
        const core::Tool& tool,
        std::function<void(float)> progressCb) = 0;
};

} // namespace e3::toolpath
