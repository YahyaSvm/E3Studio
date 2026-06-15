#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Toolpath :: Operations :: AdaptiveClearingOperation
// Trochoidal / Adaptive Clearing — En gelişmiş roughing stratejisi.
//
// Normal pocket'tan farkı:
//   - Takım her zaman sabit bir engagement angle'da ilerler
//   - Ani yük değişimi yok → takım ömrü 3-5x uzar
//   - Daha yüksek feedrate kullanılabilir
//   - Mastercam Dynamic Motion, Fusion Adaptive, HSMWorks benzeri
//
// Algoritma (Simplified Trochoidal):
//   1. Ana kesme yönü boyunca ilerleme çizgisi
//   2. Her adımda küçük daireler (trochoidal circles)
//   3. Önceki geçişlerin stock modeline göre adaptasyon
// ─────────────────────────────────────────────────────────────────────────────
#include "../ToolpathEngine.h"

namespace e3::toolpath::operations {

class AdaptiveClearingOperation : public IOperation {
public:
    Toolpath compute(
        const core::Operation& params,
        const core::Tool& tool,
        std::function<void(float)> progressCb) override;

private:
    struct TrochoidParams {
        double circleRadius;     // trochoidal daire yarıçapı
        double stepForward;      // her dairede ilerleme mesafesi
        double maxEngagement;    // maksimum açısal teması (derece)
        double feedrate;
    };

    // Tek bir trochoidal geçiş
    std::vector<Move> generateTrochoidalPass(
        const geometry::Vec3& start,
        const geometry::Vec3& end,
        const TrochoidParams& p);

    // Daire yay noktaları
    std::vector<geometry::Vec3> arcPoints(
        const geometry::Vec3& center,
        double radius,
        double startAngle,
        double endAngle,
        int segments = 24);
};

} // namespace e3::toolpath::operations
