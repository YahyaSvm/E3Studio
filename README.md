# E3Studio

E3Studio is an open-source CAM workbench for CNC workflows: import geometry, prepare stock and tools, create machining operations, preview toolpaths, simulate motion, and export G-Code.

The repository currently contains two clients around the same product idea:

- a Windows WPF desktop application in C# with the most complete UI and CAM workflow
- a cross-platform C++20 backend plus React/Three.js web UI that is being built out as the portable runtime

The project is early-stage. Some modules are production-shaped, while others are scaffolding or partial implementations. Treat generated G-Code as untrusted until it has been verified in your controller/simulator.

## What is implemented

Current repository capabilities include:

- WPF desktop CAM UI for Windows
  - project creation, stock setup, save/load, recent projects, autosave
  - 2D canvas workflow with layers, selection, transform tools, copy/cut/paste, undo/redo
  - 3D stock/STL visualization through HelixToolkit
  - dialogs for stock, materials, tools, settings, post processors, export, and imports

- Import/export services
  - SVG, DXF, STL, STEP/IGES, 3MF, PDF, Gerber, and G-Code importer service classes
  - E3Studio project serialization through `.e3p` JSON files

- CAM and simulation services
  - profile, pocket, drilling, V-carve, tab generation, lead-in/out, nesting, toolpath optimization, collision and stock-removal simulation classes
  - realtime simulator/controller classes for G-Code playback

- G-Code generation
  - C# generator with model-aware safety heights, operation ordering, tool changes, spindle/coolant controls, and manual/canned drill cycle support
  - C# post processors for GRBL, Generic/Fanuc style, Fanuc, Haas, Mazak, LinuxCNC, Mach3, and Mach4; the post-processor dialog also lists Klipper, Heidenhain, and Siemens Sinumerik presets
  - C++ generator with Fanuc, Haas, Heidenhain, and generic ISO configurations

- C++ backend prototype
  - module layout for core project management, geometry, toolpath, simulation, machine, postprocessor, AI, and WebSocket API
  - WebSocket server on `ws://localhost:9001`
  - JSON project state and operation management
  - physics-based feed/speed fallback in the AI module; ONNX is intentionally disabled for now

- React/Three.js web UI prototype
  - Vite + React + TypeScript frontend
  - Three.js viewport, operation panel, toolbar, notifications, Zustand store
  - WebSocket client with reconnect handling
  - English/Turkish i18n toggle

## Repository layout

```text
E3Studio/
├── App.xaml, MainWindow.xaml(.cs)     Windows WPF desktop app
├── CAM/                               C# CAM engines and simulation helpers
├── Controls/, Dialogs/, Models/       WPF UI and domain models
├── Services/                          Importers, G-Code, settings, post processors
├── src/                               C++20 backend modules
│   ├── core/                          Application, logger, events, projects, threads
│   ├── geometry/                      OpenCASCADE geometry and mesh processing
│   ├── toolpath/                      Toolpath engine and operations
│   ├── simulation/                    C++ simulation engine
│   ├── postprocessor/                 C++ G-Code generation
│   ├── ai/                            feed/speed prediction fallback
│   └── api/                           WebSocket API server
├── ui/                                React/Three.js web UI
├── docs/                              architecture, API, post-processor docs
├── build.sh, build.ps1                local build scripts
├── launch.sh, launch.ps1              local launch scripts
├── CMakeLists.txt, vcpkg.json         C++ build and dependencies
└── E3Studio.csproj, E3Studio.sln      WPF/.NET build files
```

## Tech stack

- C++20, CMake 3.25+, vcpkg
- OpenCASCADE, Boost, nlohmann/json, ixwebsocket, spdlog
- .NET 10 WPF, HelixToolkit.Wpf, AvalonDock, System.IO.Ports
- React 18, TypeScript, Vite, Three.js, React Three Fiber, Zustand, Immer, Tailwind CSS, i18next
- Docker for Linux container builds

## Quick start: web UI and C++ backend

Requirements:

- CMake 3.25+
- Ninja or a supported CMake generator
- a C++20 compiler
- Node.js 20+ and npm
- Git
- vcpkg is bootstrapped by the build scripts if missing

