#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Core :: Application
// Tüm sistemi başlatan, yaşam döngüsünü yöneten merkezi sınıf.
// Singleton — her şey buradan erişilir.
// ─────────────────────────────────────────────────────────────────────────────
#include "Logger.h"
#include "EventBus.h"
#include "ThreadPool.h"
#include "ProjectManager.h"
#include <memory>
#include <atomic>

namespace e3::core {

class Application {
public:
    static Application& instance() {
        static Application app;
        return app;
    }

    // Sistemi başlat
    bool initialize();

    // Ana döngü — API sunucusu ve event pump
    void run();

    // Temiz kapatma
    void shutdown();

    bool isRunning() const { return m_running; }
    void requestStop() { m_running = false; }

    // Alt sistem erişimi
    ThreadPool& threadPool() { return *m_threadPool; }
    EventBus& eventBus() { return EventBus::instance(); }
    ProjectManager& projectManager() { return ProjectManager::instance(); }

private:
    Application() = default;
    std::unique_ptr<ThreadPool> m_threadPool;
    std::atomic<bool> m_running{false};
};

} // namespace e3::core
