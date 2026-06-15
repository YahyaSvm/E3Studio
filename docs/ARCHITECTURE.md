# E3Studio Architecture

## System Overview

E3Studio uses a hybrid architecture that separates computation-intensive operations into a high-performance C++ backend while providing a modern, responsive web-based UI through React/Three.js. An optional WPF desktop client is available for Windows users who prefer a native experience.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Client Layer                                    │
│                                                                              │
│  ┌──────────────────────┐  ┌──────────────────────────────────────────────┐ │
│  │   WPF Desktop Client │  │          React Web UI                         │ │
│  │   (.NET 10 / C#)     │  │          (React 18 + Three.js + Zustand)      │ │
│  │                      │  │                                               │ │
│  │  • HelixToolkit 3D   │  │  • React Three Fiber 3D viewport             │ │
│  │  • AvalonDock panels │  │  • Tailwind CSS responsive layout             │ │
│  │  • Native Windows UI │  │  • Vite HMR dev server                        │ │
│  │  • Direct CAM access │  │  • WebSocket client                           │ │
│  └──────────┬───────────┘  └──────────────────┬───────────────────────────┘ │
│             │                                  │                             │
│             │ Direct (C#)                      │ WebSocket (JSON)            │
│             │                                  │ ws://localhost:9001         │
└─────────────┼──────────────────────────────────┼────────────────────────────┘
              │                                  │
┌─────────────┼──────────────────────────────────┼────────────────────────────┐
│             ▼              Backend Layer         ▼                            │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │                        C++20 Core Engine                                 │ │
│  │                                                                          │ │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────────┐  │ │
│  │  │   Geometry   │ │  Toolpath   │ │ Simulation  │ │ Post Processor  │  │ │
│  │  │   Module     │ │   Engine    │ │   Engine    │ │    Generator    │  │ │
│  │  │             │ │             │ │             │ │                 │  │ │
│  │  │ OpenCASCADE │ │ Profile     │ │ Real-time   │ │ GRBL            │  │ │
│  │  │ BRep        │ │ Pocket      │ │ Stock rmvl  │ │ Klipper         │  │ │
│  │  │ STL/STEP    │ │ Drill       │ │ Collision   │ │ Mach3/4         │  │ │
│  │  │ Mesh        │ │ V-Carve     │ │ Speed ctrl  │ │ LinuxCNC        │  │ │
│  │  └─────────────┘ │ Nesting     │ │             │ │ Fanuc/Haas/...  │  │ │
│  │                  └─────────────┘ └─────────────┘ └─────────────────┘  │ │
│  │                                                                          │ │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────────┐  │ │
│  │  │    Core      │ │   Machine   │ │     API     │ │      AI         │  │ │
│  │  │             │ │   Control   │ │   Server    │ │    Module       │  │ │
│  │  │ Application │ │ Serial/USB  │ │ WebSocket   │ │ Physics-based   │  │ │
│  │  │ Logger      │ │ DRO         │ │ JSON proto  │ │ Feed/speed opt  │  │ │
│  │  │ EventBus    │ │ Probe       │ │ Message hub │ │ Tool wear pred  │  │ │
│  │  │ ThreadPool  │ │ G-Code send │ │             │ │                 │  │ │
│  │  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────────┘  │ │
│  │                                                                          │ │
│  └──────────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Module Descriptions

### Core Module (`src/core/`)

The foundation layer providing application lifecycle and infrastructure services.

| Component | File | Description |
|-----------|------|-------------|
| **Application** | `Application.h/cpp` | Singleton managing initialization, shutdown, and global state |
| **Logger** | `Logger.h/cpp` | Structured logging via spdlog with file and console sinks |
| **EventBus** | `EventBus.h/cpp` | Publish-subscribe event system for inter-module communication |
| **ThreadPool** | `ThreadPool.h/cpp` | Worker thread pool for parallel computation |
| **ProjectManager** | `ProjectManager.h/cpp` | Project file I/O, autosave, recent projects |

### Geometry Module (`src/geometry/`)

Handles 3D geometry processing using OpenCASCADE Technology (OCCT).

| Component | File | Description |
|-----------|------|-------------|
| **GeometryKernel** | `GeometryKernel.h/cpp` | Core geometry operations, BRep manipulation |
| **MeshLoader** | `MeshLoader.cpp` | STL/3MF mesh loading and triangulation |
| **BRepProcessor** | `BRepProcessor.cpp` | STEP/IGES import, BRep operations (boolean, offset) |
| **GeometryUtils** | `GeometryUtils.cpp` | Utility functions (transformations, measurements) |

### Toolpath Module (`src/toolpath/`)

Generates CNC toolpaths from geometry.

| Component | File | Description |
|-----------|------|-------------|
| **ToolpathEngine** | `ToolpathEngine.h/cpp` | Main engine coordinating toolpath generation |
| **Offsetter** | `Offsetter.cpp` | 2D path offset calculation (tool radius compensation) |
| **PathOptimizer** | `PathOptimizer.cpp` | TSP-based travel path optimization |
| **Operations/** | `operations/` | Individual operation implementations (profile, pocket, drill) |

### Simulation Module (`src/simulation/`)

Real-time G-Code simulation and verification.

| Component | Description |
|-----------|-------------|
| **Simulator** | G-Code interpreter with position tracking |
| **StockRemoval** | Material removal volume calculation |
| **CollisionDetector** | Tool-holder collision checking |
| **Timeline** | Frame-by-frame animation control |

### Post Processor Module (`src/postprocessor/`)

Generates controller-specific G-Code output.

| Component | Description |
|-----------|-------------|
| **PostProcessorBase** | Abstract base for all post processors |
| **GrblPostProcessor** | GRBL-specific output (no line numbers, no tool change) |
| **IndustrialPostProcessors** | Fanuc, Haas, Heidenhain, Sinumerik |
| **PostProcessorOptions** | Configuration (line numbers, units, coordinate system) |

### API Module (`src/api/`)

WebSocket server for UI communication.

| Component | File | Description |
|-----------|------|-------------|
| **APIServer** | `APIServer.h/cpp` | ixwebsocket-based server on port 9001 |
| **MessageHandler** | `MessageHandler.h/cpp` | JSON message routing and dispatch |

### AI Module (`src/ai/`)

Optional AI/ML features for parameter optimization.

| Component | Description |
|-----------|-------------|
| **FeedSpeedOptimizer** | Physics-based feed/speed recommendation |
| **ToolWearPredictor** | Tool wear estimation based on cutting parameters |

## Data Flow

### Toolpath Generation Flow

```
User Input (geometry + parameters)
         │
         ▼
┌─────────────────┐
│  Geometry Module │ ← STL/STEP/DXF/SVG import
│  (BRep / Mesh)   │
└────────┬────────┘
         │ Raw geometry
         ▼
┌─────────────────┐
│  Toolpath Engine │ ← Tool selection, cutting parameters
│  (Offset, Ops)   │
└────────┬────────┘
         │ Raw toolpath (G0/G1 moves)
         ▼
┌─────────────────┐
│ Path Optimizer   │ ← TSP travel optimization
│ (TSP Solver)     │
└────────┬────────┘
         │ Optimized toolpath
         ▼
┌─────────────────┐
│ Post Processor   │ ← Controller selection
│ (G-Code Output)  │
└────────┬────────┘
         │ Controller-specific G-Code
         ▼
     Export / Simulation
```

### WebSocket Protocol

```
Client (UI)                          Server (Backend)
    │                                     │
    │  {"type":"project.new", ...}        │
    │────────────────────────────────────>│
    │                                     │ Process request
    │  {"type":"project.created", ...}    │
    │<────────────────────────────────────│
    │                                     │
    │  {"type":"toolpath.generate", ...}  │
    │────────────────────────────────────>│
    │                                     │ Compute toolpath
    │  {"type":"progress", "pct": 45}     │ (streaming)
    │<────────────────────────────────────│
    │  {"type":"progress", "pct": 100}    │
    │<────────────────────────────────────│
    │  {"type":"toolpath.result", ...}    │
    │<────────────────────────────────────│
    │                                     │
```

## Cross-Platform Strategy

### Platform Abstraction Layers

| Concern | Abstraction | Windows | Linux | macOS |
|---------|------------|---------|-------|-------|
| **Filesystem** | `std::filesystem` | Win32 paths | POSIX paths | POSIX paths |
| **Threading** | `std::thread` | Win32 threads | pthreads | pthreads |
| **Networking** | ixwebsocket | Winsock2 | POSIX sockets | POSIX sockets |
| **Logging** | spdlog | Console + File | Console + File | Console + File |
| **3D Rendering** | Three.js (WebGL) | Browser | Browser | Browser |
| **Desktop UI** | WPF (Windows only) | Native | N/A | N/A |
| **Serial Port** | Platform-specific | Win32 API | termios | IOKit |

### Build Matrix

| Platform | Compiler | Generator | Triplet |
|----------|----------|-----------|---------|
| Windows x64 | MSVC 2022 | Visual Studio 17 | `x64-windows` |
| Linux x64 | GCC 12+ / Clang 15+ | Ninja | `x64-linux` |
| Linux ARM64 | GCC 12+ | Ninja | `arm64-linux` |
| macOS ARM64 | Apple Clang 15+ | Ninja | `arm64-osx` |
| macOS x64 | Apple Clang 15+ | Ninja | `x64-osx` |

## Dependency Graph

```
                    ┌──────────────┐
                    │   E3Studio   │
                    │  (main.cpp)  │
                    └──────┬───────┘
                           │
              ┌────────────┼────────────┐
              │            │            │
              ▼            ▼            ▼
        ┌──────────┐ ┌──────────┐ ┌──────────┐
        │  e3_api  │ │  e3_ai   │ │ e3_core  │
        └────┬─────┘ └────┬─────┘ └────┬─────┘
             │            │            │
             ▼            ▼            ▼
        ┌──────────┐ ┌──────────┐ ┌──────────────┐
        │e3_machine│ │e3_post-  │ │  e3_geometry │
        │          │ │processor │ │              │
        └────┬─────┘ └────┬─────┘ └──────┬───────┘
             │            │              │
             ▼            ▼              ▼
        ┌──────────┐ ┌──────────┐ ┌──────────────┐
        │e3_simul- │ │e3_tool-  │ │  OpenCASCADE │
        │  ation   │ │  path    │ │    Boost     │
        └──────────┘ └──────────┘ └──────────────┘
```

## Performance Considerations

| Operation | Approach | Optimization |
|-----------|----------|-------------|
| **STL Import** | Parallel mesh loading | ThreadPool, memory-mapped I/O |
| **BRep Operations** | OpenCASCADE kernels | Incremental computation |
| **Toolpath Generation** | 2D offset + sweep | Spatial indexing (R-tree) |
| **Path Optimization** | TSP nearest-neighbor | 2-opt improvement heuristic |
| **Simulation** | G-Code interpreter | Pre-computed tool positions |
| **Stock Removal** | Voxel-based | GPU compute (future) |
| **WebSocket** | Binary JSON | Message batching |
