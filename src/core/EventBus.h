#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Core :: EventBus
// Yazılımın tüm katmanları arası type-safe, thread-safe mesajlaşma sistemi.
// Herhangi bir katman Event yayınlar, ilgilenen katmanlar subscribe olur.
// Doğrudan bağımlılık olmaz — tamamen gevşek bağlı mimari.
// ─────────────────────────────────────────────────────────────────────────────
#include <functional>
#include <unordered_map>
#include <vector>
#include <mutex>
#include <typeindex>
#include <memory>
#include <any>

namespace e3::core {

// Her event bir struct olarak tanımlanır
struct Event {
    virtual ~Event() = default;
};

// Subscription handle — unsubscribe etmek için tutulur
using SubscriptionID = uint64_t;

class EventBus {
public:
    static EventBus& instance() {
        static EventBus bus;
        return bus;
    }

    // Event tipine göre listener kaydet
    template<typename TEvent>
    SubscriptionID subscribe(std::function<void(const TEvent&)> handler) {
        std::lock_guard lock(m_mutex);
        auto typeIdx = std::type_index(typeid(TEvent));
        SubscriptionID id = m_nextId++;

        m_listeners[typeIdx].push_back({
            id,
            [handler](const std::any& e) {
                handler(std::any_cast<const TEvent&>(e));
            }
        });

        return id;
    }

    // Event yayınla — tüm listener'lar çağrılır
    template<typename TEvent>
    void publish(const TEvent& event) {
        std::lock_guard lock(m_mutex);
        auto typeIdx = std::type_index(typeid(TEvent));
        auto it = m_listeners.find(typeIdx);
        if (it == m_listeners.end()) return;

        for (auto& entry : it->second) {
            entry.callback(event);
        }
    }

    // Belirli subscription'ı iptal et
    void unsubscribe(SubscriptionID id) {
        std::lock_guard lock(m_mutex);
        for (auto& [type, listeners] : m_listeners) {
            auto it = std::remove_if(listeners.begin(), listeners.end(),
                [id](const ListenerEntry& e) { return e.id == id; });
            listeners.erase(it, listeners.end());
        }
    }

private:
    EventBus() = default;

    struct ListenerEntry {
        SubscriptionID id;
        std::function<void(const std::any&)> callback;
    };

    std::unordered_map<std::type_index, std::vector<ListenerEntry>> m_listeners;
    std::mutex m_mutex;
    SubscriptionID m_nextId = 1;
};

// ─── Uygulama genelinde kullanılan Event tipleri ───────────────────────────

struct ProjectOpenedEvent : Event {
    std::string filePath;
};

struct ToolpathGeneratedEvent : Event {
    std::string operationId;
    std::string toolpathId;
    size_t pointCount;
    double estimatedTime; // dakika

    ToolpathGeneratedEvent(std::string opId, std::string tpId, size_t ptCount, double estTime)
        : operationId(std::move(opId)), toolpathId(std::move(tpId)),
          pointCount(ptCount), estimatedTime(estTime) {}
};

struct SimulationStepEvent : Event {
    float progress; // 0.0 - 1.0
    float remainingMaterial; // yüzde
};

struct AIOptimizationCompleteEvent : Event {
    std::string operationId;
    double suggestedFeedrate;
    double suggestedSpindle;
    double predictedRoughness; // Ra değeri
};

struct ErrorEvent : Event {
    std::string source;
    std::string message;
    bool isFatal = false;
};

} // namespace e3::core
