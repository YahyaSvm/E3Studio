#include "ContourOperation.h"
#include "OperationHelpers.h"
#include "../../core/Logger.h"
#include <cmath>

namespace e3::toolpath::operations {

Toolpath ContourOperation::compute(
    const core::Operation& params,
    const core::Tool& tool,
    std::function<void(float)> progressCb)
{
    Toolpath result;
    result.id = params.id + "_contour_tp";
    result.operationId = params.id;
    result.toolId = params.toolId;

    auto shape = loadShapeForOperation(params);
    auto bbox = resolveBoundingBox(shape);
    auto& kernel = geometry::GeometryKernel::instance();

    double safeZ = bbox.max.z + 5.0;
    double toolRadius = tool.diameter / 2.0;
    double zStart = bbox.max.z;
    double zEnd = bbox.min.z + params.stockToLeave;
    int totalLevels = static_cast<int>(std::ceil((zStart - zEnd) / std::max(params.depthOfCut, 0.01)));

    result.moves.push_back(makeRapidMove({bbox.min.x, bbox.min.y, safeZ}));

    for (int level = 0; level < totalLevels; ++level) {
        if (progressCb) progressCb(static_cast<float>(level) / std::max(totalLevels, 1));
        double z = std::max(zStart - params.depthOfCut * (level + 1), zEnd);

        std::vector<std::vector<geometry::Vec3>> contours;
        if (shape) {
            auto slice = kernel.sliceAtZ(*shape, z);
            contours = slice.contours;
        }
        if (contours.empty()) {
            contours.push_back({
                {bbox.min.x, bbox.min.y, z},
                {bbox.max.x, bbox.min.y, z},
                {bbox.max.x, bbox.max.y, z},
                {bbox.min.x, bbox.max.y, z},
                {bbox.min.x, bbox.min.y, z},
            });
        }

        for (const auto& contour : contours) {
            if (contour.size() < 2) continue;
            auto offset = kernel.offsetContour(contour, toolRadius, true);
            if (offset.size() < 2) continue;

            result.moves.push_back(makeRapidMove({offset[0].x, offset[0].y, safeZ}));
            result.moves.push_back(makePlungeMove({offset[0].x, offset[0].y, z}, params.feedrateZ));

            for (size_t i = 1; i < offset.size(); ++i)
                result.moves.push_back(makeFeedMove(offset[i], params.feedrateXY));

            result.moves.push_back(makeRapidMove({offset[0].x, offset[0].y, safeZ}));
        }
    }

    if (progressCb) progressCb(1.0f);
    result.computeStats();
    return result;
}

} // namespace e3::toolpath::operations
