add_library(e3_machine STATIC MachineKinematics.cpp)
target_include_directories(e3_machine PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})
target_link_libraries(e3_machine PUBLIC e3_core e3_geometry)

add_library(e3_simulation STATIC ../simulation/SimulationEngine.cpp)
target_include_directories(e3_simulation PUBLIC ${CMAKE_SOURCE_DIR}/src/simulation)
target_link_libraries(e3_simulation PUBLIC e3_core e3_geometry e3_toolpath)

add_library(e3_postprocessor STATIC ../postprocessor/GCodeGenerator.cpp)
target_include_directories(e3_postprocessor PUBLIC ${CMAKE_SOURCE_DIR}/src/postprocessor)
target_link_libraries(e3_postprocessor PUBLIC e3_core e3_toolpath)
