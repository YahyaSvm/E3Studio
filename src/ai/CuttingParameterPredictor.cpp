#include "CuttingParameterPredictor.h"
#include "../core/Logger.h"
#include <cmath>

namespace e3::ai {

// ─── Feature Vector ───────────────────────────────────────────────────────
std::vector<float> CuttingInput::toFeatureVector() const {
    std::vector<float> toolTypeOH(3, 0.0f);
    if (toolType >= 0 && toolType < 3) toolTypeOH[toolType] = 1.0f;

    std::vector<float> toolMatOH(3, 0.0f);
    if (toolMaterial >= 0 && toolMaterial < 3) toolMatOH[toolMaterial] = 1.0f;

    std::vector<float> opTypeOH(3, 0.0f);
    if (operationType >= 0 && operationType < 3) opTypeOH[operationType] = 1.0f;

    return {
        materialHardnessHRC / 70.0f,
        toolDiameter / 50.0f,
        toolTypeOH[0], toolTypeOH[1], toolTypeOH[2],
        toolMatOH[0],  toolMatOH[1],  toolMatOH[2],
        axialDepth / 20.0f,
        (toolDiameter > 0.0f ? radialStepover / toolDiameter : 0.0f),
        opTypeOH[0], opTypeOH[1], opTypeOH[2],
        targetRoughness / 10.0f
    };
}

// ─── Constructor ──────────────────────────────────────────────────────────
CuttingParameterPredictor::CuttingParameterPredictor(
    const std::filesystem::path& modelPath)
{
    // ONNX desteği ileriki sürümde eklenecek — fizik fallback aktif
    (void)modelPath;
    m_loaded = false;
    E3_LOG_INFO("CuttingParameterPredictor: fizik tabanlı mod aktif");
}

// ─── Tahmin ───────────────────────────────────────────────────────────────
std::optional<CuttingPrediction> CuttingParameterPredictor::predict(
    const CuttingInput& input)
{
    return physicsFallback(input);
}

// ─── Fizik Tabanlı Fallback ───────────────────────────────────────────────
CuttingPrediction CuttingParameterPredictor::physicsFallback(
    const CuttingInput& input) const
{
    CuttingPrediction pred;

    float hardnessFactor = 1.0f - (input.materialHardnessHRC / 70.0f) * 0.7f;

    float toolFactor = 1.0f;
    if (input.toolMaterial == 0) toolFactor = 1.5f;  // Carbide
    else if (input.toolMaterial == 2) toolFactor = 2.0f; // Ceramic

    float opFactor = 1.0f;
    if (input.operationType == 0) opFactor = 0.7f;   // Roughing
    if (input.operationType == 2) opFactor = 1.3f;   // Finishing

    float Vc = 100.0f * hardnessFactor * toolFactor * opFactor;

    const float pi = 3.14159265f;
    float diam = input.toolDiameter > 0.0f ? input.toolDiameter : 1.0f;
    pred.spindleSpeed       = (Vc * 1000.0f) / (pi * diam);
    float fz                = diam * 0.005f * opFactor;
    pred.feedrate           = fz * 4.0f * pred.spindleSpeed;
    pred.predictedRoughness = input.targetRoughness > 0.0f
                              ? input.targetRoughness * 1.2f : 1.6f;
    pred.toolLifeMinutes    = 30.0f * hardnessFactor * toolFactor;
    pred.confidence         = 0.5f;

    return pred;
}

} // namespace e3::ai
