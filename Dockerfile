# ==============================================================================
# E3Studio Dockerfile
# Multi-stage build for cross-platform CNC CAM software
# ==============================================================================

# ─── Stage 1: Build C++ Backend ──────────────────────────────────────────────
FROM ubuntu:22.04 AS cpp-builder

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    build-essential cmake git ninja-build curl zip unzip tar \
    libx11-dev libxrandr-dev libxinerama-dev libxcursor-dev libxi-dev \
    libgl1-mesa-dev libglu1-mesa-dev mesa-common-dev \
    libfontconfig1-dev libssl-dev pkg-config \
    python3 python3-pip \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Clone vcpkg
RUN git clone https://github.com/microsoft/vcpkg.git /app/vcpkg \
    && /app/vcpkg/bootstrap-vcpkg.sh -disableMetrics

# Copy dependency manifest first (cache layer)
COPY vcpkg.json /app/

# Copy source
COPY CMakeLists.txt /app/
COPY src/ /app/src/

# Configure and build
RUN cmake -B build -S . \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE=/app/vcpkg/scripts/buildsystems/vcpkg.cmake \
    -DVCPKG_TARGET_TRIPLET=x64-linux \
    -G Ninja \
    && cmake --build build --config Release --parallel $(nproc)

# ─── Stage 2: Build Web UI ───────────────────────────────────────────────────
FROM node:20-slim AS ui-builder

WORKDIR /app/ui

# Copy dependency manifest first (cache layer)
COPY ui/package.json ui/package-lock.json ./

RUN npm ci

# Copy UI source
COPY ui/ ./

RUN npm run build

# ─── Stage 3: Runtime ────────────────────────────────────────────────────────
FROM ubuntu:22.04 AS runtime

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    libgl1-mesa1 libglu1-mesa \
    libfontconfig1 libssl3 \
    nodejs npm \
    && rm -rf /var/lib/apt/lists/*

# Install serve for static file hosting
RUN npm install -g serve

WORKDIR /app

# Copy backend binary
COPY --from=cpp-builder /app/build/bin/E3Studio /app/E3Studio
RUN chmod +x /app/E3Studio

# Copy built UI
COPY --from=ui-builder /app/ui/dist /app/ui/dist

# Copy launch script
COPY launch.sh /app/launch.sh
RUN chmod +x /app/launch.sh

# Expose ports
EXPOSE 3000 9001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:3000 || exit 1

# Run
CMD ["/app/E3Studio"]
