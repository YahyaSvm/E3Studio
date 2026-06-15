#include "PocketOperation.h"
#include "../../core/Logger.h"
#include <algorithm>
#include <cmath>
#include <limits>

namespace e3::toolpath::operations {

Toolpath PocketOperation::compute(
    const core::Operation& params,
    const core::Tool& tool,
    std::function<void(float)> progressCb)
{
    Toolpath result;
    result.id = params.id + "_tp";
    result.operationId = params.id;
    result.toolId = params.toolId;

    auto& kernel = geometry::GeometryKernel::instance();
    auto& projMgr = core::ProjectManager::instance();

    // Model yükle
    auto* project = projMgr.currentProject();
    if (!project) {
        E3_LOG_ERROR("PocketOperation: Proje yok");
        return result;
    }

    // Geometriyi bul ve yükle
    auto modelIt = std::find_if(project->models.begin(), project->models.end(),
        [&](const core::ModelRef& m) { return m.id == params.geometryRef; });
    if (modelIt == project->models.end()) {
        E3_LOG_ERROR("Geometri bulunamadı: {}", params.geometryRef);
        return result;
    }

    auto shape = kernel.loadSTEP(modelIt->filePath);
    if (!shape) return result;

    auto bbox = kernel.getBoundingBox(*shape);

    // Güvenli yükseklik
    double safeZ = bbox.max.z + 5.0;
    double toolRadius = tool.diameter / 2.0;
    double stepover = params.stepover > 0 ? params.stepover : tool.diameter * 0.4;

    // Başlangıç: güvenli yüksekliğe çık
    result.moves.push_back(makeRapidMove({0, 0, safeZ}));

    // Z seviyeleri hesapla
    double zStart = bbox.max.z;
    double zEnd   = bbox.min.z + params.stockToLeave;
    double zStep  = -std::abs(params.depthOfCut);
    int totalLevels = static_cast<int>(std::ceil((zStart - zEnd) / std::abs(zStep)));

    E3_LOG_INFO("PocketOperation: {} Z seviyesi, {}mm adım", totalLevels, zStep);

    for (int levelIdx = 0; levelIdx < totalLevels; ++levelIdx) {
        float progress = static_cast<float>(levelIdx) / totalLevels;
        if (progressCb) progressCb(progress);

        double z = zStart + zStep * (levelIdx + 1);
        z = std::max(z, zEnd);

        // Bu Z'de kesit al
        auto slice = kernel.sliceAtZ(*shape, z + params.stockToLeave);
        if (slice.contours.empty()) continue;

        // Her kontur için işleme
        auto levelMoves = processZLevel(
            slice.contours, z,
            toolRadius, stepover,
            params.feedrateXY,
            FillStrategy::Concentric);

        // Safe Z'ye çık → pozisyona git → plunge → kes → retract
        if (!levelMoves.empty()) {
            // İlk noktaya rapid git
            auto firstPos = levelMoves.front().position;
            result.moves.push_back(makeRapidMove({firstPos.x, firstPos.y, safeZ}));

            // Z iniş (plunge hızında)
            Move plunge;
            plunge.type = Move::Type::PlungeFeed;
            plunge.position = {firstPos.x, firstPos.y, z};
            plunge.feedrate = params.feedrateZ;
            plunge.toolAxis = {0, 0, 1};
            result.moves.push_back(plunge);

            // Kesme hareketleri
            for (auto& m : levelMoves)
                result.moves.push_back(m);

            // Retract
            result.moves.push_back(makeRapidMove({firstPos.x, firstPos.y, safeZ}));
        }
    }

    if (progressCb) progressCb(1.0f);
    result.computeStats();
    E3_LOG_INFO("Pocket tamamlandı: {} hareket, ~{:.1f} dk",
        result.moves.size(), result.estimatedTime);
    return result;
}

std::vector<Move> PocketOperation::processZLevel(
    const std::vector<std::vector<geometry::Vec3>>& contours,
    double z,
    double toolRadius,
    double stepover,
    double feedrate,
    FillStrategy strategy)
{
    std::vector<Move> moves;
    auto& kernel = geometry::GeometryKernel::instance();

    for (const auto& contour : contours) {
        if (contour.size() < 3) continue;

        if (strategy == FillStrategy::Concentric) {
            auto rings = generateConcentric(contour, stepover);
            for (const auto& ring : rings) {
                bool first = true;
                for (const auto& pt : ring) {
                    Move m;
                    m.type = Move::Type::Feed;
                    m.position = {pt.x, pt.y, z};
                    m.feedrate = feedrate;
                    m.toolAxis = {0, 0, 1};
                    moves.push_back(m);
                    first = false;
                }
                // Halkayı kapat
                if (!ring.empty()) {
                    Move close;
                    close.type = Move::Type::Feed;
                    close.position = {ring[0].x, ring[0].y, z};
                    close.feedrate = feedrate;
                    close.toolAxis = {0, 0, 1};
                    moves.push_back(close);
                }
            }
        } else {
            // ZigZag
            auto pts = generateZigzag(contour, stepover);
            for (const auto& pt : pts) {
                moves.push_back(makeFeedMove({pt.x, pt.y, z}, feedrate));
            }
        }
    }
    return moves;
}

std::vector<std::vector<geometry::Vec3>> PocketOperation::generateConcentric(
    const std::vector<geometry::Vec3>& boundary,
    double stepover)
{
    std::vector<std::vector<geometry::Vec3>> rings;
    auto& kernel = geometry::GeometryKernel::instance();

    auto current = boundary;
    while (current.size() >= 3) {
        rings.push_back(current);
        auto shrunk = kernel.offsetContour(current, stepover, true);
        if (shrunk.size() < 3 || shrunk.size() >= current.size()) break;
        current = shrunk;
    }
    // İçten dışa sırala (daha iyi talaş kaldırma)
    std::reverse(rings.begin(), rings.end());
    return rings;
}

std::vector<geometry::Vec3> PocketOperation::generateZigzag(
    const std::vector<geometry::Vec3>& boundary,
    double stepover,
    double angle)
{
    // Bounding box bul
    double minX = std::numeric_limits<double>::max();
    double maxX = std::numeric_limits<double>::lowest();
    double minY = std::numeric_limits<double>::max();
    double maxY = std::numeric_limits<double>::lowest();

    for (const auto& p : boundary) {
        minX = std::min(minX, p.x); maxX = std::max(maxX, p.x);
        minY = std::min(minY, p.y); maxY = std::max(maxY, p.y);
    }

    std::vector<geometry::Vec3> pts;
    bool leftToRight = true;
    for (double y = minY; y <= maxY; y += stepover) {
        if (leftToRight)
            pts.push_back({minX, y, 0});
        else
            pts.push_back({maxX, y, 0});
        leftToRight = !leftToRight;
    }
    return pts;
}

Move PocketOperation::makeFeedMove(const geometry::Vec3& to, double feedrate) {
    Move m;
    m.type = Move::Type::Feed;
    m.position = to;
    m.feedrate = feedrate;
    m.toolAxis = {0, 0, 1};
    return m;
}

Move PocketOperation::makeRapidMove(const geometry::Vec3& to) {
    Move m;
    m.type = Move::Type::Rapid;
    m.position = to;
    m.feedrate = 0;
    m.toolAxis = {0, 0, 1};
    return m;
}

} // namespace e3::toolpath::operations
