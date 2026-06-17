cmake_minimum_required(VERSION 3.25)
project(E3Studio VERSION 0.1.0 LANGUAGES CXX)

# ─── C++ Standartı ───────────────────────────────────────────────────────────
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ─── Derleme Çıktısı ─────────────────────────────────────────────────────────
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# ─── vcpkg Entegrasyonu ───────────────────────────────────────────────────────
if(DEFINED ENV{VCPKG_ROOT})
    set(CMAKE_TOOLCHAIN_FILE "$ENV{VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake"
        CACHE STRING "Vcpkg toolchain file")
endif()

# ─── Bağımlılıklar ───────────────────────────────────────────────────────────
find_package(OpenCASCADE REQUIRED)          # Geometri çekirdeği
find_package(Boost REQUIRED COMPONENTS
    system filesystem thread)               # Sistem yardımcıları
find_package(nlohmann_json REQUIRED)        # JSON mesajlaşma
find_package(ixwebsocket REQUIRED)          # WebSocket sunucusu
find_package(spdlog REQUIRED)               # Loglama
# onnxruntime devre dışı — AI modülü physics fallback kullanıyor
find_package(OpenGL REQUIRED)               # Temel 3D render

# ─── Derleyici Uyarı Seviyeleri ──────────────────────────────────────────────
if(MSVC)
    add_compile_options(/W4 /WX- /MP /permissive-)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS NOMINMAX WIN32_LEAN_AND_MEAN)
else()
    add_compile_options(-Wall -Wextra -Wpedantic)
endif()

# ─── Release optimizasyonu ───────────────────────────────────────────────────
if(CMAKE_BUILD_TYPE STREQUAL "Release")
    if(MSVC)
        add_compile_options(/O2 /GL)
        add_link_options(/LTCG)
    else()
        add_compile_options(-O3 -march=native)
    endif()
endif()

# ─── Global Include Yolları ──────────────────────────────────────────────────
include_directories(${CMAKE_SOURCE_DIR}/src)

# ─── Alt Modüller ────────────────────────────────────────────────────────────
add_subdirectory(src/core)
add_subdirectory(src/geometry)
add_subdirectory(src/toolpath)
add_subdirectory(src/machine)
add_subdirectory(src/simulation)
add_subdirectory(src/postprocessor)
add_subdirectory(src/ai)
add_subdirectory(src/api)

# ─── Ana Çalıştırılabilir ─────────────────────────────────────────────────────
add_executable(E3Studio src/main.cpp)

target_link_libraries(E3Studio PRIVATE
    e3_core
    e3_geometry
    e3_toolpath
    e3_machine
    e3_simulation
    e3_postprocessor
    e3_ai
    e3_api
    spdlog::spdlog
    nlohmann_json::nlohmann_json
)

if(WIN32)
    target_link_libraries(E3Studio PRIVATE bcrypt)
endif()

# ─── UI Kaynakları ───────────────────────────────────────────────────────────
add_custom_target(build_ui
    COMMAND npm run build
    WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}/ui
    COMMENT "React UI derleniyor..."
)
