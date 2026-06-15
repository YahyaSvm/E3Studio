add_library(e3_core STATIC
    Application.cpp
    EventBus.cpp
    ThreadPool.cpp
    Logger.cpp
    ProjectManager.cpp
)

target_include_directories(e3_core PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})
target_link_libraries(e3_core PUBLIC
    spdlog::spdlog
    nlohmann_json::nlohmann_json
    Boost::system
    Boost::filesystem
    Boost::thread
)
