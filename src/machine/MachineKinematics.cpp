// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: MachineKinematics.cpp
// ─────────────────────────────────────────────────────────────────────────────
#include "MachineKinematics.h"
#include "../core/Logger.h"
#include <cmath>
#include <algorithm>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

namespace e3::machine {

static double deg2rad(double d) { return d * M_PI / 180.0; }
static double rad2deg(double r) { return r * 180.0 / M_PI; }

MachineKinematics::MachineKinematics(const core::MachineConfig& cfg)
    : m_config(cfg)
{
    E3_LOG_INFO("MachineKinematics: {} ({}-eksen) yüklendi", cfg.name, cfg.axes);
}

// ─── İleri kinematik ─────────────────────────────────────────────────────────
Vec3 MachineKinematics::forwardKinematics(const AxisPosition& axes) const {
    // 3-eksen: doğrudan XYZ
    if (m_config.axes <= 3) {
        return {axes.X, axes.Y, axes.Z};
    }
    // TableTilt B+C: pivot uzunluğu yok (MachineConfig'de yok), basit model
    const double B = deg2rad(axes.B);
    const double C = deg2rad(axes.C);
    // Takım yönü: Z ekseninden B ve C döndürülmüş
    double dx =  std::sin(B);
    double dy = -std::sin(C) * std::cos(B);
    double dz =  std::cos(C) * std::cos(B);
    return {axes.X + dx, axes.Y + dy, axes.Z + dz};
}

// ─── Ters kinematik (TableTilt B+C) ──────────────────────────────────────────
KinematicsResult MachineKinematics::inverseKinematics(
    const Vec3& toolTip,
    const Vec3& toolAxis,
    const AxisPosition& /*currentAxes*/) const
{
    KinematicsResult res;

    if (m_config.axes <= 3) {
        res.isValid = true;
        res.axes.X = toolTip.x;
        res.axes.Y = toolTip.y;
        res.axes.Z = toolTip.z;
        res.axes.B = 0;
        res.axes.C = 0;
        return res;
    }

    return inverseBC(toolTip, toolAxis, {});
}

KinematicsResult MachineKinematics::inverseBC(
    const Vec3& toolTip,
    const Vec3& toolAxis,
    const AxisPosition& /*current*/) const
{
    KinematicsResult res;
    const double dx = toolAxis.x;
    const double dy = toolAxis.y;
    const double dz = toolAxis.z;

    // B = asin(dx)
    double B = std::asin(std::clamp(dx, -1.0, 1.0));
    double cosB = std::cos(B);
    double C = 0.0;
    if (std::abs(cosB) > 1e-9)
        C = std::atan2(-dy / cosB, dz / cosB);

    res.axes.X = toolTip.x;
    res.axes.Y = toolTip.y;
    res.axes.Z = toolTip.z;
    res.axes.B = rad2deg(B);
    res.axes.C = rad2deg(C);

    res.isValid = isWithinLimits(res.axes);
    if (!res.isValid)
        res.errorReason = "Eksen limit aşımı B=" + std::to_string(res.axes.B)
                         + " C=" + std::to_string(res.axes.C);
    return res;
}

bool MachineKinematics::isWithinLimits(const AxisPosition& axes) const {
    if (axes.X < 0 || axes.X > m_config.workEnvX) return false;
    if (axes.Y < 0 || axes.Y > m_config.workEnvY) return false;
    if (axes.Z < -m_config.workEnvZ || axes.Z > 0) return false;
    if (m_config.axes >= 5) {
        if (axes.B < -110.0 || axes.B > 110.0) return false;
        if (axes.C < -360.0 || axes.C > 360.0) return false;
    }
    return true;
}

// ─── Toolpath doğrulama ───────────────────────────────────────────────────────
MachineKinematics::ValidationResult MachineKinematics::validateToolpath(
    const toolpath::Toolpath& tp) const
{
    ValidationResult result;
    result.isValid = true;

    for (size_t i = 0; i < tp.moves.size(); ++i) {
        const auto& move = tp.moves[i];
        auto ik = inverseKinematics(move.position, move.toolAxis, {});
        if (!ik.isValid) {
            result.isValid = false;
            result.limitViolationIndices.push_back(i);
        }
    }

    E3_LOG_INFO("Toolpath doğrulama: {} hareket, {} limit ihlali",
                tp.moves.size(), result.limitViolationIndices.size());
    return result;
}

// ─── Çarpışma tespiti ─────────────────────────────────────────────────────────
bool MachineKinematics::checkCollision(const AxisPosition& /*axes*/,
                                        const Vec3& toolTip) const
{
    // Basit: takım tablo altına iniyor mu?
    if (toolTip.z < -m_config.workEnvZ) {
        E3_LOG_WARN("Çarpışma: Z={:.3f} < -{:.0f}", toolTip.z, m_config.workEnvZ);
        return true;
    }
    return false;
}

} // namespace e3::machine
