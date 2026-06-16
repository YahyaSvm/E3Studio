// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: APIServer.cpp  (ixwebsocket backend)
// ─────────────────────────────────────────────────────────────────────────────
#include "APIServer.h"
#include "MessageHandler.h"
#include "../core/Logger.h"
#include "../core/EventBus.h"
#include "../core/ProjectManager.h"

namespace e3::api {

APIServer::APIServer() {
    m_handler = std::make_unique<MessageHandler>(*this);

    // Subscribe to C++ events → push to UI
    auto& bus = core::EventBus::instance();

    bus.subscribe<core::ToolpathGeneratedEvent>([this](const auto& e) {
        broadcast({
            {"type", "toolpath.generated"},
            {"payload", {
                {"operationId",      e.operationId},
                {"pointCount",       e.pointCount},
                {"estimatedMinutes", e.estimatedTime}
            }}
        });
    });

    bus.subscribe<core::SimulationStepEvent>([this](const auto& e) {
        broadcast({
            {"type", "simulation.frame"},
            {"payload", {
                {"progress",          e.progress},
                {"remainingMaterial", e.remainingMaterial}
            }}
        });
    });

    bus.subscribe<core::AIOptimizationCompleteEvent>([this](const auto& e) {
        broadcast({
            {"type", "ai.prediction"},
            {"payload", {
                {"operationId",        e.operationId},
                {"feedrate",           e.suggestedFeedrate},
                {"spindle",            e.suggestedSpindle},
                {"predictedRoughness", e.predictedRoughness}
            }}
        });
    });
}

APIServer::~APIServer() { stop(); }

void APIServer::start(uint16_t port) {
    m_server = std::make_unique<ix::WebSocketServer>(static_cast<int>(port));

    m_server->setOnClientMessageCallback(
        [this](std::shared_ptr<ix::ConnectionState> state,
               ix::WebSocket& ws,
               const ix::WebSocketMessagePtr& msg)
        {
            const std::string id = state->getId();

            if (msg->type == ix::WebSocketMessageType::Open) {
                {
                    std::lock_guard lock(m_clientsMutex);
                    m_clients[id] = Client{state, &ws};
                }
                E3_LOG_INFO("UI baglandi [{}]. Toplam: {}", id, m_clients.size());

                ws.send(json{
                    {"type", "system.ready"},
                    {"payload", {
                        {"version",    "0.1.0"},
                        {"hasProject", core::ProjectManager::instance().hasProject()}
                    }}
                }.dump());
            }
            else if (msg->type == ix::WebSocketMessageType::Close) {
                {
                    std::lock_guard lock(m_clientsMutex);
                    m_clients.erase(id);
                }
                E3_LOG_INFO("UI baglantisi kesildi [{}]", id);
            }
            else if (msg->type == ix::WebSocketMessageType::Message) {
                try {
                    auto j        = json::parse(msg->str);
                    auto response = m_handler->handle(j, id);
                    if (!response.is_null())
                        ws.send(response.dump());
                }
                catch (const json::exception& e) {
                    E3_LOG_ERROR("Gecersiz JSON: {}", e.what());
                    ws.send(json{{"type","error"},{"message",e.what()}}.dump());
                }
            }
            else if (msg->type == ix::WebSocketMessageType::Error) {
                E3_LOG_WARN("WS hata [{}]: {}", id, msg->errorInfo.reason);
            }
        });

    auto res = m_server->listen();
    if (!res.first) {
        E3_LOG_ERROR("WebSocket sunucu dinleme baslatılamadi: {}", res.second);
        return;
    }
    m_server->start();
    m_running = true;
    E3_LOG_INFO("API sunucusu baslatildi — ws://localhost:{}", port);
}

void APIServer::stop() {
    if (!m_running) return;
    m_running = false;
    if (m_server) {
        m_server->stop();
        m_server.reset();
    }
    E3_LOG_INFO("API sunucusu durduruldu");
}

void APIServer::broadcast(const json& message) {
    std::lock_guard lock(m_clientsMutex);
    const std::string payload = message.dump();
    for (auto& [id, client] : m_clients) {
        if (client.ws)
            client.ws->send(payload);
    }
}

void APIServer::send(const ConnectionHandle& id, const json& message) {
    std::lock_guard lock(m_clientsMutex);
    auto it = m_clients.find(id);
    if (it != m_clients.end() && it->second.ws)
        it->second.ws->send(message.dump());
}

} // namespace e3::api
