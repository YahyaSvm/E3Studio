#pragma once
#include "../ToolpathEngine.h"

namespace e3::toolpath::operations {

class DrillingOperation : public IOperation {
public:
    Toolpath compute(
        const core::Operation& params,
        const core::Tool& tool,
        std::function<void(float)> progressCb) override;
};

} // namespace e3::toolpath::operations
