#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: AI :: CuttingParameterPredictor
// Kesme parametresi tahmin modeli.
// Şu an fizik tabanlı Taylor/Merchant modeli kullanıyor.
// İleride ONNX modeli ile genişletilebilir.
// ─────────────────────────────────────────────────────────────────────────────
#include <vector>
#include <string>
#include <optional>
#include <filesystem>

namespace e3::ai {

// ─── Giriş ───────────────────────────────────────────────────────────────
struct CuttingInput {
    float materialHardnessHRC;
    float toolDiameter;
    int   toolType;          // 0=FlatEnd, 1=Ball, 2=BullNose
    int   toolMaterial;      // 0=Carbide, 1=HSS, 2=Ceramic
    float axialDepth;
    float radialStepover;
    int   operationType;     // 0=Rough, 1=SemiFinish, 2=Finish
    float targetRoughness;   // Ra µm

    std::vector<float> toFeatureVector() const;
};

// ─── Çıkış ───────────────────────────────────────────────────────────────
struct CuttingPrediction {
    float feedrate;           // mm/min
    float spindleSpeed;       // rpm
    float predictedRoughness; // Ra µm
    float toolLifeMinutes;    // tahmini ömür
    float confidence;         // 0.0-1.0
};

// ─── Tahmin Motoru ────────────────────────────────────────────────────────
class CuttingParameterPredictor {
public:
    explicit CuttingParameterPredictor(const std::filesystem::path& modelPath);

    bool isLoaded() const { return m_loaded; }

    std::optional<CuttingPrediction> predict(const CuttingInput& input);

    // Model yoksa veya düşük güven varsa fizik tabanlı fallback
    CuttingPrediction physicsFallback(const CuttingInput& input) const;

private:
    bool m_loaded = false;
};

} // namespace e3::ai
