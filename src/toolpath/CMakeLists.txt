add_library(e3_toolpath STATIC
    ToolpathEngine.cpp
    operations/PocketOperation.cpp
    operations/ContourOperation.cpp
    operations/AdaptiveClearingOperation.cpp
    operations/SurfaceFinishingOperation.cpp
    Offsetter.cpp
    PathOptimizer.cpp
)

target_include_directories(e3_toolpath PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})
target_link_libraries(e3_toolpath PUBLIC e3_core e3_geometry)
