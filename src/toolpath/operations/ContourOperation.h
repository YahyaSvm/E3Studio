#pragma once
// E3Studio :: Toolpath :: Operations :: ContourOperation
// 2.5D Kontur (profil) frezeleme
#include "../ToolpathEngine.h"
#include "../../geometry/GeometryKernel.h"

namespace e3::toolpath::operations {

class ContourOperation : public IOperation {
public:
    Toolpath compute(
        const core::Operation& params,
        const core::Tool& tool,
        std::function<void(float)> progressCb) override;
};

} // namespace e3::toolpath::operations
