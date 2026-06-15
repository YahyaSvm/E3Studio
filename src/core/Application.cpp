#include "Application.h"
#include "Logger.h"
#include <stdexcept>

namespace e3::core {

bool Application::initialize() {
    try {
        Logger::init("logs");
        E3_LOG_INFO("E3Studio başlatılıyor...");

        // CPU çekirdek sayısı - 1 iş parçacığı (UI iş parçacığı için yer bırak)
        m_threadPool = std::make_unique<ThreadPool>();
        E3_LOG_INFO("ThreadPool başlatıldı — {} iş parçacığı", m_threadPool->threadCount());

        m_running = true;
        E3_LOG_INFO("Çekirdek sistem hazır.");
        return true;
    }
    catch (const std::exception& e) {
        E3_LOG_CRITICAL("Başlatma hatası: {}", e.what());
        return false;
    }
}

void Application::run() {
    E3_LOG_INFO("Ana döngü başladı");
    while (m_running) {
        std::this_thread::sleep_for(std::chrono::milliseconds(16)); // ~60fps tick
    }
    shutdown();
}

void Application::shutdown() {
    E3_LOG_INFO("E3Studio kapatılıyor...");
    m_threadPool.reset();
    Logger::shutdown();
}

} // namespace e3::core
