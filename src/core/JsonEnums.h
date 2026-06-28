#pragma once
#include "ProjectManager.h"
#include <nlohmann/json.hpp>
#include <unordered_map>
#include <algorithm>
#include <cctype>

namespace e3::core {

inline int parseJsonInt(const nlohmann::json& j, const char* key, int defaultValue) {
    if (!j.contains(key)) return defaultValue;
    if (j[key].is_number_integer() || j[key].is_number_unsigned())
        return j[key].get<int>();
    if (j[key].is_number_float())
        return static_cast<int>(j[key].get<double>());
    return defaultValue;
}

inline Operation::Type parseOperationType(const nlohmann::json& j) {
    static const std::unordered_map<std::string, Operation::Type> byName = {
        {"Pocket2D", Operation::Type::Pocket2D},
        {"Contour2D", Operation::Type::Contour2D},
        {"SurfaceFinishing", Operation::Type::SurfaceFinishing},
        {"AdaptiveClearing", Operation::Type::AdaptiveClearing},
        {"Drilling", Operation::Type::Drilling},
        {"Threading", Operation::Type::Threading},
    };

    if (!j.contains("type")) return Operation::Type::Pocket2D;

    if (j["type"].is_string()) {
        auto name = j["type"].get<std::string>();
        auto it = byName.find(name);
        if (it != byName.end()) return it->second;
    }

    return static_cast<Operation::Type>(parseJsonInt(j, "type", 0));
}

inline Tool::Type parseToolType(const nlohmann::json& j) {
    static const std::unordered_map<std::string, Tool::Type> byName = {
        {"FlatEndmill", Tool::Type::FlatEndmill},
        {"BallEndmill", Tool::Type::BallEndmill},
        {"BullNose", Tool::Type::BullNose},
        {"Drill", Tool::Type::Drill},
        {"Tap", Tool::Type::Tap},
    };

    if (!j.contains("type")) return Tool::Type::FlatEndmill;

    if (j["type"].is_string()) {
        auto name = j["type"].get<std::string>();
        auto it = byName.find(name);
        if (it != byName.end()) return it->second;
    }

    return static_cast<Tool::Type>(parseJsonInt(j, "type", 0));
}

inline std::string operationTypeName(Operation::Type type) {
    switch (type) {
        case Operation::Type::Pocket2D: return "Pocket2D";
        case Operation::Type::Contour2D: return "Contour2D";
        case Operation::Type::SurfaceFinishing: return "SurfaceFinishing";
        case Operation::Type::AdaptiveClearing: return "AdaptiveClearing";
        case Operation::Type::Drilling: return "Drilling";
        case Operation::Type::Threading: return "Threading";
    }
    return "Pocket2D";
}

inline ModelRef::Role parseModelRole(const std::string& role) {
    std::string lower = role;
    std::transform(lower.begin(), lower.end(), lower.begin(),
        [](unsigned char c){ return static_cast<char>(std::tolower(c)); });
    if (lower == "stock") return ModelRef::Role::Stock;
    if (lower == "fixture") return ModelRef::Role::Fixture;
    return ModelRef::Role::Workpiece;
}

inline std::string modelRoleName(ModelRef::Role role) {
    switch (role) {
        case ModelRef::Role::Stock: return "stock";
        case ModelRef::Role::Fixture: return "fixture";
        default: return "workpiece";
    }
}

} // namespace e3::core
