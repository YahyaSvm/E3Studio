<div align="center">

# E3Studio

### Professional Cross-Platform CNC CAM Software

**From 3D Model to G-Code — Complete CAM Workflow**

[![Build Status](https://github.com/yahyasvm/E3Studio/actions/workflows/build.yml/badge.svg)](https://github.com/yahyasvm/E3Studio/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)](#platform-support)
[![C++](https://img.shields.io/badge/C%2B%2B-20-00599C?logo=c%2B%2B)](https://en.cppreference.com/w/cpp/20)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB?logo=react)](https://react.dev/)
[![Three.js](https://img.shields.io/badge/Three.js-r163-black?logo=three.js)](https://threejs.org/)
[![Version](https://img.shields.io/badge/version-0.1.0-orange)](CHANGELOG.md)

[Installation](#installation) • [Building](#building-from-source) • [Documentation](#documentation) • [Contributing](#contributing) • [License](#license)

</div>

---

## Overview

E3Studio is a professional, open-source Computer-Aided Manufacturing (CAM) application that provides a complete workflow from 3D model import to G-Code generation. Built with a modern hybrid architecture combining C++20 backend performance with a React/Three.js web-based UI and an optional WPF desktop client for Windows.

### Key Highlights

- **Cross-Platform**: Native support for Windows, Linux, and macOS
- **Hybrid Architecture**: C++20 backend + React/Three.js frontend + optional WPF client
- **Real-Time Simulation**: Animated toolpath visualization with stock removal tracking
- **Multi-Controller Support**: GRBL, Klipper, Mach3/4, LinuxCNC, Fanuc, Haas, Heidenhain, Sinumerik
- **Professional CAM**: Profile, pocket, drill, V-carve toolpaths with tab support
- **3D Import**: STL, STEP/IGES, 3MF with OpenCASCADE geometry kernel
- **2D Import**: DXF, SVG, PDF, Gerber files
- **Extensible**: Plugin-ready architecture with WebSocket API

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        E3Studio Architecture                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────┐    ┌──────────────────────────────────┐   │
│  │   React/Three.js │    │      WPF Client (Windows)        │   │
│  │   Web UI         │    │      .NET 10 / HelixToolkit      │   │
│  │   Vite + Tailwind│    │      AvalonDock                   │   │
│  └────────┬─────────┘    └──────────────┬───────────────────┘   │
│           │ WebSocket                    │ Direct                 │
│           │ ws://localhost:9001          │                        │
│  ┌────────▼─────────────────────────────▼───────────────────┐   │
│  │                  C++20 Backend Core                        │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐  │   │
│  │  │ Geometry │ │ Toolpath │ │Simulation│ │PostProcess │  │   │
│  │  │ OpenCASCADE│ │ Engine  │ │ Engine   │ │ Generator  │  │   │
│  │  └──────────┘ └──────────┘ └──────────┘ └────────────┘  │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐  │   │
│  │  │   Core   │ │  Machine │ │   API    │ │    AI      │  │   │
│  │  │App/Logger│ │ Control  │ │ WebSocket│ │  Module    │  │   │
│  │  └──────────┘ └──────────┘ └──────────┘ └────────────┘  │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Backend Core** | C++20, CMake 3.25+ | High-performance computation engine |
| **Geometry** | OpenCASCADE (OCCT) | BRep processing, STEP/IGES import |
| **Math** | Boost (system, filesystem, thread) | Cross-platform utilities |
| **API** | ixwebsocket | WebSocket server (JSON protocol) |
| **Logging** | spdlog | Fast structured logging |
| **Web UI** | React 18, TypeScript, Vite | Modern responsive interface |
| **3D Rendering** | Three.js, React Three Fiber | WebGL-based 3D viewport |
| **State** | Zustand, Immer | Lightweight state management |
| **Styling** | Tailwind CSS | Utility-first CSS framework |
| **Desktop (Win)** | .NET 10, WPF | Native Windows desktop client |
| **3D (Win)** | HelixToolkit.Wpf | WPF 3D viewport |
| **Docking (Win)** | AvalonDock | Dockable panel system |
| **Serial (Win)** | System.IO.Ports | CNC machine communication |

---

## Platform Support

| Platform | Backend (C++) | Web UI (React) | Desktop (WPF) | Status |
|----------|:---:|:---:|:---:|--------|
| **Windows 10/11** (x64) | ✅ | ✅ | ✅ | Full support |
| **Ubuntu 22.04+** (x64) | ✅ | ✅ | — | Full support (Web UI) |
| **Fedora 38+** (x64) | ✅ | ✅ | — | Full support (Web UI) |
| **Arch Linux** (x64) | ✅ | ✅ | — | Full support (Web UI) |
| **macOS 13+** (Apple Silicon) | ✅ | ✅ | — | Full support (Web UI) |
| **macOS 13+** (Intel) | ✅ | ✅ | — | Full support (Web UI) |
| **Docker** (Linux) | ✅ | ✅ | — | Containerized |

> **Note**: The WPF desktop client is Windows-only. On Linux/macOS, use the React/Three.js web UI which provides the same functionality through a browser.

---

## Features

### Import & Design
- **3D Model Import**: STL, STEP, IGES, 3MF with full mesh/BRep visualization
- **2D Vector Import**: DXF, SVG, PDF vector graphics
- **PCB Import**: Gerber (RS-274X) files for PCB milling
- **G-Code Backplot**: Import and visualize existing G-Code programs

### CAM Operations
- **Profile Toolpath**: Cut around geometry with tab support and lead-in/out
- **Pocket Toolpath**: Clear interior regions with step-over control
- **Drill Toolpath**: Point-to-point drilling operations
- **V-Carve**: Variable-depth engraving from vector geometry
- **Nesting**: Automatic part nesting for material optimization
- **Multi-Selection**: Batch toolpath generation from multiple geometries

### 3D Visualization
- **Real-time 3D Viewport**: Three.js (Web) / HelixToolkit (WPF) rendering
- **Stock Removal Simulation**: Visual material removal during simulation
- **Toolpath Preview**: Color-coded rapid/feed/plunge moves
- **Model Display**: Import and position 3D models on stock

### Simulation
- **Real-time G-Code Simulation**: Animated tool movement
- **Stock Removal**: Visual material removal tracking
- **Speed Control**: 0.25x to 10x playback speed
- **Step Control**: Frame-by-frame stepping
- **Collision Detection**: Tool-holder collision checking

### Post Processing
- **Built-in Controllers**: GRBL, Klipper, Mach3/4, LinuxCNC, Fanuc, Haas, Heidenhain, Sinumerik
- **Custom Post Processors**: Create and configure your own
- **G-Code Format Control**: Line numbers, G54, G90/G91, metric/imperial
- **Tool Change Support**: M6 commands with spindle warmup
- **Coolant Control**: M7/M8/M9 support

### Tool & Material Management
- **Tool Library**: Comprehensive tool database with presets
- **Tool Types**: End mills, ball nose, V-bits, drills, engravers
- **Material Library**: Pre-configured cutting parameters
- **Feed/Speed Calculator**: Automatic parameter calculation

### User Interface
- **Fusion 360-Inspired**: Modern, professional dark theme
- **Icon Rail Navigation**: Quick access to core functions
- **Context Toolbar**: Dynamic tools based on selection
- **Dockable Panels**: Customizable workspace layout (WPF)
- **Responsive Design**: Works on desktop and tablet browsers (Web UI)

### Project Management
- **Project Files**: Save/load complete project state
- **Recent Projects**: Quick access to recent work
- **Autosave**: Automatic backup with dirty state tracking
- **Undo/Redo**: Full operation history

---

## Installation

### Pre-built Binaries

Download the latest release for your platform from the [Releases page](https://github.com/yahyasvm/E3Studio/releases).

#### Windows
1. Download `E3Studio-0.1.0-win-x64.zip`
2. Extract to your preferred location
3. Run `E3Studio.exe`

#### Linux
```bash
# Download and extract
wget https://github.com/yahyasvm/E3Studio/releases/download/v0.1.0/E3Studio-0.1.0-linux-x64.tar.gz
tar -xzf E3Studio-0.1.0-linux-x64.tar.gz
cd E3Studio

# Run backend
./E3Studio

# Open browser to http://localhost:3000
```

#### macOS
```bash
# Download and extract
curl -LO https://github.com/yahyasvm/E3Studio/releases/download/v0.1.0/E3Studio-0.1.0-macos-arm64.tar.gz
tar -xzf E3Studio-0.1.0-macos-arm64.tar.gz
cd E3Studio

# Run backend
./E3Studio

# Open browser to http://localhost:3000
```

### Docker

```bash
# Pull and run
docker pull yahyasvm/e3studio:latest
docker run -p 3000:3000 -p 9001:9001 yahyasvm/e3studio

# Open browser to http://localhost:3000
```

---

## Building from Source

### Prerequisites

All platforms require:
- **CMake** 3.25 or later
- **C++20** compatible compiler (MSVC 2022, GCC 12+, Clang 15+)
- **Node.js** 18+ and npm
- **vcpkg** (auto-bootstrapped by build scripts)

Platform-specific:
| Platform | Additional Requirements |
|----------|------------------------|
| **Windows** | Visual Studio 2022, Windows 10/11 SDK, .NET 10 SDK (optional, for WPF client) |
| **Linux** | GCC 12+ or Clang 15+, X11/OpenGL dev libs, libglu1-mesa-dev |
| **macOS** | Xcode 15+, Command Line Tools |

### Windows

```powershell
# Clone the repository
git clone https://github.com/yahyasvm/E3Studio.git
cd E3Studio

# Build everything (C++ backend + Web UI)
.\build.ps1

# Build release version
.\build.ps1 -Release

# Build without UI (backend only)
.\build.ps1 -NoBuildUI

# Launch the application
.\launch.ps1

# Launch with dev UI (hot reload)
.\launch.ps1 -DevUI
```

### Linux

```bash
# Clone the repository
git clone https://github.com/yahyasvm/E3Studio.git
cd E3Studio

# Install system dependencies (Ubuntu/Debian)
sudo apt-get update
sudo apt-get install -y build-essential cmake git ninja-build \
    libx11-dev libxrandr-dev libxinerama-dev libxcursor-dev libxi-dev \
    libgl1-mesa-dev libglu1-mesa-dev mesa-common-dev \
    libfontconfig1-dev libssl-dev

# Install system dependencies (Fedora)
sudo dnf install -y gcc-c++ cmake git ninja-build \
    libX11-devel libXrandr-devel libXinerama-devel libXcursor-devel libXi-devel \
    mesa-libGL-devel mesa-libGLU-devel \
    fontconfig-devel openssl-devel

# Install system dependencies (Arch)
sudo pacman -S base-devel cmake git ninja \
    libx11 libxrandr libxinerama libxcursor libxi \
    mesa glu fontconfig openssl

# Build everything
chmod +x build.sh
./build.sh

# Build release version
./build.sh --release

# Launch the application
./launch.sh

# Launch with dev UI (hot reload)
./launch.sh --dev
```

### macOS

```bash
# Clone the repository
git clone https://github.com/yahyasvm/E3Studio.git
cd E3Studio

# Install dependencies via Homebrew
brew install cmake ninja node

# Build everything
chmod +x build.sh
./build.sh

# Build release version
./build.sh --release

# Launch the application
./launch.sh

# Launch with dev UI (hot reload)
./launch.sh --dev
```

### Manual Build (Advanced)

```bash
# 1. Bootstrap vcpkg
git clone https://github.com/microsoft/vcpkg.git
./vcpkg/bootstrap-vcpkg.sh -disableMetrics  # Linux/macOS
# .\vcpkg\bootstrap-vcpkg.bat -disableMetrics  # Windows

# 2. Configure CMake
cmake -B build -S . \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_TOOLCHAIN_FILE=vcpkg/scripts/buildsystems/vcpkg.cmake \
  -G Ninja

# 3. Build C++ backend
cmake --build build --config Release --parallel

# 4. Build Web UI
cd ui
npm install
npm run build
cd ..

# 5. (Optional) Build WPF client (Windows only)
dotnet build -c Release
```

---

## Usage

### Quick Start

1. **Create New Project**
   - Click "New Project" on welcome screen
   - Configure stock dimensions (Width x Height x Thickness)
   - Select material type

2. **Import Geometry**
   - Use the Import button on the icon rail
   - Select file type: STL, STEP, DXF, SVG, PDF, Gerber, 3MF
   - Geometry is automatically centered on stock

3. **Create Toolpaths**
   - Select geometry paths in viewport
   - Click Profile, Pocket, Drill, or V-Carve button
   - Configure tool and cutting parameters
   - Click "Generate" to create toolpath

4. **Simulate**
   - Click "SIMULATE" button
   - Watch real-time tool movement
   - Observe stock removal
   - Adjust speed as needed

5. **Export G-Code**
   - Select post processor (GRBL, Klipper, etc.)
   - Click "EXPORT" button
   - Save to file for your CNC controller

### Keyboard Shortcuts

| Shortcut | Action | | Shortcut | Action |
|----------|--------|-|----------|--------|
| `Ctrl+N` | New Project | | `Ctrl+G` | Generate G-Code |
| `Ctrl+O` | Open Project | | `Delete` | Delete Selection |
| `Ctrl+S` | Save Project | | `S` | Select Tool |
| `Ctrl+Z` | Undo | | `M` | Move Tool |
| `Ctrl+Y` | Redo | | `F` | Fit View |
| `Ctrl+C` | Copy | | `Space` | Play/Pause Simulation |
| `Ctrl+X` | Cut | | `1-5` | View Presets |
| `Ctrl+V` | Paste | | `Tab` | Toggle Panels |
| `Ctrl+D` | Duplicate | | `Esc` | Deselect All |
| `Ctrl+A` | Select All | | `?` | Show Shortcuts |

---

## Post Processor Reference

| Controller | Extension | Line Numbers | Tool Change | Coolant | G54 | Notes |
|------------|-----------|:---:|:---:|:---:|:---:|-------|
| **GRBL** | `.gcode` | No | No | No | No | Hobby CNC, Arduino-based |
| **Klipper** | `.gcode` | No | No | No | No | 3D printer CNC conversions |
| **Mach3/4** | `.tap` | Yes | Yes | Yes | Yes | Popular hobby/pro controller |
| **LinuxCNC** | `.ngc` | Yes | Yes | Yes | Yes | Open-source industrial |
| **Fanuc** | `.nc` | Yes | Yes | Yes | Yes | Industrial standard |
| **Haas** | `.nc` | Yes | Yes | Yes | Yes | Haas-specific codes |
| **Heidenhain** | `.h` | Yes | Yes | Yes | Yes | Heidenhain TNC format |
| **Sinumerik** | `.mpf` | Yes | Yes | Yes | Yes | Siemens Sinumerik |

---

## Project Structure

```
E3Studio/
├── CMakeLists.txt              # Root CMake configuration
├── vcpkg.json                  # C++ dependency manifest
├── build.ps1                   # Windows build script
├── build.sh                    # Linux/macOS build script
├── launch.ps1                  # Windows launch script
├── launch.sh                   # Linux/macOS launch script
├── Dockerfile                  # Multi-platform Docker image
├── docker-compose.yml          # Docker Compose configuration
│
├── src/                        # C++20 Backend Source
│   ├── main.cpp                # Application entry point
│   ├── core/                   # Core: App, Logger, EventBus, ThreadPool
│   ├── geometry/               # OpenCASCADE geometry kernel
│   ├── toolpath/               # Toolpath generation engine
│   ├── machine/                # Machine control & serial
│   ├── simulation/             # Simulation engine
│   ├── postprocessor/          # G-Code post processors
│   ├── ai/                     # AI/ML module
│   └── api/                    # WebSocket API server
│
├── ui/                         # React/Three.js Web UI
│   ├── package.json
│   ├── vite.config.ts
│   ├── tailwind.config.js
│   └── src/
│       ├── App.tsx
│       ├── main.tsx
│       ├── components/         # React components
│       ├── store/              # Zustand state management
│       └── lib/                # Utilities & WebSocket client
│
├── CAM/                        # C# CAM Engines (WPF client)
│   ├── ToolpathEngine.cs
│   ├── CollisionSimulator.cs
│   ├── VCarveEngine.cs
│   ├── NestingEngine.cs
│   └── ...
│
├── Services/                   # C# Services (WPF client)
│   ├── StlImporter.cs
│   ├── DxfImporter.cs
│   ├── SvgImporter.cs
│   ├── GCodeGenerator.cs
│   ├── PostProcessor/
│   └── ...
│
├── Models/                     # C# Data Models (WPF client)
├── Views/                      # C# UI Views (WPF client)
├── Controls/                   # C# Custom Controls (WPF client)
├── Dialogs/                    # C# Dialog Windows (WPF client)
├── Resources/                  # UI resources & themes
│
├── .github/                    # GitHub configuration
│   ├── workflows/
│   │   ├── build.yml           # CI/CD: Multi-platform build
│   │   └── release.yml         # Automated releases
│   ├── ISSUE_TEMPLATE/
│   └── PULL_REQUEST_TEMPLATE.md
│
└── docs/                       # Documentation
    ├── ARCHITECTURE.md
    ├── API.md
    └── POST_PROCESSORS.md
```

---

## Documentation

- **[Architecture](docs/ARCHITECTURE.md)** — Detailed system architecture and module descriptions
- **[API Reference](docs/API.md)** — WebSocket API protocol documentation
- **[Post Processors](docs/POST_PROCESSORS.md)** — Post processor configuration guide
- **[Development Guide](CONTRIBUTING.md)** — How to contribute to E3Studio

---

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow existing code style and naming conventions
- C++: Use modern C++20 features, prefer RAII and smart pointers
- TypeScript: Strict mode, functional components, hooks
- C#: Follow .NET naming conventions, XML documentation for public APIs
- Add tests for new functionality
- Update documentation as needed

---

## Roadmap

### v0.2.0 (Q3 2026)
- [ ] 3D model editing (scale, rotate, position in viewport)
- [ ] Nesting algorithm for material optimization
- [ ] Toolpath optimization (TSP solver)
- [ ] Machine connection via serial/USB (all platforms)

### v0.3.0 (Q4 2026)
- [ ] Real-time machine control (DRO)
- [ ] Probe integration
- [ ] Multi-axis support (4th axis)
- [ ] Plugin system for custom operations

### v1.0.0 (Q1 2027)
- [ ] Native macOS app (SwiftUI wrapper)
- [ ] Native Linux app (GTK4/Qt wrapper)
- [ ] Cloud sync for projects
- [ ] Marketplace for post processors and plugins

---

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments

- **[OpenCASCADE](https://dev.opencascade.org/)** — Geometry kernel (BRep, STEP, IGES)
- **[Three.js](https://threejs.org/)** — WebGL 3D rendering
- **[React Three Fiber](https://docs.pmnd.rs/react-three-fiber)** — React renderer for Three.js
- **[HelixToolkit](https://helix-toolkit.org/)** — WPF 3D viewport
- **[AvalonDock](https://github.com/Dirkster99/AvalonDock)** — Dockable panel system
- **[spdlog](https://github.com/gabime/spdlog)** — Fast C++ logging
- **[ixwebsocket](https://github.com/machinezone/IXWebSocket)** — WebSocket library
- **[Material Design Icons](https://materialdesignicons.com/)** — Icon set
- **[Fusion 360](https://www.autodesk.com/products/fusion-360/)** — UI inspiration

---

## Disclaimer

This software is provided "as-is" without warranty. Always verify G-Code output before running on your CNC machine. The authors are not responsible for any damage or injury resulting from the use of this software.

---

<div align="center">

**Made with dedication by [yahyasvm](https://github.com/yahyasvm)**

If you find this project useful, consider giving it a star!

</div>
