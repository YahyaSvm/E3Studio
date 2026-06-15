#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Geometry :: GeometryKernel
// OpenCASCADE üzerine sarmalayıcı — CAM'in geometri altyapısı.
// B-Rep (Boundary Representation) yüzeyler ve mesh verisi bu katmanda işlenir.
// ─────────────────────────────────────────────────────────────────────────────
#include <TopoDS_Shape.hxx>
#include <TopoDS_Face.hxx>
#include <TopoDS_Edge.hxx>
#include <BRep_Builder.hxx>
#include <gp_Pnt.hxx>
#include <gp_Vec.hxx>
#include <gp_Dir.hxx>
#include <gp_Pln.hxx>
#include <Bnd_Box.hxx>
#include <BRepBndLib.hxx>
#include <vector>
#include <string>
#include <optional>
#include <array>

namespace e3::geometry {

// ─── Temel 3D Tipleri ────────────────────────────────────────────────────
struct Vec3 {
    double x, y, z;
    Vec3 operator+(const Vec3& o) const { return {x+o.x, y+o.y, z+o.z}; }
    Vec3 operator-(const Vec3& o) const { return {x-o.x, y-o.y, z-o.z}; }
    Vec3 operator*(double s) const { return {x*s, y*s, z*s}; }
    double dot(const Vec3& o) const { return x*o.x + y*o.y + z*o.z; }
    Vec3 cross(const Vec3& o) const {
        return {y*o.z - z*o.y, z*o.x - x*o.z, x*o.y - y*o.x};
    }
    double length() const;
    Vec3 normalized() const;

    gp_Pnt toOCC() const { return gp_Pnt(x, y, z); }
    static Vec3 fromOCC(const gp_Pnt& p) { return {p.X(), p.Y(), p.Z()}; }
};

struct BoundingBox {
    Vec3 min;
    Vec3 max;
    Vec3 center() const { return (min + max) * 0.5; }
    Vec3 size() const { return max - min; }
};

// ─── Triangle Mesh ───────────────────────────────────────────────────────
struct Mesh {
    std::vector<Vec3>                vertices;
    std::vector<Vec3>                normals;
    std::vector<std::array<int,3>>   triangles;
    BoundingBox                      bbox;

    bool isEmpty() const { return vertices.empty(); }
    void computeBBox();
    void computeNormals();

    // Three.js'e gönderilecek buffer formatı
    std::vector<float> toInterleavedBuffer() const; // [x,y,z, nx,ny,nz, ...]
};

// ─── Z-Level Kesit ───────────────────────────────────────────────────────
struct ZSlice {
    double z;
    std::vector<std::vector<Vec3>> contours; // birden fazla kapalı kontur olabilir
};

// ─── GeometryKernel ──────────────────────────────────────────────────────
class GeometryKernel {
public:
    static GeometryKernel& instance() {
        static GeometryKernel k;
        return k;
    }

    // Dosya yükleme
    std::optional<TopoDS_Shape> loadSTEP(const std::string& path);
    std::optional<TopoDS_Shape> loadIGES(const std::string& path);
    std::optional<Mesh>         loadSTL(const std::string& path);

    // B-Rep → Mesh (görselleştirme için)
    Mesh tessellate(const TopoDS_Shape& shape, double linearDeflection = 0.1);

    // Z-Level dilimleme — toolpath için kesit alma
    std::vector<ZSlice> sliceByZ(
        const TopoDS_Shape& shape,
        double zStart,
        double zEnd,
        double zStep);

    // Tek Z seviyesinde kesit
    ZSlice sliceAtZ(const TopoDS_Shape& shape, double z);

    // Kontur offsetleme — takım yarıçapı kadar içeri çek
    std::vector<Vec3> offsetContour(
        const std::vector<Vec3>& contour,
        double offset,
        bool inward = true);

    // Bounding box
    BoundingBox getBoundingBox(const TopoDS_Shape& shape);

    // Yüzey normali hesapla (5 eksen için)
    Vec3 getSurfaceNormal(const TopoDS_Face& face, double u, double v);

    // Yüzey üzerinde nokta örnekleme (scallop hesabı için)
    std::vector<Vec3> sampleSurface(
        const TopoDS_Face& face,
        double stepU,
        double stepV);

private:
    GeometryKernel() = default;
};

} // namespace e3::geometry
