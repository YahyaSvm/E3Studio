#pragma once
// E3Studio :: Toolpath :: Operations :: SurfaceFinishingOperation
// 3D yüzey finish - iso-scallop / paralel hat / scallop
#include "../ToolpathEngine.h"
#include "../../geometry/GeometryKernel.h"

namespace e3::toolpath::operations {

class SurfaceFinishingOperation : public IOperation {
public:
    Toolpath compute(
        const core::Operation& params,
        const core::Tool& tool,
        std::function<void(float)> progressCb) override;
};

} // namespace e3::toolpath::operations
