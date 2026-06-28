#pragma once
#include "../Toolpath.h"
#include "../../core/ProjectManager.h"
#include "../../geometry/GeometryKernel.h"
#include <TopoDS_Shape.hxx>
#include <algorithm>
#include <filesystem>
#include <optional>

namespace e3::toolpath::operations {

inline std::optional<TopoDS_Shape> loadShapeForOperation(const core::Operation& params) {
    auto* project = core::ProjectManager::instance().currentProject();
    if (!project) return std::nullopt;

    std::string geoRef = params.geometryRef;
    if (geoRef.empty() && !project->models.empty())
        geoRef = project->models.front().id;

    auto modelIt = std::find_if(project->models.begin(), project->models.end(),
        [&](const core::ModelRef& m){ return m.id == geoRef; });
    if (modelIt == project->models.end()) return std::nullopt;

    auto& kernel = geometry::GeometryKernel::instance();
    std::filesystem::path path(modelIt->filePath);
    auto ext = path.extension().string();
    std::transform(ext.begin(), ext.end(), ext.begin(),
        [](unsigned char c){ return static_cast<char>(std::tolower(c)); });

    if (ext == ".step" || ext == ".stp") return kernel.loadSTEP(modelIt->filePath);
    if (ext == ".iges" || ext == ".igs") return kernel.loadIGES(modelIt->filePath);
    return kernel.loadSTEP(modelIt->filePath);
}

inline geometry::BoundingBox resolveBoundingBox(const std::optional<TopoDS_Shape>& shape) {
    if (shape) return geometry::GeometryKernel::instance().getBoundingBox(*shape);
    return {{-50, -50, 0}, {50, 50, 10}};
}

inline Move makeRapidMove(const geometry::Vec3& to) {
    Move m;
    m.type = Move::Type::Rapid;
    m.position = to;
    m.toolAxis = {0, 0, 1};
    return m;
}

inline Move makeFeedMove(const geometry::Vec3& to, double feedrate) {
    Move m;
    m.type = Move::Type::Feed;
    m.position = to;
    m.feedrate = feedrate;
    m.toolAxis = {0, 0, 1};
    return m;
}

inline Move makePlungeMove(const geometry::Vec3& to, double feedrate) {
    Move m;
    m.type = Move::Type::PlungeFeed;
    m.position = to;
    m.feedrate = feedrate;
    m.toolAxis = {0, 0, 1};
    return m;
}

} // namespace e3::toolpath::operations
