#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Toolpath :: Operations :: PocketOperation
// 2.5D Cep Frezeleme Algoritması
//
// Algoritma:
//   1. Her Z seviyesinde şeklin kesitini al
//   2. Konturları takım yarıçapı kadar offsetle
//   3. Morfolojik erode ile iç dolgu konturlarını üret (concentric veya zigzag)
//   4. Güvenli yükseklik → plunge → kesme → retract sıralaması
// ─────────────────────────────────────────────────────────────────────────────
#include "../ToolpathEngine.h"
#include "../../geometry/GeometryKernel.h"

namespace e3::toolpath::operations {

class PocketOperation : public IOperation {
public:
    Toolpath compute(
        const core::Operation& params,
        const core::Tool& tool,
        std::function<void(float)> progressCb) override;

private:
    enum class FillStrategy { Concentric, ZigZag, Spiral };

    // Bir Z kesitindeki konturları işle
    std::vector<Move> processZLevel(
        const std::vector<std::vector<geometry::Vec3>>& contours,
        double z,
        double toolRadius,
        double stepover,
        double feedrate,
        FillStrategy strategy);

    // Zigzag dolgu
    std::vector<geometry::Vec3> generateZigzag(
        const std::vector<geometry::Vec3>& boundary,
        double stepover,
        double angle = 0.0);

    // Konsantrik ofset dolgu
    std::vector<std::vector<geometry::Vec3>> generateConcentric(
        const std::vector<geometry::Vec3>& boundary,
        double stepover);

    // İki nokta arası kesme move oluştur
    Move makeFeedMove(const geometry::Vec3& to, double feedrate);
    Move makeRapidMove(const geometry::Vec3& to);
};

} // namespace e3::toolpath::operations
