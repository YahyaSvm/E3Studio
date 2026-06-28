#include "ThreadingOperation.h"
#include "OperationHelpers.h"
#include "../../core/Logger.h"
#include <cmath>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

namespace e3::toolpath::operations {

Toolpath ThreadingOperation::compute(
    const core::Operation& params,
    const core::Tool& tool,
    std::function<void(float)> progressCb)
{
    Toolpath result;
    result.id = params.id + "_thread_tp";
    result.operationId = params.id;
    result.toolId = params.toolId;

    auto bbox = resolveBoundingBox(loadShapeForOperation(params));
    double safeZ = bbox.max.z + 5.0;
    double centerX = (bbox.min.x + bbox.max.x) * 0.5;
    double centerY = (bbox.min.y + bbox.max.y) * 0.5;
    double radius = std::max(tool.diameter * 0.5, 1.0);
    double pitch = std::max(params.stepover, 0.5);
    double zStart = bbox.max.z;
    double zEnd = bbox.min.z + params.stockToLeave;
    int turns = static_cast<int>(std::ceil((zStart - zEnd) / pitch));

    result.moves.push_back(makeRapidMove({centerX + radius, centerY, safeZ}));
    result.moves.push_back(makePlungeMove({centerX + radius, centerY, zStart}, params.feedrateZ));

    for (int t = 0; t < turns; ++t) {
        if (progressCb) progressCb(static_cast<float>(t) / std::max(turns, 1));
        double z = std::max(zStart - pitch * (t + 1), zEnd);
        double angle = 2.0 * M_PI * (t + 1);
        geometry::Vec3 pt{
            centerX + radius * std::cos(angle),
            centerY + radius * std::sin(angle),
            z
        };
        result.moves.push_back(makeFeedMove(pt, params.feedrateXY));
    }

    result.moves.push_back(makeRapidMove({centerX + radius, centerY, safeZ}));
    if (progressCb) progressCb(1.0f);
    result.computeStats();
    return result;
}

} // namespace e3::toolpath::operations
