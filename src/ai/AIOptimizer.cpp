#include "CuttingParameterPredictor.h"
#include "../core/Logger.h"

namespace e3::ai {

struct OptimizationResult {
    double feedrate;
    double spindleSpeed;
    double roughness;
    double toolLifeMinutes;
    double confidence;
};

OptimizationResult optimizeCuttingParameters(const CuttingInput& input) {
    CuttingParameterPredictor predictor("models/cutting_params.onnx");
    auto result = predictor.predict(input);
    if (!result) {
        E3_LOG_WARN("AI optimizer fallback kullanıyor");
        return {1200.0, 8000.0, input.targetRoughness, 45.0, 0.5};
    }
    return {
        result->feedrate,
        result->spindleSpeed,
        result->predictedRoughness,
        result->toolLifeMinutes,
        result->confidence
    };
}

} // namespace e3::ai
