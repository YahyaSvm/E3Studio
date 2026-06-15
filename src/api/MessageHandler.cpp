#include "MessageHandler.h"
#include "APIServer.h"
#include "../core/Logger.h"
#include "../core/ProjectManager.h"
#include "../toolpath/ToolpathEngine.h"
#include "../postprocessor/GCodeGenerator.h"
#include "../ai/CuttingParameterPredictor.h"
#include <unordered_map>
#include <functional>

namespace e3::api {

json MessageHandler::handle(const json& msg, const ConnectionHandle& hdl) {
    std::string type = msg.value("type", "");
    std::string reqId = msg.value("id", "");
    json payload = msg.value("payload", json{});

    E3_LOG_DEBUG("Komut: {}", type);

    static std::unordered_map<std::string,
        std::function<json(MessageHandler*, const json&)>> dispatch = {
        {"project.new",          [](auto* h, auto& p){ return h->handleProjectNew(p); }},
        {"project.open",         [](auto* h, auto& p){ return h->handleProjectOpen(p); }},
        {"project.save",         [](auto* h, auto& p){ return h->handleProjectSave(p); }},
        {"project.get",          [](auto* h, auto& p){ return h->handleProjectGet(p); }},
        {"operation.add",        [](auto* h, auto& p){ return h->handleOperationAdd(p); }},
        {"operation.update",     [](auto* h, auto& p){ return h->handleOperationUpdate(p); }},
        {"operation.remove",     [](auto* h, auto& p){ return h->handleOperationRemove(p); }},
        {"operation.compute",    [](auto* h, auto& p){ return h->handleOperationCompute(p); }},
        {"toolpath.get",         [](auto* h, auto& p){ return h->handleToolpathGet(p); }},
        {"toolpath.export",      [](auto* h, auto& p){ return h->handleToolpathExport(p); }},
        {"simulation.start",     [](auto* h, auto& p){ return h->handleSimStart(p); }},
        {"simulation.step",      [](auto* h, auto& p){ return h->handleSimStep(p); }},
        {"simulation.pause",     [](auto* h, auto& p){ return h->handleSimPause(p); }},
        {"ai.optimize",          [](auto* h, auto& p){ return h->handleAIOptimize(p); }},
        {"model.load",           [](auto* h, auto& p){ return h->handleModelLoad(p); }},
        {"mesh.get",             [](auto* h, auto& p){ return h->handleMeshGet(p); }},
    };

    auto it = dispatch.find(type);
    if (it == dispatch.end()) {
        return err(reqId, "Bilinmeyen komut: " + type);
    }

    try {
        auto result = it->second(this, payload);
        result["id"] = reqId; // yanıt ID'yi ekle
        return result;
    }
    catch (const std::exception& e) {
        E3_LOG_ERROR("Komut hatası [{}]: {}", type, e.what());
        return err(reqId, e.what());
    }
}

json MessageHandler::handleProjectNew(const json& payload) {
    std::string name = payload.value("name", "Yeni Proje");
    core::ProjectManager::instance().newProject(name);
    return ok("", {{"message", "Proje oluşturuldu"}});
}

json MessageHandler::handleProjectOpen(const json& payload) {
    std::string path = payload.at("path");
    bool success = core::ProjectManager::instance().loadProject(path);
    if (!success) return err("", "Proje yüklenemedi: " + path);
    return ok("", {{"message", "Proje yüklendi"}});
}

json MessageHandler::handleProjectSave(const json& payload) {
    std::string path = payload.value("path", "");
    bool success = path.empty()
        ? core::ProjectManager::instance().saveProjectIncremental()
        : core::ProjectManager::instance().saveProject(path);
    return success ? ok("") : err("", "Kaydedilemedi");
}

json MessageHandler::handleProjectGet(const json& payload) {
    auto* proj = core::ProjectManager::instance().currentProject();
    if (!proj) return err("", "Proje yok");
    return ok("", proj->toJson());
}

json MessageHandler::handleOperationAdd(const json& payload) {
    core::Operation op = core::Operation::fromJson(payload);
    std::string id = core::ProjectManager::instance().addOperation(op);
    return ok("", {{"id", id}});
}

json MessageHandler::handleOperationCompute(const json& payload) {
    std::string opId = payload.at("operationId");

    // Asenkron hesaplama başlat — sonuç EventBus üzerinden gelecek
    toolpath::ToolpathEngine::instance().computeAsync(opId);

    return ok("", {{"message", "Hesaplama başlatıldı"}, {"operationId", opId}});
}

json MessageHandler::handleToolpathExport(const json& payload) {
    std::string tpId = payload.at("toolpathId");
    std::string outPath = payload.at("outputPath");
    std::string postId = payload.value("postProcessor", "fanuc");

    auto* tp = toolpath::ToolpathEngine::instance().getToolpath(tpId);
    if (!tp) return err("", "Toolpath bulunamadı");

    auto* proj = core::ProjectManager::instance().currentProject();
    if (!proj) return err("", "Proje yok");

    postprocessor::PostConfig cfg = (postId == "fanuc")
        ? postprocessor::posts::fanuc()
        : (postId == "heidenhain")
            ? postprocessor::posts::heidenhain()
            : postprocessor::posts::generic();

    postprocessor::GCodeGenerator gen(cfg);

    // Toolpath'e ait toolId ile doğru takımı bul; bulunamazsa ilk takımı kullan
    auto toolOpt = core::ProjectManager::instance().findTool(tp->toolId);
    if (!toolOpt && proj->toolLibrary.empty()) return err("", "Takım bulunamadı");
    const core::Tool& exportTool = toolOpt ? *toolOpt : proj->toolLibrary[0];

    auto gcode = gen.generate(*tp, exportTool, proj->machine);
    bool written = gen.writeToFile(gcode, outPath);
    return written ? ok("", {{"path", outPath}}) : err("", "Dosya yazılamadı");
}

json MessageHandler::handleAIOptimize(const json& payload) {
    ai::CuttingInput input;
    input.materialHardnessHRC = payload.value("hardnessHRC", 30.0f);
    input.toolDiameter        = payload.value("toolDiameter", 10.0f);
    input.toolType            = payload.value("toolType", 0);
    input.toolMaterial        = payload.value("toolMaterial", 0);
    input.axialDepth          = payload.value("axialDepth", 3.0f);
    input.radialStepover      = payload.value("radialStepover", 4.0f);
    input.operationType       = payload.value("operationType", 0);
    input.targetRoughness     = payload.value("targetRoughness", 1.6f);

    ai::CuttingParameterPredictor predictor("models/cutting_params.onnx");
    auto result = predictor.predict(input);
    if (!result) return err("", "AI tahmini başarısız");

    return ok("", {
        {"feedrate",            result->feedrate},
        {"spindleSpeed",        result->spindleSpeed},
        {"predictedRoughness",  result->predictedRoughness},
        {"toolLifeMinutes",     result->toolLifeMinutes},
        {"confidence",          result->confidence}
    });
}

json MessageHandler::handleMeshGet(const json& payload) {
    std::string modelId = payload.at("modelId");
    auto* proj = core::ProjectManager::instance().currentProject();
    if (!proj) return err("", "Proje yok");

    auto it = std::find_if(proj->models.begin(), proj->models.end(),
        [&](const core::ModelRef& m){ return m.id == modelId; });
    if (it == proj->models.end()) return err("", "Model bulunamadı");

    auto shape = geometry::GeometryKernel::instance().loadSTEP(it->filePath);
    if (!shape) return err("", "Model yüklenemedi");

    auto mesh = geometry::GeometryKernel::instance().tessellate(*shape, 0.1);
    auto buf = mesh.toInterleavedBuffer();

    return ok("", {
        {"vertexCount", mesh.vertices.size()},
        {"triangleCount", mesh.triangles.size()},
        {"buffer", buf}, // float array — Three.js'e doğrudan
        {"bbox", {
            {"min", {mesh.bbox.min.x, mesh.bbox.min.y, mesh.bbox.min.z}},
            {"max", {mesh.bbox.max.x, mesh.bbox.max.y, mesh.bbox.max.z}}
        }}
    });
}

json MessageHandler::handleModelLoad(const json& payload) {
    std::string filePath = payload.at("filePath");
    std::string role = payload.value("role", "workpiece");

    core::ModelRef ref;
    ref.id = "model_" + std::to_string(std::hash<std::string>{}(filePath));
    ref.filePath = filePath;
    ref.role = (role == "stock") ? core::ModelRef::Role::Stock
             : (role == "fixture") ? core::ModelRef::Role::Fixture
             : core::ModelRef::Role::Workpiece;

    auto id = core::ProjectManager::instance().addModel(ref);
    return ok("", {{"modelId", id}});
}

json MessageHandler::handleSimStart(const json& payload) {
    return ok("", {{"message", "Simülasyon başlatıldı"}});
}

json MessageHandler::handleSimStep(const json& payload) {
    return ok("", {{"message", "Adım ilerledi"}});
}

json MessageHandler::handleSimPause(const json& payload) {
    return ok("", {{"message", "Simülasyon durduruldu"}});
}

json MessageHandler::handleOperationUpdate(const json& payload) {
    std::string id = payload.at("id");
    auto op = core::Operation::fromJson(payload);
    bool ok_ = core::ProjectManager::instance().updateOperation(id, op);
    return ok_ ? ok("") : err("", "Operasyon bulunamadı");
}

json MessageHandler::handleOperationRemove(const json& payload) {
    std::string id = payload.at("id");
    core::ProjectManager::instance().removeOperation(id);
    return ok("");
}

json MessageHandler::handleToolpathGet(const json& payload) {
    std::string id = payload.at("toolpathId");
    auto* tp = toolpath::ToolpathEngine::instance().getToolpath(id);
    if (!tp) return err("", "Toolpath bulunamadı");
    return ok("", {
        {"moveCount", tp->moveCount()},
        {"estimatedTime", tp->estimatedTime},
        {"cuttingLength", tp->cuttingLength}
    });
}

json MessageHandler::ok(const std::string& reqId, const json& data) {
    json r = {{"status", "ok"}};
    if (!data.is_null() && !data.empty()) r["data"] = data;
    if (!reqId.empty()) r["id"] = reqId;
    return r;
}

json MessageHandler::err(const std::string& reqId, const std::string& message) {
    json r = {{"status", "error"}, {"message", message}};
    if (!reqId.empty()) r["id"] = reqId;
    return r;
}

} // namespace e3::api
