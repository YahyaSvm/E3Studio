#include "Toolpath.h"
#include "../geometry/GeometryKernel.h"
#include <algorithm>

namespace e3::toolpath {

std::vector<geometry::Vec3> offsetContour(
    const std::vector<geometry::Vec3>& contour,
    double offset,
    bool inward)
{
    if (contour.size() < 3 || std::abs(offset) < 1e-9)
        return contour;

    return geometry::GeometryKernel::instance().offsetContour(contour, offset, inward);
}

} // namespace e3::toolpath
