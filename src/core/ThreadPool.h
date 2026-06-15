#pragma once
// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: Core :: ThreadPool
// İş parçacığı havuzu. CPU yoğun işlemler (toolpath, simülasyon) burada çalışır.
// Toolpath hesaplama UI'ı bloklamamalı — tüm ağır iş asenkron.
// ─────────────────────────────────────────────────────────────────────────────

#include <thread>
#include <queue>
#include <functional>
#include <mutex>
#include <condition_variable>
#include <future>
#include <atomic>
#include <vector>
#include <stdexcept>

namespace e3::core {

class ThreadPool {
public:
    explicit ThreadPool(size_t threadCount = 0) {
        size_t count = threadCount > 0
            ? threadCount
            : std::max(1u, std::thread::hardware_concurrency() - 1);

        m_workers.reserve(count);
        for (size_t i = 0; i < count; ++i) {
            m_workers.emplace_back([this] { workerLoop(); });
        }
    }

    ~ThreadPool() {
        {
            std::lock_guard lock(m_mutex);
            m_stopping = true;
        }
        m_cv.notify_all();
        for (auto& t : m_workers) {
            if (t.joinable()) t.join();
        }
    }

    // Görevi kuyruğa ekle, future döner
    template<typename Func, typename... Args>
    auto submit(Func&& func, Args&&... args)
        -> std::future<std::invoke_result_t<Func, Args...>>
    {
        using ReturnType = std::invoke_result_t<Func, Args...>;
        auto task = std::make_shared<std::packaged_task<ReturnType()>>(
            std::bind(std::forward<Func>(func), std::forward<Args>(args)...)
        );

        auto future = task->get_future();
        {
            std::lock_guard lock(m_mutex);
            if (m_stopping) throw std::runtime_error("ThreadPool durduruldu");
            m_queue.emplace([task]() { (*task)(); });
        }
        m_cv.notify_one();
        return future;
    }

    size_t threadCount() const { return m_workers.size(); }
    size_t queueSize() const {
        std::lock_guard lock(m_mutex);
        return m_queue.size();
    }

private:
    void workerLoop() {
        while (true) {
            std::function<void()> task;
            {
                std::unique_lock lock(m_mutex);
                m_cv.wait(lock, [this] {
                    return m_stopping || !m_queue.empty();
                });
                if (m_stopping && m_queue.empty()) return;
                task = std::move(m_queue.front());
                m_queue.pop();
            }
            task();
        }
    }

    std::vector<std::thread>           m_workers;
    std::queue<std::function<void()>>  m_queue;
    mutable std::mutex                 m_mutex;
    std::condition_variable            m_cv;
    bool                               m_stopping = false;
};

} // namespace e3::core
