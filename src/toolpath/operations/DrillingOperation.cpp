#include "DrillingOperation.h"
#include "OperationHelpers.h"
#include "../../core/Logger.h"
#include <cmath>

namespace e3::toolpath::operations {

static std::vector<geometry::Vec3> drillSites(
    const geometry::BoundingBox& bbox,
    const std::optional<TopoDS_Shape>& shape,
    double stepover)
{
    std::vector<geometry::Vec3> sites;
    if (shape) {
        auto& kernel = geometry::GeometryKernel::instance();
        auto slice = kernel.sliceAtZ(*shape, (bbox.min.z + bbox.max.z) * 0.5);
        for (const auto& contour : slice.contours) {
            if (contour.empty()) continue;
            double sx = 0, sy = 0;
            for (const auto& p : contour) { sx += p.x; sy += p.y; }
            sites.push_back({sx / contour.size(), sy / contour.size(), bbox.max.z});
        }
    }

    if (sites.empty()) {
        double spacing = std::max(stepover, 10.0);
        for (double x = bbox.min.x + spacing * 0.5; x <= bbox.max.x; x += spacing) {
            for (double y = bbox.min.y + spacing * 0.5; y <= bbox.max.y; y += spacing)
                sites.push_back({x, y, bbox.max.z});
        }
    }
    return sites;
}

Toolpath DrillingOperation::compute(
    const core::Operation& params,
    const core::Tool& tool,
    std::function<void(float)> progressCb)
{
    Toolpath result;
    result.id = params.id + "_drill_tp";
    result.operationId = params.id;
    result.toolId = params.toolId;

    auto shape = loadShapeForOperation(params);
    auto bbox = resolveBoundingBox(shape);
    double safeZ = bbox.max.z + 5.0;
    double finalZ = bbox.min.z + params.stockToLeave;
    auto sites = drillSites(bbox, shape, params.stepover);

    for (size_t i = 0; i < sites.size(); ++i) {
        if (progressCb) progressCb(static_cast<float>(i) / sites.size());
        const auto& site = sites[i];
        result.moves.push_back(makeRapidMove({site.x, site.y, safeZ}));
        result.moves.push_back(makePlungeMove({site.x, site.y, finalZ}, params.feedrateZ));
        result.moves.push_back(makeRapidMove({site.x, site.y, safeZ}));
    }

    if (progressCb) progressCb(1.0f);
    result.computeStats();
    return result;
}

} // namespace e3::toolpath::operations
