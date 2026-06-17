# Architecture

E3Studio is a hybrid CAM application. The repository contains a mature Windows desktop path in C# and an in-progress cross-platform runtime made of a C++ backend and a React/Three.js web UI.

The important architectural distinction is:

- WPF/C# is currently the richest product surface.
- C++/React is the portable backend/web direction.
- Shared concepts exist across both paths, but they are not yet one unified runtime.

## High-level system

```text
                              Users
                                │
              ┌─────────────────┴─────────────────┐
              │                                   │
              ▼                                   ▼
   Windows WPF desktop app              React/Three.js web UI
   App.xaml / MainWindow.xaml           ui/src/*
   .NET 10 + HelixToolkit               Vite + Zustand + i18n
              │                                   │
              │ direct C# services                │ WebSocket JSON
              │                                   ▼
              │                         C++ API server
              │                         src/api/* on :9001
              │                                   │
              └──────────────┬────────────────────┘
                             ▼
                      CAM domain concepts
       projects, stock, tools, operations, toolpaths, simulation, G-Code
```

## Runtime paths

### Windows WPF path

The WPF app is defined by:

- `App.xaml`, `App.xaml.cs`
- `MainWindow.xaml`, `MainWindow.xaml.cs`
- `Controls/`, `Dialogs/`, `Resources/`
- `Models/`, `Services/`, `CAM/`

It owns the most complete end-user workflow:

- new/open/save `.e3p` projects
- recent projects and autosave
- stock/material/tool configuration dialogs
- 2D canvas selection and transform tools
- layer/project tree management
- SVG/DXF/STL and other importer service classes
- CAM operation creation and G-Code generation
- realtime and stock-removal simulation classes
- post-processor management dialogs

This path builds with `E3Studio.csproj`, targets `net10.0-windows`, and uses WPF-specific dependencies. It is Windows-only.

### C++ backend path

The C++ backend starts at `src/main.cpp`:

1. initializes logging under `logs`
2. initializes the singleton `core::Application`
3. starts `api::APIServer` on port 9001
4. waits until SIGINT/SIGTERM
5. stops the API and shuts down the app

The backend is split into CMake libraries:

- `src/core/`
  - application lifecycle
  - logging
  - event bus
  - thread pool
  - project manager and JSON project state

- `src/geometry/`
  - OpenCASCADE-based shape loading/processing
  - mesh tessellation for viewport use

- `src/toolpath/`
  - toolpath engine
  - operation implementations for pocket, contour, adaptive clearing, surface finishing
  - path optimization and offsetting

- `src/simulation/`
  - C++ simulation engine

- `src/machine/`
  - machine kinematics/control abstractions

- `src/postprocessor/`
  - C++ G-Code generator
  - Fanuc, Haas, Heidenhain, and generic ISO post configs

- `src/ai/`
  - cutting parameter prediction API
  - current implementation uses physics fallback, not ONNX runtime

- `src/api/`
  - WebSocket server
  - JSON message handler and dispatch table

### React web UI path

The web UI lives under `ui/` and is built with Vite.

Core files:

- `ui/src/App.tsx` lays out toolbar, side panel, resizable viewport, notifications, and loading overlay.
- `ui/src/components/Toolbar/Toolbar.tsx` handles new/open/save actions, panel switching, viewport mode, and language toggle.
- `ui/src/components/OperationPanel/OperationPanel.tsx` creates operations, triggers computation, and requests AI suggestions.
- `ui/src/components/Viewport3D/Viewport3D.tsx` renders models, toolpaths, grid, lights, orbit controls, and gizmo through React Three Fiber.
- `ui/src/store/useStore.ts` stores project/UI state with Zustand and Immer.
- `ui/src/lib/wsClient.ts` owns WebSocket connection, request/response matching, reconnection, and push-event handling.
- `ui/src/lib/i18n/` contains English and Turkish translations.

The web UI talks only to `ws://localhost:9001` today. It does not directly call C# services.

## Data flow: C++ web workflow

```text
User action in React
   │
   ▼
ws.send(type, payload)
   │
   ▼
WebSocket message to C++ APIServer
   │
   ▼
MessageHandler dispatch table
   │
   ├── ProjectManager for project/model/operation state
   ├── ToolpathEngine for async computation
   ├── GCodeGenerator for export
   └── CuttingParameterPredictor for feed/speed fallback
   │
   ▼
JSON response with status/data or status/message
   │
   ▼
Zustand store update and UI notification
```