On macOS:

```bash
brew install cmake ninja node
./build.sh
./launch.sh --dev
```

On Ubuntu/Debian:

```bash
sudo apt-get update
sudo apt-get install -y build-essential cmake git ninja-build nodejs npm \
  libx11-dev libxrandr-dev libxinerama-dev libxcursor-dev libxi-dev \
  libgl1-mesa-dev libglu1-mesa-dev mesa-common-dev libfontconfig1-dev libssl-dev
./build.sh
./launch.sh --dev
```

Common script options:

```bash
./build.sh --release          # Release build
./build.sh --no-ui            # C++ backend only
./build.sh --clean            # remove build/<type> first
./build.sh --parallel 8       # choose build parallelism

./launch.sh --dev             # backend + Vite dev server
./launch.sh --backend-only    # backend only
./launch.sh --ui-only --dev   # Vite UI only
```

The backend listens on `ws://localhost:9001`. The web UI runs at `http://localhost:3000` in the scripts/docs, although Vite may choose another port if 3000 is already occupied.

## Quick start: Windows WPF app

Requirements:

- Windows 10/11
- Visual Studio 2022 with C++ workload if building the C++ backend
- .NET 10 SDK
- Node.js 20+ if building the web UI

```powershell
.\build.ps1
.\launch.ps1
```

Useful build variants:

```powershell
.\build.ps1 -Release
.\build.ps1 -NoBuildUI

dotnet build -c Release
```

The WPF client is Windows-only because it targets `net10.0-windows` and `UseWPF=true`.

## Manual build

```bash
# Configure C++ backend
cmake -B build/Debug -S . \
  -DCMAKE_BUILD_TYPE=Debug \
  -DCMAKE_TOOLCHAIN_FILE=vcpkg/scripts/buildsystems/vcpkg.cmake \
  -G Ninja

# Build C++ backend
cmake --build build/Debug --parallel

# Build web UI
cd ui
npm ci
npm run build
```

## Docker

```bash
docker compose up --build
```

Current Docker notes:

- the image builds the C++ backend and web UI
- ports 3000 and 9001 are exposed
- the checked-in Dockerfile command starts only `/app/E3Studio`; serve/static UI startup may need adjustment if you want a single container to host both backend and UI

## WebSocket API status

The C++ backend exposes a JSON-over-WebSocket API. The implemented dispatch table currently handles:

- `project.new`, `project.open`, `project.save`, `project.get`
- `operation.add`, `operation.update`, `operation.remove`, `operation.compute`
- `toolpath.get`, `toolpath.export`
- `simulation.start`, `simulation.step`, `simulation.pause`
- `ai.optimize`
- `model.load`, `mesh.get`

See `docs/API.md` for the current protocol and message shapes. Older names such as `import.stl`, `toolpath.generate`, and `export.gcode` are not the current C++ API names.

## Documentation

- `docs/ARCHITECTURE.md` explains the hybrid C# / C++ / React architecture and module responsibilities.
- `docs/API.md` documents the current WebSocket protocol implemented by `src/api/MessageHandler.cpp`.
- `docs/POST_PROCESSORS.md` explains post-processor behavior and controller-specific G-Code considerations.
- `CONTRIBUTING.md` covers contribution workflow and project conventions.

## Development notes

- The C++ code currently uses Turkish comments/log messages in several files. The React UI has English and Turkish runtime strings.
- The C++ API returns `status: "ok"` plus a `data` object for successful responses, or `status: "error"` plus `message` for failures.
- The React file input uses `(file as any).path ?? file.name`; browsers usually expose only the file name, so full-path loading is mainly suitable for desktop/Electron-like environments or future file-upload handling.
- `ai.optimize` is deterministic physics fallback today, not a trained model inference path.
- There are no dedicated automated test projects in the repository yet; build verification is the main quality gate.

## Safety disclaimer

CNC output can damage machines, stock, tooling, or injure people. Always dry-run, simulate, inspect clearances, verify work offsets, and test with the spindle disabled before running generated G-Code on real hardware.

## License

MIT. See `LICENSE`.
