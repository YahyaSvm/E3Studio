#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Machine :: MachineKinematics
// Hedef tezgahın kinematik modelini bilir.
// Toolpath noktalarını tezgah eksen koordinatlarına çevirir.
// Eksen limitlerini kontrol eder, collision uyarısı verir.
// ─────────────────────────────────────────────────────────────────────────────
#include "../core/ProjectManager.h"
#include "../geometry/GeometryKernel.h"
#include "../toolpath/Toolpath.h"
#include <optional>
#include <vector>

namespace e3::machine {

using Vec3 = geometry::Vec3;

// ─── Kinematik Sonuç ──────────────────────────────────────────────────────
struct AxisPosition {
    double X, Y, Z;
    double A = 0.0; // 4. eksen (rotasyon X etrafında)
    double B = 0.0; // 5. eksen (rotasyon Y etrafında)
    double C = 0.0; // 5. eksen alternatif (rotasyon Z etrafında)
};

struct KinematicsResult {
    bool isValid;
    AxisPosition axes;
    std::string errorReason; // limit aşımı, singular nokta, vb.
};

// ─── Makine Tipi ──────────────────────────────────────────────────────────
enum class MachineType {
    Cartesian3Axis,     // XYZ — en yaygın freze
    TableTilt_BC,       // B+C döner tabla — 5 eksen (DMG, Haas)
    SpindleTilt_AC,     // A+C döner iş mili — 5 eksen
    RTCP               // Rotation around Tool Center Point — modern 5 eksen
};

class MachineKinematics {
public:
    explicit MachineKinematics(const core::MachineConfig& cfg);

    // İleri kinematik: eksen pozisyonundan kartezyen konum
    Vec3 forwardKinematics(const AxisPosition& axes) const;

    // Ters kinematik: kartezyen nokta + alet yönü → eksen pozisyonları
    // 5 eksen için birden fazla çözüm olabilir → en yakın döner
    KinematicsResult inverseKinematics(
        const Vec3& toolTip,
        const Vec3& toolAxis,
        const AxisPosition& currentAxes) const;

    // Tüm toolpath'ı dönüştür ve doğrula
    struct ValidationResult {
        bool isValid;
        std::vector<size_t> limitViolationIndices;  // limit aşan hareket indexleri
        std::vector<size_t> singularityIndices;     // tekillik noktaları
    };
    ValidationResult validateToolpath(const toolpath::Toolpath& tp) const;

    // Collision detection (alet + bağlama takımı)
    bool checkCollision(const AxisPosition& axes, const Vec3& toolTip) const;

    const core::MachineConfig& config() const { return m_config; }

private:
    core::MachineConfig m_config;

    bool isWithinLimits(const AxisPosition& axes) const;

    // TableTilt B+C kinematik
    KinematicsResult inverseBC(
        const Vec3& toolTip,
        const Vec3& toolAxis,
        const AxisPosition& current) const;
};

} // namespace e3::machine
