#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: API :: MessageHandler
// ─────────────────────────────────────────────────────────────────────────────
#include <nlohmann/json.hpp>
#include <string>
#include <functional>

namespace e3::api {

using json             = nlohmann::json;
using ConnectionHandle = std::string; // ixwebsocket connection ID

class APIServer;

class MessageHandler {
public:
    explicit MessageHandler(APIServer& server) : m_server(server) {}

    json handle(const json& msg, const ConnectionHandle& hdl);

private:
    APIServer& m_server;

    // ─── Komut İşleyiciler ───────────────────────────────────────────────
    json handleProjectNew     (const json& payload);
    json handleProjectOpen    (const json& payload);
    json handleProjectSave    (const json& payload);
    json handleProjectGet     (const json& payload);

    json handleOperationAdd   (const json& payload);
    json handleOperationUpdate(const json& payload);
    json handleOperationRemove(const json& payload);
    json handleOperationCompute(const json& payload);

    json handleToolpathGet    (const json& payload);
    json handleToolpathExport (const json& payload); // G-Code

    json handleSimStart       (const json& payload);
    json handleSimStep        (const json& payload);
    json handleSimPause       (const json& payload);

    json handleAIOptimize     (const json& payload);

    json handleModelLoad      (const json& payload);
    json handleMeshGet        (const json& payload); // Three.js için

    // Başarı yanıtı oluştur
    json ok(const std::string& reqId, const json& data = {});
    json err(const std::string& reqId, const std::string& message);
};

} // namespace e3::api
