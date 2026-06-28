#include "GeometryKernel.h"
#include <cmath>

namespace e3::geometry {

geometry::Vec3 transformPoint(const geometry::Vec3& p, const std::array<double, 16>& matrix) {
    return {
        matrix[0] * p.x + matrix[1] * p.y + matrix[2] * p.z + matrix[3],
        matrix[4] * p.x + matrix[5] * p.y + matrix[6] * p.z + matrix[7],
        matrix[8] * p.x + matrix[9] * p.y + matrix[10] * p.z + matrix[11],
    };
}

double measureDistance(const geometry::Vec3& a, const geometry::Vec3& b) {
    return (a - b).length();
}

BoundingBox measureBoundingBox(const std::vector<Vec3>& points) {
    BoundingBox box{{0, 0, 0}, {0, 0, 0}};
    if (points.empty()) return box;
    box.min = box.max = points.front();
    for (const auto& p : points) {
        box.min.x = std::min(box.min.x, p.x);
        box.min.y = std::min(box.min.y, p.y);
        box.min.z = std::min(box.min.z, p.z);
        box.max.x = std::max(box.max.x, p.x);
        box.max.y = std::max(box.max.y, p.y);
        box.max.z = std::max(box.max.z, p.z);
    }
    return box;
}

} // namespace e3::geometry
