#include "GeometryKernel.h"
#include <TopoDS_Shape.hxx>
#include <BRepAlgoAPI_Fuse.hxx>

namespace e3::geometry {

std::optional<TopoDS_Shape> fuseShapes(
    const TopoDS_Shape& a,
    const TopoDS_Shape& b)
{
    try {
        BRepAlgoAPI_Fuse fuse(a, b);
        if (!fuse.IsDone()) return std::nullopt;
        return fuse.Shape();
    } catch (...) {
        return std::nullopt;
    }
}

BoundingBox combinedBoundingBox(
    const TopoDS_Shape& a,
    const TopoDS_Shape& b)
{
    auto& kernel = GeometryKernel::instance();
    auto boxA = kernel.getBoundingBox(a);
    auto boxB = kernel.getBoundingBox(b);
    return {
        {
            std::min(boxA.min.x, boxB.min.x),
            std::min(boxA.min.y, boxB.min.y),
            std::min(boxA.min.z, boxB.min.z),
        },
        {
            std::max(boxA.max.x, boxB.max.x),
            std::max(boxA.max.y, boxB.max.y),
            std::max(boxA.max.z, boxB.max.z),
        }
    };
}

} // namespace e3::geometry
