#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Toolpath :: Toolpath (Veri Yapısı)
// Hesaplanmış bir takım yolunun tüm noktalarını tutar.
// Bu veri hem simülasyona hem G-Code üretimine hem UI'a gider.
// ─────────────────────────────────────────────────────────────────────────────
#include "../geometry/GeometryKernel.h"
#include <vector>
#include <string>

namespace e3::toolpath {

using Vec3 = geometry::Vec3;

// ─── Tek Bir Hareket ─────────────────────────────────────────────────────
struct Move {
    enum class Type {
        Rapid,          // G0  — hızlı pozisyonlama (kesme yok)
        Feed,           // G1  — doğrusal kesme
        ArcCW,          // G2  — saat yönünde yay
        ArcCCW,         // G3  — saat karşı yay
        PlungeFeed,     // Z iniş kesmesi
        Retract,        // güvenli yüksekliğe çıkış
    };

    Type        type;
    Vec3        position;       // XYZ hedef
    Vec3        toolAxis;       // 5 eksen için alet ekseni vektörü (normal: 0,0,1)
    Vec3        arcCenter;      // G2/G3 için merkez (I, J, K)
    double      feedrate;       // mm/min (Rapid için görmezden gelinir)
    double      spindleSpeed;   // rpm (değişmişse)
    bool        coolant;        // soğutma aktif mi
};

// ─── Toolpath ────────────────────────────────────────────────────────────
struct Toolpath {
    std::string id;
    std::string operationId;
    std::string toolId;

    std::vector<Move> moves;

    // İstatistikler (hesaplanmış)
    double cuttingLength    = 0; // mm — kesici hareket toplamı
    double rapidLength      = 0; // mm — rapid hareket toplamı
    double estimatedTime    = 0; // dakika
    double minZ             = 0;
    double maxZ             = 0;

    bool   isEmpty() const { return moves.empty(); }
    size_t moveCount() const { return moves.size(); }

    void computeStats();
};

} // namespace e3::toolpath
