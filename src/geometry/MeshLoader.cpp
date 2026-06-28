#include "GeometryKernel.h"
#include "../core/Logger.h"
#include <filesystem>
#include <algorithm>

namespace e3::geometry {

std::optional<Mesh> loadMeshFromFile(const std::string& path) {
    auto ext = std::filesystem::path(path).extension().string();
    std::transform(ext.begin(), ext.end(), ext.begin(),
        [](unsigned char c){ return static_cast<char>(std::tolower(c)); });

    auto& kernel = GeometryKernel::instance();
    if (ext == ".stl")
        return kernel.loadSTL(path);

    if (ext == ".step" || ext == ".stp") {
        auto shape = kernel.loadSTEP(path);
        if (!shape) return std::nullopt;
        return kernel.tessellate(*shape, 0.1);
    }

    if (ext == ".iges" || ext == ".igs") {
        auto shape = kernel.loadIGES(path);
        if (!shape) return std::nullopt;
        return kernel.tessellate(*shape, 0.1);
    }

    E3_LOG_WARN("Desteklenmeyen mesh dosyası: {}", path);
    return std::nullopt;
}

} // namespace e3::geometry
