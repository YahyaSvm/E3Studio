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
#include <Geom_Surface.hxx>
#include <Geom_TrimmedCurve.hxx>
#include <GC_MakeSegment.hxx>
#include <BRepTools.hxx>
#include <BRepBuilderAPI_MakeEdge.hxx>
#include <BRepBuilderAPI_MakeWire.hxx>
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

void Mesh::computeNormals() {
    normals.assign(vertices.size(), Vec3{0, 0, 0});
    for (const auto& tri : triangles) {
        if (tri[0] >= vertices.size() || tri[1] >= vertices.size() || tri[2] >= vertices.size())
            continue;
        const auto& v0 = vertices[tri[0]];
        const auto& v1 = vertices[tri[1]];
        const auto& v2 = vertices[tri[2]];

        Vec3 edge1 = v1 - v0;
        Vec3 edge2 = v2 - v0;
        Vec3 faceNormal = edge1.cross(edge2);

        normals[tri[0]] = normals[tri[0]] + faceNormal;
        normals[tri[1]] = normals[tri[1]] + faceNormal;
        normals[tri[2]] = normals[tri[2]] + faceNormal;
    }

    for (auto& n : normals) {
        n = n.normalized();
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

std::optional<Mesh> GeometryKernel::loadSTL(const std::string& path) {
    (void)path;
    E3_LOG_WARN("STL yukleme henuz implemente edilmedi");
    return std::nullopt;
}

ZSlice GeometryKernel::sliceAtZ(const TopoDS_Shape& shape, double z) {
    ZSlice slice;
    slice.z = z;

    gp_Pln plane(gp_Pnt(0, 0, z), gp_Dir(0, 0, 1));
    TopoDS_Face face = BRepBuilderAPI_MakeFace(plane, -1e6, 1e6, -1e6, 1e6);
    BRepAlgoAPI_Section section(shape, face);
    section.Approximation(true);
    section.Build();

    if (!section.IsDone()) return slice;

    TopExp_Explorer exp(section.Shape(), TopAbs_EDGE);
    for (; exp.More(); exp.Next()) {
        TopoDS_Edge edge = TopoDS::Edge(exp.Current());
        double first, last;
        Handle(Geom_Curve) curve = BRep_Tool::Curve(edge, first, last);
        if (curve.IsNull()) continue;

        std::vector<Vec3> contour;
        const int numSamples = 50;
        for (int i = 0; i <= numSamples; ++i) {
            double t = first + (last - first) * i / numSamples;
            gp_Pnt p = curve->Value(t);
            contour.push_back({p.X(), p.Y(), p.Z()});
        }
        slice.contours.push_back(contour);
    }

    return slice;
}

std::vector<ZSlice> GeometryKernel::sliceByZ(
    const TopoDS_Shape& shape,
    double zStart,
    double zEnd,
    double zStep)
{
    std::vector<ZSlice> slices;
    if (zStep <= 0 || zStart > zEnd) return slices;

    for (double z = zStart; z <= zEnd; z += zStep) {
        slices.push_back(sliceAtZ(shape, z));
    }
    return slices;
}

std::vector<Vec3> GeometryKernel::offsetContour(
    const std::vector<Vec3>& contour,
    double offset,
    bool inward)
{
    if (contour.size() < 3) return contour;

    // Convert contour to OCCT wire
    BRepBuilderAPI_MakeWire wireMaker;
    for (size_t i = 0; i < contour.size(); ++i) {
        const auto& p1 = contour[i];
        const auto& p2 = contour[(i + 1) % contour.size()];
        gp_Pnt p1_occ(p1.x, p1.y, p1.z);
        gp_Pnt p2_occ(p2.x, p2.y, p2.z);
        Handle(Geom_TrimmedCurve) seg = GC_MakeSegment(p1_occ, p2_occ);
        TopoDS_Edge edge = BRepBuilderAPI_MakeEdge(seg);
        wireMaker.Add(edge);
    }

    if (!wireMaker.IsDone()) return contour;

    TopoDS_Wire wire = wireMaker.Wire();
    double offsetVal = inward ? -std::abs(offset) : std::abs(offset);

    BRepOffsetAPI_MakeOffset offsetMaker;
    offsetMaker.AddWire(wire);
    offsetMaker.Perform(offsetVal);

    if (!offsetMaker.IsDone()) return contour;

    // Extract the offset result
    TopoDS_Shape offsetShape = offsetMaker.Shape();
    std::vector<Vec3> result;
    TopExp_Explorer exp(offsetShape, TopAbs_VERTEX);
    for (; exp.More(); exp.Next()) {
        TopoDS_Vertex v = TopoDS::Vertex(exp.Current());
        gp_Pnt p = BRep_Tool::Pnt(v);
        result.push_back({p.X(), p.Y(), p.Z()});
    }

    return result;
}

std::vector<Vec3> GeometryKernel::sampleSurface(
    const TopoDS_Face& face,
    double stepU,
    double stepV)
{
    std::vector<Vec3> samples;
    Handle(Geom_Surface) surf = BRep_Tool::Surface(face);
    double uMin, uMax, vMin, vMax;
    BRepTools::UVBounds(face, uMin, uMax, vMin, vMax);

    for (double u = uMin; u <= uMax; u += stepU) {
        for (double v = vMin; v <= vMax; v += stepV) {
            gp_Pnt p = surf->Value(u, v);
            samples.push_back({p.X(), p.Y(), p.Z()});
        }
    }
    return samples;
}

} // namespace e3::geometry
