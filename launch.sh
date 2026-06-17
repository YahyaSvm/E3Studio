#!/usr/bin/env bash
# ==============================================================================
# E3Studio Launch Script (Linux / macOS)
# Starts C++ backend and opens Web UI in browser
# Usage: ./launch.sh [--release] [--dev] [--backend-only] [--ui-only]
# ==============================================================================
set -euo pipefail

# ─── Defaults ─────────────────────────────────────────────────────────────────
BUILD_TYPE="Debug"
DEV_UI=false
BACKEND_ONLY=false
UI_ONLY=false
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
        --release)      BUILD_TYPE="Release"; shift ;;
        --debug)        BUILD_TYPE="Debug"; shift ;;
        --dev)          DEV_UI=true; shift ;;
        --backend-only) BACKEND_ONLY=true; shift ;;
        --ui-only)      UI_ONLY=true; shift ;;
        -h|--help)
            echo "Usage: ./launch.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --release       Launch Release build (default: Debug)"
            echo "  --debug         Launch Debug build"
            echo "  --dev           Use Vite dev server (hot reload)"
            echo "  --backend-only  Only start C++ backend"
            echo "  --ui-only       Only start Web UI"
            echo "  -h, --help      Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

echo -e "\n${CYAN}=== E3Studio Launching ===${NC}\n"

# ─── Cleanup Handler ─────────────────────────────────────────────────────────
BACKEND_PID=""
UI_PID=""

cleanup() {
    echo -e "\n${YELLOW}Shutting down...${NC}"
    if [ -n "$BACKEND_PID" ]; then
        kill "$BACKEND_PID" 2>/dev/null || true
        echo -e "${GRAY}  Backend stopped (PID: $BACKEND_PID)${NC}"
    fi
    if [ -n "$UI_PID" ]; then
        kill "$UI_PID" 2>/dev/null || true
        echo -e "${GRAY}  UI stopped (PID: $UI_PID)${NC}"
    fi
    exit 0
}
trap cleanup SIGINT SIGTERM EXIT

# ─── Find Backend Binary ─────────────────────────────────────────────────────
EXE_PATH="$ROOT/build/$BUILD_TYPE/bin/E3Studio"
if [ ! -f "$EXE_PATH" ]; then
    EXE_PATH="$ROOT/build/$BUILD_TYPE/E3Studio"
fi
if [ ! -f "$EXE_PATH" ]; then
    EXE_PATH="$ROOT/build/bin/E3Studio"
fi

if [ "$UI_ONLY" = false ]; then
    if [ ! -f "$EXE_PATH" ]; then
        echo -e "${RED}[ERROR] E3Studio backend not found. Run ./build.sh first.${NC}"
        exit 1
    fi

    # ─── Start Backend ───────────────────────────────────────────────────────
    echo -e "${YELLOW}[1/3] Starting backend: $EXE_PATH${NC}"
    "$EXE_PATH" &
    BACKEND_PID=$!
    echo -e "${GREEN}  Backend PID: $BACKEND_PID${NC}"

    echo -e "${GRAY}  Waiting for backend to initialize...${NC}"
    sleep 2
else
    echo -e "${GRAY}[1/3] Skipping backend (--ui-only)${NC}"
fi

if [ "$BACKEND_ONLY" = false ]; then
    # ─── Start UI ────────────────────────────────────────────────────────────
    if [ "$DEV_UI" = true ]; then
        echo -e "${YELLOW}[2/3] Starting Vite dev server...${NC}"
        (cd "$ROOT/ui" && npm run dev) &
        UI_PID=$!
        sleep 3
        echo -e "${GREEN}  Dev server started${NC}"
    else
        if [ -d "$ROOT/ui/dist" ]; then
            echo -e "${YELLOW}[2/3] Serving production UI...${NC}"
            if command -v npx &>/dev/null; then
                (cd "$ROOT" && npx serve ui/dist -p 3000 -s) &
                UI_PID=$!
                sleep 2
            else
                echo -e "${RED}[ERROR] npx not found. Install Node.js or use --dev flag.${NC}"
                exit 1
            fi
        else
            echo -e "${RED}[ERROR] UI dist not found. Run ./build.sh first.${NC}"
            exit 1
        fi
    fi

    # ─── Open Browser ────────────────────────────────────────────────────────
    echo -e "${YELLOW}[3/3] Opening browser...${NC}"
    case "$(uname -s)" in
        Linux*)
            if command -v xdg-open &>/dev/null; then
                xdg-open "http://localhost:3000" 2>/dev/null &
            elif command -v sensible-browser &>/dev/null; then
                sensible-browser "http://localhost:3000" 2>/dev/null &
            fi
            ;;
        Darwin*)
            open "http://localhost:3000" 2>/dev/null &
            ;;
    esac
else
    echo -e "${GRAY}[2/3] Skipping UI (--backend-only)${NC}"
    echo -e "${GRAY}[3/3] Skipping browser (--backend-only)${NC}"
fi

# ─── Status ───────────────────────────────────────────────────────────────────
echo -e "\n${GREEN}E3Studio is running!${NC}"
echo -e "  Backend : ws://localhost:9001"
if [ "$BACKEND_ONLY" = false ]; then
    echo -e "  UI      : http://localhost:3000"
fi
echo -e "\n${GRAY}Press Ctrl+C to stop${NC}"

# ─── Wait ─────────────────────────────────────────────────────────────────────
if [ -n "$BACKEND_PID" ]; then
    wait "$BACKEND_PID"
elif [ -n "$UI_PID" ]; then
    wait "$UI_PID"
fi
