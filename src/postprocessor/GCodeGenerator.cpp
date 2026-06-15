#include "GCodeGenerator.h"
#include "../core/Logger.h"
#include <fstream>
#include <iomanip>
#include <sstream>
#include <cmath>

namespace e3::postprocessor {

std::string GCodeGenerator::generate(
    const toolpath::Toolpath& tp,
    const core::Tool& tool,
    const core::MachineConfig& machine)
{
    m_lineNum = 10;
    m_currentSpindle = -1;
    m_currentFeed = -1;

    std::ostringstream out;
    out << generateHeader(tool, machine);
    out << generateToolChange(tool);

    // İlk hareketin spindle hızını kullan; yoksa makine maksimumunun yarısı
    double initialSpindle = 0.0;
    for (const auto& m : tp.moves) {
        if (m.spindleSpeed > 0.0) { initialSpindle = m.spindleSpeed; break; }
    }
    if (initialSpindle <= 0.0)
        initialSpindle = machine.maxSpindle > 0.0 ? machine.maxSpindle * 0.5 : 8000.0;

    out << generateSpindleOn(initialSpindle);
    out << generateCoolantOn();

    const toolpath::Move* prev = nullptr;
    for (const auto& move : tp.moves) {
        out << formatMove(move, prev) << m_cfg.lineEndChar;
        if (move.feedrate > 0.0) m_currentFeed = move.feedrate; // feed takibini güncelle
        prev = &move;
    }

    out << generateCoolantOff();
    out << generateFooter();
    return out.str();
}

std::string GCodeGenerator::generateHeader(
    const core::Tool& tool,
    const core::MachineConfig& machine)
{
    std::ostringstream h;
    h << "%" << m_cfg.lineEndChar;
    h << "O0001 (E3STUDIO - " << tool.name << ")" << m_cfg.lineEndChar;
    h << "(" << machine.name << ")" << m_cfg.lineEndChar;
    h << "G17 G40 G49 G80 G90" << m_cfg.lineEndChar;
    h << "G21" << m_cfg.lineEndChar; // Metrik
    return h.str();
}

std::string GCodeGenerator::generateToolChange(const core::Tool& tool) {
    std::ostringstream t;
    if (m_cfg.useToolChange) {
        t << "T1 M06" << m_cfg.lineEndChar;
        t << "(D=" << formatCoord(tool.diameter)
          << " CR=" << formatCoord(tool.cornerRadius)
          << " - " << tool.name << ")" << m_cfg.lineEndChar;
    }
    return t.str();
}

std::string GCodeGenerator::generateSpindleOn(double rpm, bool clockwise) {
    std::ostringstream s;
    s << "S" << static_cast<int>(rpm)
      << (clockwise ? " M03" : " M04") << m_cfg.lineEndChar;
    m_currentSpindle = static_cast<int>(rpm);
    return s.str();
}

std::string GCodeGenerator::generateCoolantOn() {
    return "M08" + m_cfg.lineEndChar;
}

std::string GCodeGenerator::generateCoolantOff() {
    return "M09" + m_cfg.lineEndChar;
}

std::string GCodeGenerator::generateFooter() {
    std::ostringstream f;
    f << "G91 G28 Z0." << m_cfg.lineEndChar;
    f << "G91 G28 X0. Y0." << m_cfg.lineEndChar;
    f << "M30" << m_cfg.lineEndChar;
    f << "%" << m_cfg.lineEndChar;
    return f.str();
}

std::string GCodeGenerator::formatCoord(double val) const {
    std::ostringstream ss;
    ss << std::fixed << std::setprecision(m_cfg.decimalPlaces) << val;
    return ss.str();
}

std::string GCodeGenerator::formatMove(
    const toolpath::Move& m,
    const toolpath::Move* prev) const
{
    std::ostringstream line;

    // Feed değişimi
    bool feedChanged = (std::abs(m.feedrate - m_currentFeed) > 0.1);

    switch (m.type) {
        case toolpath::Move::Type::Rapid:
            line << "G0 X" << formatCoord(m.position.x)
                 << " Y" << formatCoord(m.position.y)
                 << " Z" << formatCoord(m.position.z);
            break;

        case toolpath::Move::Type::Feed:
        case toolpath::Move::Type::PlungeFeed:
            line << "G1 X" << formatCoord(m.position.x)
                 << " Y" << formatCoord(m.position.y)
                 << " Z" << formatCoord(m.position.z);
            if (feedChanged)
                line << " F" << static_cast<int>(m.feedrate);
            break;

        case toolpath::Move::Type::ArcCW:
            line << "G2 X" << formatCoord(m.position.x)
                 << " Y" << formatCoord(m.position.y)
                 << " Z" << formatCoord(m.position.z)
                 << " I" << formatCoord(m.arcCenter.x)
                 << " J" << formatCoord(m.arcCenter.y)
                 << " K" << formatCoord(m.arcCenter.z);
            if (feedChanged)
                line << " F" << static_cast<int>(m.feedrate);
            break;

        case toolpath::Move::Type::ArcCCW:
            line << "G3 X" << formatCoord(m.position.x)
                 << " Y" << formatCoord(m.position.y)
                 << " Z" << formatCoord(m.position.z)
                 << " I" << formatCoord(m.arcCenter.x)
                 << " J" << formatCoord(m.arcCenter.y)
                 << " K" << formatCoord(m.arcCenter.z);
            if (feedChanged)
                line << " F" << static_cast<int>(m.feedrate);
            break;

        case toolpath::Move::Type::Retract:
            line << "G0 Z" << formatCoord(m.position.z);
            break;
    }

    return line.str();
}

bool GCodeGenerator::writeToFile(const std::string& content, const std::string& filePath) {
    std::ofstream f(filePath);
    if (!f.is_open()) {
        E3_LOG_ERROR("G-Code dosyası yazılamadı: {}", filePath);
        return false;
    }
    f << content;
    E3_LOG_INFO("G-Code yazıldı: {} ({} karakter)", filePath, content.size());
    return true;
}

// ─── Hazır Post-Processor'lar ─────────────────────────────────────────────
namespace posts {

PostConfig fanuc() {
    return {"fanuc", "Fanuc", "\n", true, 10, true, false, 4,
            50.0, 10000.0, PostConfig::Dialect::Fanuc};
}

PostConfig heidenhain() {
    return {"heidenhain", "Heidenhain iTNC", "\n", true, 5, true, false, 4,
            50.0, 0.0, PostConfig::Dialect::Heidenhain};
}

PostConfig haas() {
    return {"haas", "Haas", "\n", true, 10, true, false, 4,
            50.0, 0.0, PostConfig::Dialect::Haas};
}

PostConfig generic() {
    return {"generic", "Generic ISO", "\n", false, 10, false, false, 3,
            50.0, 0.0, PostConfig::Dialect::Generic};
}

} // namespace posts
} // namespace e3::postprocessor
