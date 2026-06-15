#include "AdaptiveClearingOperation.h"
#include "../../core/Logger.h"
#include <cmath>
#include <numbers>

namespace e3::toolpath::operations {

static constexpr double PI = std::numbers::pi;

Toolpath AdaptiveClearingOperation::compute(
    const core::Operation& params,
    const core::Tool& tool,
    std::function<void(float)> progressCb)
{
    Toolpath result;
    result.id = params.id + "_adaptive_tp";
    result.operationId = params.id;
    result.toolId = params.toolId;

    auto& kernel = geometry::GeometryKernel::instance();
    auto* project = core::ProjectManager::instance().currentProject();
    if (!project) return result;

    auto modelIt = std::find_if(project->models.begin(), project->models.end(),
        [&](const core::ModelRef& m){ return m.id == params.geometryRef; });
    if (modelIt == project->models.end()) return result;

    auto shape = kernel.loadSTEP(modelIt->filePath);
    if (!shape) return result;

    auto bbox = kernel.getBoundingBox(*shape);
    double safeZ   = bbox.max.z + 5.0;
    double toolD   = tool.diameter;

    // Trochoidal parametreler
    TrochoidParams tp;
    tp.circleRadius   = toolD * 0.35;    // %35 çap kadar daire
    tp.stepForward    = toolD * 0.15;    // her dairede %15 ilerleme
    tp.maxEngagement  = 30.0;            // 30° maksimum temas
    tp.feedrate       = params.feedrateXY * 1.5; // Adaptive yüksek feed

    double zStart = bbox.max.z;
    double zEnd   = bbox.min.z + params.stockToLeave;
    int totalLevels = static_cast<int>(
        std::ceil((zStart - zEnd) / params.depthOfCut));

    result.moves.push_back({Move::Type::Rapid, {0,0,safeZ}, {0,0,1}, {}, 0});

    for (int lvl = 0; lvl < totalLevels; ++lvl) {
        if (progressCb) progressCb(static_cast<float>(lvl) / totalLevels);
        double z = zStart - params.depthOfCut * (lvl + 1);
        z = std::max(z, zEnd);

        auto slice = kernel.sliceAtZ(*shape, z);
        if (slice.contours.empty()) continue;

        for (const auto& contour : slice.contours) {
            if (contour.size() < 2) continue;

            // Kontur boyunca trochoidal geçişler
            for (size_t i = 0; i + 1 < contour.size(); ++i) {
                auto& start = contour[i];
                auto& end   = contour[i + 1];

                auto passStart = geometry::Vec3{start.x, start.y, safeZ};
                result.moves.push_back({Move::Type::Rapid, passStart, {0,0,1}, {}, 0});
                result.moves.push_back({Move::Type::PlungeFeed,
                    {start.x, start.y, z}, {0,0,1}, {}, params.feedrateZ});

                auto passMoves = generateTrochoidalPass(
                    {start.x, start.y, z}, {end.x, end.y, z}, tp);

                for (auto& m : passMoves)
                    result.moves.push_back(m);

                result.moves.push_back({Move::Type::Rapid,
                    {end.x, end.y, safeZ}, {0,0,1}, {}, 0});
            }
        }
    }

    if (progressCb) progressCb(1.0f);
    result.computeStats();
    E3_LOG_INFO("AdaptiveClearing: {} hareket, ~{:.1f} dk",
        result.moves.size(), result.estimatedTime);
    return result;
}

std::vector<Move> AdaptiveClearingOperation::generateTrochoidalPass(
    const geometry::Vec3& start,
    const geometry::Vec3& end,
    const TrochoidParams& p)
{
    std::vector<Move> moves;

    geometry::Vec3 dir = {end.x - start.x, end.y - start.y, 0};
    double totalLen = std::sqrt(dir.x*dir.x + dir.y*dir.y);
    if (totalLen < 1e-6) return moves;

    dir.x /= totalLen; dir.y /= totalLen;

    // Dike perpendicular vektör
    geometry::Vec3 perp = {-dir.y, dir.x, 0};

    double traveled = 0.0;
    bool side = true; // hangi yönde yarım daire

    while (traveled < totalLen) {
        geometry::Vec3 center = {
            start.x + dir.x * traveled,
            start.y + dir.y * traveled,
            start.z
        };

        // Yarım daire noktaları
        double startAngle = side ? 0.0 : PI;
        double endAngle   = side ? PI  : 2*PI;
        auto pts = arcPoints(center, p.circleRadius, startAngle, endAngle);

        for (const auto& pt : pts) {
            moves.push_back({
                Move::Type::Feed,
                pt, {0,0,1}, {}, p.feedrate
            });
        }

        traveled += p.stepForward;
        side = !side;
    }

    return moves;
}

std::vector<geometry::Vec3> AdaptiveClearingOperation::arcPoints(
    const geometry::Vec3& center,
    double radius,
    double startAngle,
    double endAngle,
    int segments)
{
    std::vector<geometry::Vec3> pts;
    double sweep = endAngle - startAngle;
    for (int i = 0; i <= segments; ++i) {
        double angle = startAngle + sweep * i / segments;
        pts.push_back({
            center.x + radius * std::cos(angle),
            center.y + radius * std::sin(angle),
            center.z
        });
    }
    return pts;
}

} // namespace e3::toolpath::operations
