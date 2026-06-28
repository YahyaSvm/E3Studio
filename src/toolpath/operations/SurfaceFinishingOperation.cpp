#include "SurfaceFinishingOperation.h"
#include "OperationHelpers.h"
#include "../../core/Logger.h"
#include <cmath>

namespace e3::toolpath::operations {

Toolpath SurfaceFinishingOperation::compute(
    const core::Operation& params,
    const core::Tool& tool,
    std::function<void(float)> progressCb)
{
    Toolpath result;
    result.id = params.id + "_surface_tp";
    result.operationId = params.id;
    result.toolId = params.toolId;

    auto bbox = resolveBoundingBox(loadShapeForOperation(params));
    double safeZ = bbox.max.z + 5.0;
    double stepover = params.stepover > 0 ? params.stepover : tool.diameter * 0.35;
    double zStart = bbox.max.z - params.stockToLeave;
    double zEnd = bbox.min.z + params.stockToLeave;
    int totalLevels = static_cast<int>(std::ceil((zStart - zEnd) / std::max(params.depthOfCut, 0.01)));

    result.moves.push_back(makeRapidMove({bbox.min.x, bbox.min.y, safeZ}));

    for (int level = 0; level < totalLevels; ++level) {
        if (progressCb) progressCb(static_cast<float>(level) / std::max(totalLevels, 1));
        double z = std::max(zStart - params.depthOfCut * (level + 1), zEnd);
        bool reverse = (level % 2) == 1;

        for (double y = bbox.min.y; y <= bbox.max.y + 1e-6; y += stepover) {
            geometry::Vec3 start{reverse ? bbox.max.x : bbox.min.x, y, z};
            geometry::Vec3 end{reverse ? bbox.min.x : bbox.max.x, y, z};

            result.moves.push_back(makeRapidMove({start.x, start.y, safeZ}));
            result.moves.push_back(makePlungeMove(start, params.feedrateZ));
            result.moves.push_back(makeFeedMove(end, params.feedrateXY));
            result.moves.push_back(makeRapidMove({end.x, end.y, safeZ}));
        }
    }

    if (progressCb) progressCb(1.0f);
    result.computeStats();
    return result;
}

} // namespace e3::toolpath::operations
