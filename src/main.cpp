// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: main.cpp
// Uygulama giriş noktası
// ─────────────────────────────────────────────────────────────────────────────
#include "core/Application.h"
#include "core/Logger.h"
#include "api/APIServer.h"

#include <iostream>
#include <csignal>
#include <atomic>

static std::atomic<bool> g_running{true};

static void signalHandler(int sig) {
    (void)sig;
    g_running.store(false);
}

int main(int argc, char* argv[]) {
    // ── Sinyal yönetimi
    std::signal(SIGINT,  signalHandler);
    std::signal(SIGTERM, signalHandler);

    // ── Logger başlat
    e3::core::Logger::init("logs");
    E3_LOG_INFO("E3Studio v0.1.0 başlatılıyor");

    // ── Çekirdek uygulama
    auto& app = e3::core::Application::instance();
    if (!app.initialize()) {
        E3_LOG_ERROR("Uygulama başlatılamadı");
        return 1;
    }

    // ── WebSocket API sunucusu
    auto& api = e3::api::APIServer::instance();
    constexpr uint16_t PORT = 9001;
    api.start(PORT);
    E3_LOG_INFO("API sunucusu ws://localhost:{} adresinde dinliyor", PORT);

    // ── Ana döngü
    E3_LOG_INFO("Hazır. UI: http://localhost:3000");
    std::cout << "[E3Studio] ws://localhost:" << PORT << " hazir\n"
              << "[E3Studio] UI -> http://localhost:3000\n"
              << "Cikmak icin Ctrl+C\n";

    while (g_running.load()) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }

    // ── Temiz kapatma
    E3_LOG_INFO("Kapatılıyor...");
    api.stop();
    app.shutdown();

    return 0;
}
