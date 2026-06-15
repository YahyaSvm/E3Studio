add_library(e3_api STATIC
    APIServer.cpp
    MessageHandler.cpp
)

target_include_directories(e3_api PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})
target_link_libraries(e3_api PUBLIC
    e3_core
    e3_geometry
    e3_toolpath
    e3_simulation
    e3_postprocessor
    e3_ai
    ixwebsocket::ixwebsocket
    nlohmann_json::nlohmann_json
    Boost::system
    Boost::thread
)
