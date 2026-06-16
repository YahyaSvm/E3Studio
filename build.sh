#!/usr/bin/env bash
# ==============================================================================
# E3Studio Build Script (Linux / macOS)
# Usage: ./build.sh [--release] [--no-ui] [--clean] [--parallel N]
# ==============================================================================
set -euo pipefail

# ─── Defaults ─────────────────────────────────────────────────────────────────
BUILD_TYPE="Debug"
BUILD_UI=true
CLEAN=false
PARALLEL=$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ─── Colors ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

# ─── Parse Arguments ──────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case $1 in
        --release)  BUILD_TYPE="Release"; shift ;;
        --debug)    BUILD_TYPE="Debug"; shift ;;
        --no-ui)    BUILD_UI=false; shift ;;
        --clean)    CLEAN=true; shift ;;
        --parallel) PARALLEL="$2"; shift 2 ;;
        -h|--help)
            echo "Usage: ./build.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --release       Build in Release mode (default: Debug)"
            echo "  --debug         Build in Debug mode"
            echo "  --no-ui         Skip UI build"
            echo "  --clean         Clean build directory before building"
            echo "  --parallel N    Number of parallel jobs (default: auto)"
            echo "  -h, --help      Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# ─── Platform Detection ──────────────────────────────────────────────────────
OS="$(uname -s)"
ARCH="$(uname -m)"
case "$OS" in
    Linux*)     PLATFORM="Linux";;
    Darwin*)    PLATFORM="macOS";;
    *)          echo -e "${RED}Unsupported platform: $OS${NC}"; exit 1;;
esac

echo -e "\n${CYAN}=== E3Studio Build ($PLATFORM $ARCH - $BUILD_TYPE) ===${NC}\n"

# ─── Clean ────────────────────────────────────────────────────────────────────
if [ "$CLEAN" = true ]; then
    echo -e "${YELLOW}[0/5] Cleaning build directory...${NC}"
    rm -rf "$ROOT/build/$BUILD_TYPE"
fi

# ─── vcpkg Bootstrap ─────────────────────────────────────────────────────────
if [ ! -d "$ROOT/vcpkg" ]; then
    echo -e "${YELLOW}[1/5] Cloning vcpkg...${NC}"
    git clone https://github.com/microsoft/vcpkg.git "$ROOT/vcpkg"
fi

if [ ! -f "$ROOT/vcpkg/vcpkg" ] && [ ! -f "$ROOT/vcpkg/vcpkg.exe" ]; then
    echo -e "${YELLOW}[1/5] Bootstrapping vcpkg...${NC}"
    "$ROOT/vcpkg/bootstrap-vcpkg.sh" -disableMetrics
fi

# ─── Determine Triplet ───────────────────────────────────────────────────────
case "$OS" in
    Linux*)
        case "$ARCH" in
            x86_64)   VCPKG_TRIPLET="x64-linux";;
            aarch64)  VCPKG_TRIPLET="arm64-linux";;
            *)        echo -e "${RED}Unsupported arch: $ARCH${NC}"; exit 1;;
        esac
        CMAKE_GENERATOR="Ninja"
        ;;
    Darwin*)
        case "$ARCH" in
            arm64)    VCPKG_TRIPLET="arm64-osx";;
            x86_64)   VCPKG_TRIPLET="x64-osx";;
            *)        echo -e "${RED}Unsupported arch: $ARCH${NC}"; exit 1;;
        esac
        CMAKE_GENERATOR="Ninja"
        ;;
esac

# ─── Check Dependencies ──────────────────────────────────────────────────────
echo -e "${YELLOW}[1/5] Checking dependencies...${NC}"

check_command() {
    if ! command -v "$1" &>/dev/null; then
        echo -e "${RED}Error: $1 is not installed.${NC}"
        case "$OS" in
            Linux*)
                echo -e "${GRAY}Install with: sudo apt-get install $2${NC}"
                ;;
            Darwin*)
                echo -e "${GRAY}Install with: brew install $3${NC}"
                ;;
        esac
        exit 1
    fi
}

check_command "cmake" "cmake" "cmake"
check_command "ninja" "ninja-build" "ninja" || check_command "make" "build-essential" ""
check_command "node" "nodejs" "node"
check_command "npm" "npm" "node"

echo -e "${GREEN}  cmake  : $(cmake --version | head -1)${NC}"
echo -e "${GREEN}  node   : $(node --version)${NC}"
echo -e "${GREEN}  npm    : $(npm --version)${NC}"

# ─── CMake Configure ─────────────────────────────────────────────────────────
BUILD_DIR="$ROOT/build/$BUILD_TYPE"
echo -e "\n${YELLOW}[2/5] Configuring CMake ($BUILD_TYPE, $VCPKG_TRIPLET)...${NC}"

cmake -B "$BUILD_DIR" -S "$ROOT" \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DCMAKE_TOOLCHAIN_FILE="$ROOT/vcpkg/scripts/buildsystems/vcpkg.cmake" \
    -DVCPKG_TARGET_TRIPLET="$VCPKG_TRIPLET" \
    -G "$CMAKE_GENERATOR"

if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] CMake configuration failed${NC}"
    exit 1
fi

# ─── C++ Build ────────────────────────────────────────────────────────────────
echo -e "${YELLOW}[3/5] Building C++ backend ($PARALLEL parallel jobs)...${NC}"
cmake --build "$BUILD_DIR" --config "$BUILD_TYPE" --parallel "$PARALLEL"

if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] C++ build failed${NC}"
    exit 1
fi

# ─── UI Build ─────────────────────────────────────────────────────────────────
if [ "$BUILD_UI" = true ]; then
    echo -e "${YELLOW}[4/5] Installing UI dependencies...${NC}"
    (cd "$ROOT/ui" && npm ci)

    echo -e "${YELLOW}[5/5] Building Web UI...${NC}"
    (cd "$ROOT/ui" && npm run build)
else
    echo -e "${GRAY}[4/5] Skipping UI build (--no-ui)${NC}"
    echo -e "${GRAY}[5/5] Skipping UI build (--no-ui)${NC}"
fi

# ─── Done ─────────────────────────────────────────────────────────────────────
echo -e "\n${GREEN}Build successful!${NC}"
echo -e "  Binary : $BUILD_DIR/bin/E3Studio"
if [ "$BUILD_UI" = true ]; then
    echo -e "  UI     : $ROOT/ui/dist/"
fi
echo -e "\n${CYAN}Run with: ./launch.sh${NC}"
