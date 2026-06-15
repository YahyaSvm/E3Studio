#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: API :: APIServer  (ixwebsocket backend)
// ─────────────────────────────────────────────────────────────────────────────
#include <ixwebsocket/IXWebSocketServer.h>
#include <nlohmann/json.hpp>
#include <string>
#include <memory>
#include <map>
#include <mutex>

namespace e3::api {

using json             = nlohmann::json;
using ConnectionHandle = std::string; // ixwebsocket connection ID

class MessageHandler;

class APIServer {
public:
    static APIServer& instance() {
        static APIServer srv;
        return srv;
    }

    void start(uint16_t port = 9001);
    void broadcast(const json& message);
    void send(const ConnectionHandle& id, const json& message);
    void stop();
    bool isRunning() const { return m_running; }

private:
    APIServer();
    ~APIServer();

    struct Client {
        std::shared_ptr<ix::ConnectionState> state;
        ix::WebSocket*                       ws = nullptr;
    };

    std::unique_ptr<ix::WebSocketServer>          m_server;
    std::map<std::string, Client>                 m_clients;
    std::mutex                                    m_clientsMutex;
    bool                                          m_running = false;

    std::unique_ptr<MessageHandler> m_handler;
};

} // namespace e3::api
