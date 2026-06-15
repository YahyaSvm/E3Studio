#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Core :: Logger
// Merkezi loglama. Konsol + dosyaya eş zamanlı yazar.
// ─────────────────────────────────────────────────────────────────────────────
#include <spdlog/spdlog.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/sinks/base_sink.h>
#include <memory>
#include <string>

namespace e3::core {

class Logger {
public:
    static void init(const std::string& logDir = "logs") {
        auto consoleSink = std::make_shared<spdlog::sinks::stdout_color_sink_mt>();
        consoleSink->set_level(spdlog::level::debug);
        consoleSink->set_pattern("[%H:%M:%S.%e] [%^%l%$] [%n] %v");

        auto fileSink = std::make_shared<spdlog::sinks::rotating_file_sink_mt>(
            logDir + "/e3studio.log", 5 * 1024 * 1024, 3); // 5MB x 3 dosya
        fileSink->set_level(spdlog::level::trace);
        fileSink->set_pattern("[%Y-%m-%d %H:%M:%S.%e] [%l] [%n] %v");

        std::vector<spdlog::sink_ptr> sinks{consoleSink, fileSink};
        auto logger = std::make_shared<spdlog::logger>("E3", sinks.begin(), sinks.end());
        logger->set_level(spdlog::level::trace);

        spdlog::register_logger(logger);
        spdlog::set_default_logger(logger);
        spdlog::flush_on(spdlog::level::warn);
    }

    // Modül bazlı alt logger
    static std::shared_ptr<spdlog::logger> getLogger(const std::string& module) {
        auto existing = spdlog::get(module);
        if (existing) return existing;

        auto parent = spdlog::default_logger();
        auto child = parent->clone(module);
        spdlog::register_logger(child);
        return child;
    }

    static void shutdown() {
        spdlog::shutdown();
    }
};

// Kolaylık makroları — her kaynak dosyada kullanılır
#define E3_LOG_TRACE(...)    spdlog::trace(__VA_ARGS__)
#define E3_LOG_DEBUG(...)    spdlog::debug(__VA_ARGS__)
#define E3_LOG_INFO(...)     spdlog::info(__VA_ARGS__)
#define E3_LOG_WARN(...)     spdlog::warn(__VA_ARGS__)
#define E3_LOG_ERROR(...)    spdlog::error(__VA_ARGS__)
#define E3_LOG_CRITICAL(...) spdlog::critical(__VA_ARGS__)

} // namespace e3::core