The frontend is also prepared for server push events such as `toolpath.generated`, `simulation.frame`, and `ai.prediction`.

## Data model concepts

### Project

C++ projects are represented by `e3::core::Project` and managed through `ProjectManager`.

Important fields:

- `id`, `name`, `createdAt`, `updatedAt`
- `MachineConfig machine`
- `models`
- `toolLibrary`
- `operations`
- `outputDir`

Current C++ JSON persistence includes project metadata, tool library, and operations. Model references and machine configuration exist in memory but are not fully serialized yet.

### Tool

Tools include:

- ID and name
- type: flat endmill, ball endmill, bull nose, drill, tap
- diameter, corner radius, flute count, overall length, cutting length
- material such as Carbide or HSS

### Operation

C++ operation types are:

- Pocket2D
- Contour2D
- SurfaceFinishing
- AdaptiveClearing
- Drilling
- Threading

Operations carry tool references, geometry references, feed/plunge rates, spindle speed, depth of cut, stepover, stock-to-leave, tolerance, dirty state, and computed toolpath ID.

### Model reference

A model has:

- generated ID
- file path
- role: workpiece, stock, or fixture
- transform matrix

Current mesh serving path loads/tessellates with the C++ geometry kernel.

## WebSocket API

Implemented API messages are documented in `docs/API.md`.

The API layer is deliberately thin:

- parse envelope
- dispatch by `type`
- call core/toolpath/postprocessor/AI services
- return JSON

Current successful responses are shaped as:

```json
{
  "status": "ok",
  "id": "request-id",
  "data": {}
}
```

Errors are shaped as:

```json
{
  "status": "error",
  "id": "request-id",
  "message": "error text"
}
```

## Build architecture

### C++

The root `CMakeLists.txt` requires CMake 3.25 and C++20. It expects these vcpkg packages:

- opencascade
- boost-system
- boost-filesystem
- boost-thread
- nlohmann-json
- ixwebsocket
- spdlog

`build.sh` and `build.ps1` bootstrap vcpkg locally when needed, configure CMake, build the C++ backend, and optionally build the web UI.

### WPF

`E3Studio.csproj` targets `net10.0-windows` and references:

- Dirkster.AvalonDock
- Dirkster.AvalonDock.Themes.VS2013
- HelixToolkit.Wpf
- System.IO.Ports

### Web UI

`ui/package.json` exposes:

- `npm run dev`
- `npm run build`
- `npm run preview`

`npm run build` runs TypeScript compilation and then Vite production build.

## Current integration gaps to know about

These are architectural facts, not failures:

- The WPF app and C++/React runtime are not fully unified.
- The C++ API does not implement every planned command previously documented.
- The React operation type currently sends strings while the C++ operation parser expects enum integers.
- The web file picker does not reliably expose absolute file paths in normal browsers.
- The C++ mesh endpoint currently uses STEP loading in `mesh.get`; broader import support exists mostly in C# services.
- The Dockerfile starts only the backend binary in its final command, even though the image also contains `ui/dist`.
- AI prediction is a physics fallback, not model inference.
- Automated tests are not yet a first-class part of the repository; build success is the main verification signal.

## Extension points

Good places to extend the system:

- Add new WebSocket messages in `MessageHandler.cpp` and document them in `docs/API.md`.
- Wire EventBus results to API push events for long-running toolpath/simulation tasks.
- Add serialization fields to `Project::toJson()` / `Project::fromJson()` when making more project state durable.
- Expand `toolpath.export` post selection to expose every configured C++ post processor.
- Add upload or sandboxed file-access handling for browser-safe model import.
- Add tests around project serialization, message handling, G-Code generation, and operation parsing.

## Safety model

CAM software needs conservative defaults. E3Studio code already contains several safety-oriented ideas: safe Z heights, spindle/coolant shutdown commands, tool-change handling, simulation, stock removal, collision checking, and explicit machine/controller post processors.

When changing toolpath generation or post processing, verify at least:

- units and coordinate mode
- stock zero and work coordinate system
- safe Z and retract behavior before rapid XY moves
- spindle and coolant startup/shutdown
- tool change commands
- arc formatting and plane selection
- controller-specific line endings, comments, program headers, and endings
