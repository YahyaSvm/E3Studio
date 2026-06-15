# Contributing to E3Studio

Thank you for your interest in contributing to E3Studio! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Reporting Bugs](#reporting-bugs)
- [Suggesting Features](#suggesting-features)

## Code of Conduct

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## How Can I Contribute?

### Reporting Bugs

Before creating a bug report, please check the [existing issues](https://github.com/yahyasvm/E3Studio/issues) to avoid duplicates. When creating a bug report, include:

- **Clear title** — Summarize the issue
- **Steps to reproduce** — Be as specific as possible
- **Expected behavior** — What should happen
- **Actual behavior** — What actually happens
- **Environment** — OS, version, build type
- **Logs** — Attach relevant log files
- **Screenshots** — If applicable

### Suggesting Features

Feature suggestions are welcome! Please:

- Check if the feature is already planned in the [Roadmap](README.md#roadmap)
- Open an issue with the `enhancement` label
- Describe the use case and expected behavior
- Consider cross-platform implications

### Code Contributions

We accept contributions in these areas:

- **C++ Backend** — Core engine, geometry, toolpath, simulation
- **React/Three.js UI** — Web interface components
- **C# WPF Client** — Windows desktop client
- **Post Processors** — New CNC controller support
- **Documentation** — Guides, tutorials, API docs
- **Testing** — Unit tests, integration tests
- **Translations** — UI localization

## Development Setup

### Prerequisites

See [Building from Source](README.md#building-from-source) in the README for platform-specific requirements.

### Quick Setup

```bash
# Clone
git clone https://github.com/yahyasvm/E3Studio.git
cd E3Studio

# Build (Windows)
.\build.ps1

# Build (Linux/macOS)
chmod +x build.sh && ./build.sh

# Launch
.\launch.ps1          # Windows
./launch.sh --dev     # Linux/macOS (with hot reload)
```

### Project Structure

```
src/          → C++20 backend modules
  core/       → Application, Logger, EventBus, ThreadPool
  geometry/   → OpenCASCADE geometry kernel
  toolpath/   → Toolpath generation
  simulation/ → Simulation engine
  api/        → WebSocket API server

ui/           → React/Three.js web UI
  src/components/ → React components
  src/store/      → Zustand state management
  src/lib/        → Utilities, WebSocket client

CAM/          → C# CAM engines (WPF)
Services/     → C# services (WPF)
Models/       → C# data models (WPF)
```

## Coding Standards

### C++ (Backend)

- **Standard**: C++20
- **Style**: Follow existing code style (4-space indent, Allman braces)
- **Naming**: `camelCase` for variables/functions, `PascalCase` for classes/types
- **Headers**: Use `#pragma once`
- **Includes**: Order: module header, C++ standard, third-party, project
- **Memory**: Prefer RAII, smart pointers, and value semantics
- **Error Handling**: Use return codes and optional; avoid exceptions in core
- **Logging**: Use `E3_LOG_INFO`, `E3_LOG_WARN`, `E3_LOG_ERROR` macros
- **Documentation**: Doxygen-style comments for public APIs

```cpp
/// @brief Calculates toolpath offset for profile cutting
/// @param geometry Input geometry to offset
/// @param toolRadius Tool radius in mm
/// @return Offset path, or empty on failure
std::optional<Path2D> calculateOffset(
    const Geometry& geometry,
    double toolRadius
);
```

### TypeScript / React (Web UI)

- **Style**: 2-space indent, single quotes, semicolons required
- **Components**: Functional components with hooks
- **State**: Zustand for global state, React state for local
- **Types**: Strict TypeScript, no `any`
- **Naming**: `PascalCase` for components/types, `camelCase` for functions/variables
- **CSS**: Tailwind utility classes; custom CSS in `index.css`

```tsx
interface ToolpathPanelProps {
  toolpaths: Toolpath[];
  onSelect: (id: string) => void;
}

export const ToolpathPanel: React.FC<ToolpathPanelProps> = ({
  toolpaths,
  onSelect,
}) => {
  // ...
};
```

### C# (WPF Client)

- **Framework**: .NET 10
- **Style**: Follow .NET naming conventions
- **Documentation**: XML documentation for all public APIs
- **Naming**: `PascalCase` for public members, `_camelCase` for private fields

## Pull Request Process

1. **Fork** the repository
2. **Branch** from `develop`
   ```bash
   git checkout -b feature/your-feature develop
   ```
3. **Develop** your changes
   - Write clear commit messages
   - Keep commits focused and atomic
   - Update documentation as needed
4. **Test** your changes
   - Build on your platform
   - Run existing tests
   - Add tests for new functionality
5. **Submit** the PR
   - Fill in the PR template completely
   - Reference related issues
   - Add screenshots for UI changes
6. **Review** process
   - Address reviewer feedback
   - Keep the PR updated with `develop`
   - Be patient — reviews take time

### PR Checklist

- [ ] Code builds successfully on all platforms (or note which platforms)
- [ ] Tests pass (or new tests added)
- [ ] Documentation updated
- [ ] No unrelated changes included
- [ ] Commit messages are clear and descriptive
- [ ] UI changes include screenshots

## Reporting Bugs

Use the [Bug Report template](.github/ISSUE_TEMPLATE/bug_report.md) when creating issues.

## Suggesting Features

Use the [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.md) when creating issues.

---

Thank you for contributing to E3Studio!
