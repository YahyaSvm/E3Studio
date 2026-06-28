#include "MessageHandler.h"
#include "APIServer.h"
#include "../core/Logger.h"
#include "../core/ProjectManager.h"
#include "../toolpath/ToolpathEngine.h"
#include "../postprocessor/GCodeGenerator.h"
#include "../ai/CuttingParameterPredictor.h"
#include "../simulation/SimulationEngine.h"
#include "../core/JsonEnums.h"
#include <unordered_map>
#include <functional>
#include <filesystem>
#include <algorithm>

namespace e3::api {

namespace {
simulation::SimulationEngine g_simEngine;
size_t g_simMoveIndex = 0;

json toolpathToVisualizationJson(const toolpath::Toolpath& tp) {
    json points = json::array();
    json types = json::array();
    for (const auto& move : tp.moves) {
        points.push_back({move.position.x, move.position.y, move.position.z});
        types.push_back(move.type == toolpath::Move::Type::Rapid ? 0 : 1);
    }
    return {
        {"toolpathId", tp.id},
        {"operationId", tp.operationId},
        {"moveCount", tp.moveCount()},
        {"estimatedTime", tp.estimatedTime},
        {"points", points},
        {"types", types}
    };
}

std::optional<geometry::Mesh> loadModelMesh(const core::ModelRef& model) {
    auto ext = std::filesystem::path(model.filePath).extension().string();
    std::transform(ext.begin(), ext.end(), ext.begin(),
        [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
    auto& kernel = geometry::GeometryKernel::instance();

    if (ext == ".stl") {
        auto mesh = kernel.loadSTL(model.filePath);
        if (mesh) return *mesh;
    }
    if (ext == ".step" || ext == ".stp") {
        auto shape = kernel.loadSTEP(model.filePath);
        if (shape) return kernel.tessellate(*shape, 0.1);
    }
    if (ext == ".iges" || ext == ".igs") {
        auto shape = kernel.loadIGES(model.filePath);
        if (shape) return kernel.tessellate(*shape, 0.1);
    }
    return std::nullopt;
}
} // namespace

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
        {"tool.add",             [](auto* h, auto& p){ return h->handleToolAdd(p); }},
        {"tool.update",          [](auto* h, auto& p){ return h->handleToolUpdate(p); }},
        {"tool.remove",          [](auto* h, auto& p){ return h->handleToolRemove(p); }},
        {"tool.list",            [](auto* h, auto& p){ return h->handleToolList(p); }},
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
    auto* proj = core::ProjectManager::instance().currentProject();
    if (proj) {
        if (op.toolId.empty() && !proj->toolLibrary.empty())
            op.toolId = proj->toolLibrary.front().id;
        if (op.geometryRef.empty() && !proj->models.empty())
            op.geometryRef = proj->models.front().id;
    }
    std::string id = core::ProjectManager::instance().addOperation(op);
    return ok("", {{"id", id}});
}

json MessageHandler::handleOperationCompute(const json& payload) {
    std::string opId = payload.at("operationId");
    auto& engine = toolpath::ToolpathEngine::instance();
    auto tp = engine.computeAsync(opId).get();
    if (tp.isEmpty()) return err("", "Toolpath hesaplanamadi");

    engine.cacheToolpath(tp);
    core::ProjectManager::instance().markOperationClean(opId, tp.id);

    core::EventBus::instance().publish(core::ToolpathGeneratedEvent{
        opId, tp.id, tp.moveCount(), tp.estimatedTime
    });

    return ok("", toolpathToVisualizationJson(tp));
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
    if (it == proj->models.end()) return err("", "Model bulunamadi");

    auto meshOpt = loadModelMesh(*it);
    if (!meshOpt) return err("", "Model yuklenemedi");

    const auto& mesh = *meshOpt;
    auto buf = mesh.toInterleavedBuffer();

    return ok("", {
        {"vertexCount", mesh.vertices.size()},
        {"triangleCount", mesh.triangles.size()},
        {"buffer", buf},
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
    std::string toolpathId = payload.value("toolpathId", "");
    auto* tp = toolpath::ToolpathEngine::instance().getToolpath(toolpathId);
    if (!tp) return err("", "Toolpath bulunamadi");

    auto* proj = core::ProjectManager::instance().currentProject();
    if (!proj) return err("", "Proje yok");

    geometry::BoundingBox bbox{{-100, -100, 0}, {100, 100, 20}};
    if (!proj->models.empty()) {
        if (auto mesh = loadModelMesh(proj->models.front()))
            bbox = mesh->bbox;
    }

    auto toolOpt = core::ProjectManager::instance().findTool(tp->toolId);
    double toolDiameter = toolOpt ? toolOpt->diameter : 6.0;

    g_simEngine.setup(bbox, *tp, toolDiameter, 0.5);
    g_simMoveIndex = 0;

    return ok("", {{"message", "Simulasyon baslatildi"}, {"moveCount", tp->moveCount()}});
}

json MessageHandler::handleSimStep(const json& payload) {
    int steps = payload.value("steps", 1);
    size_t maxMoves = payload.value("maxMoves", g_simMoveIndex + static_cast<size_t>(steps));

    for (int i = 0; i < steps && g_simMoveIndex < maxMoves; ++i) {
        auto frame = g_simEngine.stepTo(g_simMoveIndex++);
        core::EventBus::instance().publish(core::SimulationStepEvent{
            frame.progress,
            static_cast<float>(frame.remainingVolume)
        });
    }

    return ok("", {
        {"moveIndex", g_simMoveIndex},
        {"progress", maxMoves > 0
            ? static_cast<float>(g_simMoveIndex) / static_cast<float>(maxMoves)
            : 0.0f}
    });
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
    if (!tp) return err("", "Toolpath bulunamadi");
    return ok("", toolpathToVisualizationJson(*tp));
}

json MessageHandler::handleToolAdd(const json& payload) {
    core::Tool tool = core::Tool::fromJson(payload);
    std::string id = core::ProjectManager::instance().addTool(tool);
    return ok("", {{"id", id}});
}

json MessageHandler::handleToolUpdate(const json& payload) {
    std::string id = payload.at("id");
    core::Tool tool = core::Tool::fromJson(payload);
    bool ok_ = core::ProjectManager::instance().updateTool(id, tool);
    return ok_ ? ok("") : err("", "Takim bulunamadi");
}

json MessageHandler::handleToolRemove(const json& payload) {
    std::string id = payload.at("id");
    bool ok_ = core::ProjectManager::instance().removeTool(id);
    return ok_ ? ok("") : err("", "Takim bulunamadi");
}

json MessageHandler::handleToolList(const json& payload) {
    (void)payload;
    auto* proj = core::ProjectManager::instance().currentProject();
    if (!proj) return ok("", {{"tools", json::array()}});
    json tools = json::array();
    for (const auto& t : proj->toolLibrary) tools.push_back(t.toJson());
    return ok("", {{"tools", tools}});
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
