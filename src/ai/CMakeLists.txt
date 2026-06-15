add_library(e3_ai STATIC
    AIOptimizer.cpp
    CuttingParameterPredictor.cpp
)

target_include_directories(e3_ai PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})
target_link_libraries(e3_ai PUBLIC
    e3_core
    e3_toolpath
)
