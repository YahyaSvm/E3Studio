#include "Toolpath.h"
#include <algorithm>
#include <cmath>
#include <limits>

namespace e3::toolpath {

static double dist2d(const geometry::Vec3& a, const geometry::Vec3& b) {
    double dx = a.x - b.x;
    double dy = a.y - b.y;
    return std::sqrt(dx * dx + dy * dy);
}

void optimizeRapidOrder(Toolpath& tp) {
    if (tp.moves.size() < 3) return;

    std::vector<Move> optimized;
    optimized.reserve(tp.moves.size());

    size_t idx = 0;
    while (idx < tp.moves.size()) {
        optimized.push_back(tp.moves[idx]);
        if (tp.moves[idx].type != Move::Type::Rapid) {
            ++idx;
            continue;
        }

        size_t best = idx;
        geometry::Vec3 current = tp.moves[idx].position;
        for (size_t j = idx + 1; j < tp.moves.size(); ++j) {
            if (tp.moves[j].type != Move::Type::Rapid) break;
            if (dist2d(current, tp.moves[j].position) <
                dist2d(current, tp.moves[best].position))
                best = j;
        }

        if (best != idx)
            std::swap(tp.moves[idx], tp.moves[best]);

        ++idx;
    }

    tp.moves = std::move(optimized);
    tp.computeStats();
}

} // namespace e3::toolpath
