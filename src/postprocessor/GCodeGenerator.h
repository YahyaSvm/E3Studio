#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: PostProcessor :: GCodeGenerator
// Toolpath → G-Code
// Makineye özel formatlar için post-processor yapısı genişletilebilir.
// ─────────────────────────────────────────────────────────────────────────────
#include "../toolpath/Toolpath.h"
#include "../core/ProjectManager.h"
#include <string>
#include <vector>
#include <sstream>

namespace e3::postprocessor {

// ─── Post-Processor Konfigürasyonu ───────────────────────────────────────
struct PostConfig {
    std::string id;
    std::string name;           // "Fanuc", "Heidenhain", "Siemens840D"
    std::string lineEndChar;    // "\n" veya "\r\n"
    bool        useLineNumbers; // N10, N20...
    int         lineNumberStep; // 10
    bool        useToolChange;  // M06 T
    bool        useSubprograms;
    int         decimalPlaces;  // koordinat hassasiyeti
    double      safeZ;          // mm
    double      rapidFeed;      // mm/min (G0 beslemesi bazı makinelerde belirtilir)

    // G-Code diyalekti
    enum class Dialect { Fanuc, Heidenhain, Siemens840D, Haas, Generic } dialect;
};

// ─── G-Code Satırı ───────────────────────────────────────────────────────
struct GLine {
    int    lineNumber = -1; // -1 → numara yok
    std::string content;
    std::string comment;

    std::string toString(bool useNumbers, int decimals) const;
};

// ─── Generator ───────────────────────────────────────────────────────────
class GCodeGenerator {
public:
    explicit GCodeGenerator(PostConfig cfg) : m_cfg(std::move(cfg)) {}

    // Ana üretim fonksiyonu
    std::string generate(
        const toolpath::Toolpath& tp,
        const core::Tool& tool,
        const core::MachineConfig& machine);

    // Dosyaya yaz
    bool writeToFile(const std::string& content, const std::string& filePath);

private:
    PostConfig m_cfg;
    int m_lineNum = 10;
    int m_currentSpindle = -1;
    double m_currentFeed = -1;

    std::string nextLineNum();
    std::string formatCoord(double val) const;
    std::string formatMove(const toolpath::Move& m, const toolpath::Move* prev) const;

    // Program bölümleri
    std::string generateHeader(const core::Tool& tool, const core::MachineConfig& machine);
    std::string generateFooter();
    std::string generateToolChange(const core::Tool& tool);
    std::string generateSpindleOn(double rpm, bool clockwise = true);
    std::string generateCoolantOn();
    std::string generateCoolantOff();
};

// ─── Yerleşik Post-Processor'lar ─────────────────────────────────────────
namespace posts {
    PostConfig fanuc();
    PostConfig heidenhain();
    PostConfig siemens840d();
    PostConfig haas();
    PostConfig generic();
} // namespace posts

} // namespace e3::postprocessor
