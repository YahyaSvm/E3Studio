# E3Studio Build & Launch Script
# Kullanım: .\build.ps1 [-Release] [-NoBuildUI]

param(
    [switch]$Release,
    [switch]$NoBuildUI
)

$BuildType = if ($Release) { "Release" } else { "Debug" }
$Root = $PSScriptRoot

Write-Host "`n=== E3Studio Build ($BuildType) ===" -ForegroundColor Cyan

# ── vcpkg kurulum kontrolü
if (-not (Test-Path "$Root/vcpkg")) {
    Write-Host "[1/5] vcpkg klonlanıyor..." -ForegroundColor Yellow
    git clone https://github.com/microsoft/vcpkg.git "$Root/vcpkg"
    & "$Root/vcpkg/bootstrap-vcpkg.bat" -disableMetrics
}

# ── CMake yapılandır
Write-Host "[2/5] CMake yapılandırılıyor ($BuildType)..." -ForegroundColor Yellow
$BuildDir = "$Root/build/$BuildType"
cmake -B $BuildDir -S $Root `
      -DCMAKE_BUILD_TYPE=$BuildType `
      -DCMAKE_TOOLCHAIN_FILE="$Root/vcpkg/scripts/buildsystems/vcpkg.cmake" `
      -DVCPKG_TARGET_TRIPLET="x64-windows" `
      -G "Visual Studio 17 2022" -A x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "[HATA] CMake yapılandırması başarısız" -ForegroundColor Red
    exit 1
}

# ── C++ derleme
Write-Host "[3/5] C++ derleniyor..." -ForegroundColor Yellow
cmake --build $BuildDir --config $BuildType --parallel

if ($LASTEXITCODE -ne 0) {
    Write-Host "[HATA] C++ derleme başarısız" -ForegroundColor Red
    exit 1
}

# ── UI derleme
if (-not $NoBuildUI) {
    Write-Host "[4/5] UI bağımlılıkları yükleniyor..." -ForegroundColor Yellow
    Push-Location "$Root/ui"
    npm install
    Write-Host "[5/5] UI derleniyor..." -ForegroundColor Yellow
    npm run build
    Pop-Location
}

Write-Host "`n✓ Build başarılı! Çalıştırmak için: .\launch.ps1" -ForegroundColor Green
