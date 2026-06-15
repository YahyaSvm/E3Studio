#include "GeometryKernel.h"
#include "../core/Logger.h"

// OCCT başlıkları
#include <STEPControl_Reader.hxx>
#include <IGESControl_Reader.hxx>
#include <BRepMesh_IncrementalMesh.hxx>
#include <BRep_Tool.hxx>
#include <TopExp_Explorer.hxx>
#include <TopoDS.hxx>
#include <Poly_Triangulation.hxx>
#include <GeomAPI_ProjectPointOnSurf.hxx>
#include <BRepBuilderAPI_MakeFace.hxx>
#include <BRepAlgoAPI_Section.hxx>
#include <ShapeAnalysis_FreeBounds.hxx>
#include <BRepOffsetAPI_MakeOffset.hxx>
#include <BRepBndLib.hxx>
#include <GeomLProp_SLProps.hxx>
#include <cmath>

namespace e3::geometry {

// ─── Vec3 ─────────────────────────────────────────────────────────────────
double Vec3::length() const {
    return std::sqrt(x*x + y*y + z*z);
}
Vec3 Vec3::normalized() const {
    double len = length();
    if (len < 1e-12) return {0,0,1};
    return {x/len, y/len, z/len};
}

// ─── Mesh ─────────────────────────────────────────────────────────────────
void Mesh::computeBBox() {
    if (vertices.empty()) return;
    bbox.min = bbox.max = vertices[0];
    for (const auto& v : vertices) {
        bbox.min.x = std::min(bbox.min.x, v.x);
        bbox.min.y = std::min(bbox.min.y, v.y);
        bbox.min.z = std::min(bbox.min.z, v.z);
        bbox.max.x = std::max(bbox.max.x, v.x);
        bbox.max.y = std::max(bbox.max.y, v.y);
        bbox.max.z = std::max(bbox.max.z, v.z);
    }
}

std::vector<float> Mesh::toInterleavedBuffer() const {
    std::vector<float> buf;
    buf.reserve(triangles.size() * 3 * 6); // 3 vertex x (xyz + normal)
    for (const auto& tri : triangles) {
        for (int i = 0; i < 3; ++i) {
            const auto& v = vertices[tri[i]];
            const auto& n = normals.size() > static_cast<size_t>(tri[i])
                ? normals[tri[i]] : Vec3{0,0,1};
            buf.push_back(static_cast<float>(v.x));
            buf.push_back(static_cast<float>(v.y));
            buf.push_back(static_cast<float>(v.z));
            buf.push_back(static_cast<float>(n.x));
            buf.push_back(static_cast<float>(n.y));
            buf.push_back(static_cast<float>(n.z));
        }
    }
    return buf;
}

// ─── GeometryKernel ───────────────────────────────────────────────────────

std::optional<TopoDS_Shape> GeometryKernel::loadSTEP(const std::string& path) {
    STEPControl_Reader reader;
    IFSelect_ReturnStatus status = reader.ReadFile(path.c_str());
    if (status != IFSelect_RetDone) {
        E3_LOG_ERROR("STEP dosyası okunamadı: {}", path);
        return std::nullopt;
    }
    reader.TransferRoots();
    TopoDS_Shape shape = reader.OneShape();
    E3_LOG_INFO("STEP yüklendi: {}", path);
    return shape;
}

std::optional<TopoDS_Shape> GeometryKernel::loadIGES(const std::string& path) {
    IGESControl_Reader reader;
    IFSelect_ReturnStatus status = reader.ReadFile(path.c_str());
    if (status != IFSelect_RetDone) {
        E3_LOG_ERROR("IGES dosyası okunamadı: {}", path);
        return std::nullopt;
    }
    reader.TransferRoots();
    TopoDS_Shape shape = reader.OneShape();
    E3_LOG_INFO("IGES yüklendi: {}", path);
    return shape;
}

Mesh GeometryKernel::tessellate(const TopoDS_Shape& shape, double linearDeflection) {
    // OCCT mesh hesaplama
    BRepMesh_IncrementalMesh mesher(shape, linearDeflection, false, 0.5);
    mesher.Perform();

    Mesh result;
    TopExp_Explorer faceExp(shape, TopAbs_FACE);

    for (; faceExp.More(); faceExp.Next()) {
        TopoDS_Face face = TopoDS::Face(faceExp.Current());
        TopLoc_Location loc;
        Handle(Poly_Triangulation) tri = BRep_Tool::Triangulation(face, loc);
        if (tri.IsNull()) continue;

        int baseIdx = static_cast<int>(result.vertices.size());
        gp_Trsf trsf = loc.IsIdentity() ? gp_Trsf() : loc.Transformation();

        // Vertex'ler
        for (int i = 1; i <= tri->NbNodes(); ++i) {
            gp_Pnt p = tri->Node(i).Transformed(trsf);
            result.vertices.push_back(Vec3::fromOCC(p));
        }

        // Normal'ler
        bool hasNormals = tri->HasNormals();
        for (int i = 1; i <= tri->NbNodes(); ++i) {
            if (hasNormals) {
                gp_Dir n = tri->Normal(i);
                result.normals.push_back({n.X(), n.Y(), n.Z()});
            } else {
                result.normals.push_back({0, 0, 1});
            }
        }

        // Üçgenler
        bool reversed = (face.Orientation() == TopAbs_REVERSED);
        for (int i = 1; i <= tri->NbTriangles(); ++i) {
            int n1, n2, n3;
            tri->Triangle(i).Get(n1, n2, n3);
            if (reversed) std::swap(n2, n3);
            result.triangles.push_back({baseIdx + n1 - 1, baseIdx + n2 - 1, baseIdx + n3 - 1});
        }
    }

    result.computeBBox();
    E3_LOG_DEBUG("Tessellate: {} vertex, {} üçgen",
        result.vertices.size(), result.triangles.size());
    return result;
}

BoundingBox GeometryKernel::getBoundingBox(const TopoDS_Shape& shape) {
    Bnd_Box box;
    BRepBndLib::Add(shape, box);
    double xmin, ymin, zmin, xmax, ymax, zmax;
    box.Get(xmin, ymin, zmin, xmax, ymax, zmax);
    return {{xmin, ymin, zmin}, {xmax, ymax, zmax}};
}

Vec3 GeometryKernel::getSurfaceNormal(const TopoDS_Face& face, double u, double v) {
    Handle(Geom_Surface) surf = BRep_Tool::Surface(face);
    GeomLProp_SLProps props(surf, u, v, 1, 1e-6);
    if (props.IsNormalDefined()) {
        gp_Dir n = props.Normal();
        if (face.Orientation() == TopAbs_REVERSED)
            n.Reverse();
        return {n.X(), n.Y(), n.Z()};
    }
    return {0, 0, 1};
}

} // namespace e3::geometry
