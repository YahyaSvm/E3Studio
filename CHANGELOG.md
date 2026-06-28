# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.2] — 2026-06-29

### Added
- Web UI tool, simulation, and G-Code export panels
- C++ Drilling and Threading operations; full operation engine wiring
- Tool WebSocket API (`tool.add`, `tool.list`, `tool.update`, `tool.remove`)
- xUnit test project for CAM engines
- Project serialization for models and machine configuration

### Fixed
- GitHub Actions Windows release packaging path (`build/bin/Release`)
- CI test command now runs `tests/E3Studio.Tests`
- Disabled macOS Intel CI job blocked by runner queue timeouts
- Docker release job skips when `DOCKERHUB_TOKEN` is not configured
- WPF CAM toolbar integrations (V-Carve, nesting, tabs, optimize, collision)
- Post-processor and tool library JSON import/export

### Changed
- Docker container starts backend and serves the built web UI

---

## [0.1.0] — 2026-06-15

### Added

#### Core Architecture
- C++20 backend with CMake build system
- WebSocket API server (ws://localhost:9001)
- Event bus system for inter-module communication
- Thread pool for parallel computation
- Structured logging with spdlog
- Project management with save/load/autosave

#### Geometry Engine
- OpenCASCADE (OCCT) geometry kernel integration
- STL file import with mesh visualization
- STEP/IGES import via BRep processor
- 3MF file import support
- Mesh loading and processing utilities

#### Toolpath Engine
- Profile toolpath generation with tab support
- Pocket toolpath with step-over control
- Drill toolpath (point-to-point)
- V-Carve engine for variable-depth engraving
- Lead-in/lead-out path generation
- Toolpath optimizer (TSP-based)
- Arc fitting for smooth curves
- Collision simulation and detection

#### CAM Operations
- Multi-selection for batch operations
- Nesting engine for material optimization
- Stock removal simulation
- Real-time simulation with speed control
- Stock setup dialog with dimension/material configuration

#### Simulation
- Real-time G-Code simulation with animated tool movement
- Stock removal visualization
- Speed control (0.25x to 10x)
- Step-by-step frame control
- Toolpath preview with color-coded moves (rapid/feed/plunge)

#### Post Processors
- GRBL post processor
- Klipper post processor
- Mach3/4 post processor
- LinuxCNC post processor
- Fanuc post processor
- Haas post processor
- Heidenhain post processor
- Sinumerik post processor
- Custom post processor configuration
- G-Code format control (line numbers, G54, G90/G91, metric/imperial)
- Tool change support (M6 with spindle warmup)
- Coolant control (M7/M8/M9)

#### File Import
- STL importer (binary and ASCII)
- DXF importer (entities: LINE, ARC, CIRCLE, POLYLINE, LWPOLYLINE, SPLINE)
- SVG importer (path, rect, circle, ellipse, polygon, polyline)
- PDF vector importer
- Gerber (RS-274X) importer for PCB milling
- G-Code backplot importer

#### Tool & Material Management
- Tool library with comprehensive database
- Tool types: End mill, ball nose, V-bit, drill, engraver
- Tool parameters: diameter, flutes, flute length, total length
- Material library with pre-configured cutting parameters
- Material types: Wood, plastic, aluminum, steel, brass
- Feed/speed calculator with automatic parameter calculation

#### WPF Desktop Client (Windows)
- Fusion 360-inspired dark theme UI
- HelixToolkit-powered 3D viewport
- AvalonDock dockable panel system
- Icon rail navigation
- Context toolbar with dynamic tools
- Properties panel with real-time transform editing
- Full keyboard shortcut support
- Undo/redo system
- Clipboard manager
- Theme service with dark/light toggle

#### Web UI (Cross-Platform)
- React 18 with TypeScript
- Three.js / React Three Fiber 3D viewport
- Zustand state management with Immer
- Tailwind CSS styling
- Vite build system
- Responsive design for desktop and tablet
- WebSocket client for backend communication

#### Project Management
- Project file save/load
- Recent projects list
- Autosave with dirty state tracking
- Unsaved changes warning
- Project serialization

#### Build System
- CMake 3.25+ build configuration
- vcpkg dependency management
- Windows build script (PowerShell)
- Linux/macOS build script (Bash)
- Windows launch script
- Linux/macOS launch script
- Debug and Release build configurations

---

## Version History

| Version | Date | Description |
|---------|------|-------------|
| 0.1.0 | 2026-06-15 | Initial release — Core CAM functionality |

---

[Unreleased]: https://github.com/yahyasvm/E3Studio/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/yahyasvm/E3Studio/releases/tag/v0.1.0
