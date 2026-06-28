// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: ProjectManager.cpp
// ─────────────────────────────────────────────────────────────────────────────
#include "ProjectManager.h"
#include "Logger.h"
#include "JsonEnums.h"
#include <fstream>
#include <sstream>
#include <chrono>
#include <atomic>
#include <algorithm>
#include <cstdio>

using json = nlohmann::json;

namespace e3::core {

// ─── JSON helpers ─────────────────────────────────────────────────────────────

nlohmann::json Tool::toJson() const {
    return {
        {"id", id}, {"name", name},
        {"type", static_cast<int>(type)},
        {"diameter", diameter},
        {"cornerRadius", cornerRadius},
        {"flutes", flutes},
        {"overallLength", overallLength},
        {"cuttingLength", cuttingLength},
        {"material", material}
    };
}

Tool Tool::fromJson(const nlohmann::json& j) {
    Tool t;
    t.id            = j.value("id", "");
    t.name          = j.value("name", "");
    t.type          = parseToolType(j);
    t.diameter      = j.value("diameter", 10.0);
    t.cornerRadius  = j.value("cornerRadius", 0.0);
    t.flutes        = j.value("flutes", 4.0);
    t.overallLength = j.value("overallLength", 75.0);
    t.cuttingLength = j.value("cuttingLength", 25.0);
    t.material      = j.value("material", "Carbide");
    return t;
}

nlohmann::json Operation::toJson() const {
    return {
        {"id", id}, {"name", name},
        {"type", static_cast<int>(type)},
        {"typeName", operationTypeName(type)},
        {"toolId", toolId},
        {"geometryRef", geometryRef},
        {"feedrateXY", feedrateXY}, {"feedrateZ", feedrateZ},
        {"spindleSpeed", spindleSpeed},
        {"depthOfCut", depthOfCut}, {"stepover", stepover},
        {"stockToLeave", stockToLeave}, {"tolerance", tolerance},
        {"isDirty", isDirty}, {"toolpathId", toolpathId}
    };
}

Operation Operation::fromJson(const nlohmann::json& j) {
    Operation op;
    op.id           = j.value("id", "");
    op.name         = j.value("name", "");
    op.type         = parseOperationType(j);
    op.toolId       = j.value("toolId", "");
    op.geometryRef  = j.value("geometryRef", "");
    op.feedrateXY   = j.value("feedrateXY", 1200.0);
    op.feedrateZ    = j.value("feedrateZ", 400.0);
    op.spindleSpeed = j.value("spindleSpeed", 8000.0);
    op.depthOfCut   = j.value("depthOfCut", 2.0);
    op.stepover     = j.value("stepover", 4.0);
    op.stockToLeave = j.value("stockToLeave", 0.0);
    op.tolerance    = j.value("tolerance", 0.01);
    op.isDirty      = j.value("isDirty", true);
    op.toolpathId   = j.value("toolpathId", "");
    return op;
}

nlohmann::json modelRefToJson(const ModelRef& m) {
    return {
        {"id", m.id},
        {"filePath", m.filePath},
        {"role", modelRoleName(m.role)},
        {"transform", m.transform}
    };
}

ModelRef modelRefFromJson(const nlohmann::json& j) {
    ModelRef m;
    m.id = j.value("id", "");
    m.filePath = j.value("filePath", "");
    m.role = parseModelRole(j.value("role", "workpiece"));
    if (j.contains("transform") && j["transform"].is_array()) {
        for (size_t i = 0; i < m.transform.size() && i < j["transform"].size(); ++i)
            m.transform[i] = j["transform"][i].get<double>();
    }
    return m;
}

nlohmann::json machineToJson(const MachineConfig& mc) {
    return {
        {"id", mc.id},
        {"name", mc.name},
        {"axes", mc.axes},
        {"maxFeedXY", mc.maxFeedXY},
        {"maxFeedZ", mc.maxFeedZ},
        {"maxSpindle", mc.maxSpindle},
        {"postProcessorId", mc.postProcessorId}
    };
}

MachineConfig machineFromJson(const nlohmann::json& j) {
    MachineConfig mc;
    mc.id = j.value("id", "default");
    mc.name = j.value("name", "3-Axis Mill");
    mc.axes = j.value("axes", 3);
    mc.maxFeedXY = j.value("maxFeedXY", 5000.0);
    mc.maxFeedZ = j.value("maxFeedZ", 2000.0);
    mc.maxSpindle = j.value("maxSpindle", 24000.0);
    mc.postProcessorId = j.value("postProcessorId", j.value("postProcessor", "fanuc"));
    return mc;
}

nlohmann::json Project::toJson() const {
    json j;
    j["id"]   = id;
    j["name"] = name;
    j["createdAt"] = createdAt;
    j["updatedAt"] = updatedAt;
    j["outputDir"] = outputDir;

    json toolsArr = json::array();
    for (const auto& t : toolLibrary) toolsArr.push_back(t.toJson());
    j["toolLibrary"] = toolsArr;

    json opsArr = json::array();
    for (const auto& op : operations) opsArr.push_back(op.toJson());
    j["operations"] = opsArr;

    json modelsArr = json::array();
    for (const auto& m : models) modelsArr.push_back(modelRefToJson(m));
    j["models"] = modelsArr;

    j["machine"] = machineToJson(machine);

    return j;
}

Project Project::fromJson(const nlohmann::json& j) {
    Project p;
    p.id         = j.value("id", "");
    p.name       = j.value("name", "");
    p.createdAt  = j.value("createdAt", "");
    p.updatedAt  = j.value("updatedAt", "");
    p.outputDir  = j.value("outputDir", "");
    for (const auto& jt : j.value("toolLibrary", json::array()))
        p.toolLibrary.push_back(Tool::fromJson(jt));
    for (const auto& jo : j.value("operations", json::array()))
        p.operations.push_back(Operation::fromJson(jo));
    for (const auto& jm : j.value("models", json::array()))
        p.models.push_back(modelRefFromJson(jm));
    if (j.contains("machine"))
        p.machine = machineFromJson(j["machine"]);
    return p;
}

// ─── ID generator ────────────────────────────────────────────────────────────
static std::string genId() {
    static std::atomic<uint64_t> counter{0};
    auto ts  = std::chrono::high_resolution_clock::now().time_since_epoch().count();
    auto cnt = counter.fetch_add(1);
    char buf[64];
    std::snprintf(buf, sizeof(buf), "%016llx%04llx",
                  static_cast<unsigned long long>(ts),
                  static_cast<unsigned long long>(cnt) & 0xFFFF);
    return std::string(buf);
}

// ─── ProjectManager ──────────────────────────────────────────────────────────

void ProjectManager::newProject(const std::string& name) {
    m_project = std::make_unique<Project>();
    m_project->id   = genId();
    m_project->name = name;
    m_project->machine = MachineConfig{};
    m_project->machine.id = "default";
    m_project->machine.name = "3-Axis Mill";

    Tool defaultTool;
    defaultTool.id = genId();
    defaultTool.name = "6mm Flat Endmill";
    defaultTool.type = Tool::Type::FlatEndmill;
    defaultTool.diameter = 6.0;
    m_project->toolLibrary.push_back(defaultTool);

    E3_LOG_INFO("Yeni proje oluşturuldu: {}", name);
}

bool ProjectManager::loadProject(const std::filesystem::path& path) {
    std::ifstream f(path);
    if (!f) {
        E3_LOG_ERROR("Proje açılamadı: {}", path.string());
        return false;
    }
    json j;
    try { f >> j; }
    catch (const json::parse_error& e) {
        E3_LOG_ERROR("JSON parse hatası: {}", e.what());
        return false;
    }
    m_project = std::make_unique<Project>(Project::fromJson(j));
    m_lastSavePath = path;
    E3_LOG_INFO("Proje yüklendi: {} ({} op)", m_project->name, m_project->operations.size());
    return true;
}

bool ProjectManager::saveProject(const std::filesystem::path& path) {
    if (!m_project) return false;
    std::ofstream f(path);
    if (!f) {
        E3_LOG_ERROR("Kayıt başarısız: {}", path.string());
        return false;
    }
    f << m_project->toJson().dump(2);
    m_lastSavePath = path;
    E3_LOG_INFO("Proje kaydedildi: {}", path.string());
    return true;
}

bool ProjectManager::saveProjectIncremental() {
    if (m_lastSavePath.empty()) return false;
    return saveProject(m_lastSavePath);
}

std::string ProjectManager::addOperation(Operation op) {
    if (!m_project) return {};
    if (op.id.empty()) op.id = genId();
    m_project->operations.push_back(op);
    return op.id;
}

bool ProjectManager::updateOperation(const std::string& id, Operation op) {
    if (!m_project) return false;
    for (auto& o : m_project->operations) {
        if (o.id == id) {
            op.id = id;
            op.isDirty = true;
            o = std::move(op);
            return true;
        }
    }
    return false;
}

bool ProjectManager::removeOperation(const std::string& id) {
    if (!m_project) return false;
    auto& ops = m_project->operations;
    auto it = std::find_if(ops.begin(), ops.end(), [&](const Operation& o){ return o.id == id; });
    if (it == ops.end()) return false;
    ops.erase(it);
    return true;
}

std::optional<Operation> ProjectManager::findOperation(const std::string& id) const {
    if (!m_project) return std::nullopt;
    for (const auto& op : m_project->operations)
        if (op.id == id) return op;
    return std::nullopt;
}

std::string ProjectManager::addTool(Tool tool) {
    if (!m_project) return {};
    if (tool.id.empty()) tool.id = genId();
    m_project->toolLibrary.push_back(tool);
    return tool.id;
}

std::optional<Tool> ProjectManager::findTool(const std::string& id) const {
    if (!m_project) return std::nullopt;
    for (const auto& t : m_project->toolLibrary)
        if (t.id == id) return t;
    return std::nullopt;
}

std::string ProjectManager::addModel(ModelRef model) {
    if (!m_project) return {};
    if (model.id.empty()) model.id = genId();
    m_project->models.push_back(model);
    return model.id;
}

bool ProjectManager::removeTool(const std::string& id) {
    if (!m_project) return false;
    auto& tools = m_project->toolLibrary;
    auto it = std::find_if(tools.begin(), tools.end(), [&](const Tool& t){ return t.id == id; });
    if (it == tools.end()) return false;
    tools.erase(it);
    return true;
}

bool ProjectManager::updateTool(const std::string& id, Tool tool) {
    if (!m_project) return false;
    for (auto& t : m_project->toolLibrary) {
        if (t.id == id) {
            tool.id = id;
            t = std::move(tool);
            return true;
        }
    }
    return false;
}

std::vector<std::string> ProjectManager::getDirtyOperationIds() const {
    if (!m_project) return {};
    std::vector<std::string> ids;
    for (const auto& op : m_project->operations)
        if (op.isDirty) ids.push_back(op.id);
    return ids;
}

void ProjectManager::markOperationClean(const std::string& id, const std::string& toolpathId) {
    if (!m_project) return;
    for (auto& op : m_project->operations) {
        if (op.id == id) {
            op.isDirty    = false;
            op.toolpathId = toolpathId;
            return;
        }
    }
}

} // namespace e3::core
