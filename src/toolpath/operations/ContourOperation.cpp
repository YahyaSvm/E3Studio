#include "ContourOperation.h"

namespace e3::toolpath::operations {

Toolpath ContourOperation::compute(
    const core::Operation& params,
    const core::Tool& tool,
    std::function<void(float)> progressCb)
{
    Toolpath tp;
    tp.id = "tp_contour";
    tp.operationId = params.id;
    tp.toolId = params.toolId;

    // Güvenli retract
    const double safeZ = 10.0;
    Move rapid;
    rapid.type = Move::Type::Rapid;
    rapid.position = {0, 0, safeZ};
    rapid.toolAxis = {0, 0, 1};
    rapid.feedrate = 0;
    rapid.spindleSpeed = params.spindleSpeed;
    rapid.coolant = true;
    tp.moves.push_back(rapid);

    if (progressCb) progressCb(1.0f);
    return tp;
}

} // namespace e3::toolpath::operations
