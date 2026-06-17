# E3Studio Launch Script
# C++ backend'i başlatır ve tarayıcıda UI'ı açar

param(
    [switch]$Release,
    [switch]$DevUI  # Vite dev server ile çalıştır (hot reload)
)

$BuildType = if ($Release) { "Release" } else { "Debug" }
$Root = $PSScriptRoot

Write-Host "`n=== E3Studio Başlatılıyor ===" -ForegroundColor Cyan

# ── Backend exe yolu
$ExePath = "$Root/build/$BuildType/$BuildType/E3Studio.exe"
if (-not (Test-Path $ExePath)) {
    # Alternatif path (Generator bağımsız build)
    $ExePath = "$Root/build/$BuildType/E3Studio.exe"
}
if (-not (Test-Path $ExePath)) {
    Write-Host "[HATA] E3Studio.exe bulunamadı. Önce build.ps1 çalıştırın." -ForegroundColor Red
    exit 1
}

# ── C++ backend başlat
Write-Host "[1/3] Backend başlatılıyor: $ExePath" -ForegroundColor Yellow
$Backend = Start-Process -FilePath $ExePath -PassThru -NoNewWindow

Write-Host "[2/3] Backend PID: $($Backend.Id)" -ForegroundColor Green

# Backend'in hazır olmasını bekle (ws://localhost:9001)
Write-Host "      Backend'in başlaması bekleniyor..." -ForegroundColor Gray
Start-Sleep -Seconds 2

# ── UI başlat
if ($DevUI) {
    Write-Host "[3/3] Vite dev server başlatılıyor..." -ForegroundColor Yellow
    Push-Location "$Root/ui"
    Start-Process "cmd" -ArgumentList "/c npm run dev" -NoNewWindow
    Pop-Location
    Start-Sleep -Seconds 3
    Start-Process "http://localhost:3000"
} else {
    # Production: ui/dist klasörünü serve et
    Write-Host "[3/3] UI açılıyor (dist mod)..." -ForegroundColor Yellow
    # Basit dosya sunucu — Node gerektirir
    Push-Location "$Root"
    $ServeExists = Get-Command "npx" -ErrorAction SilentlyContinue
    if ($ServeExists) {
        Start-Process "cmd" -ArgumentList "/c npx serve ui/dist -p 3000 -s" -NoNewWindow
        Start-Sleep -Seconds 2
    }
    Pop-Location
    Start-Process "http://localhost:3000"
}

Write-Host "`nE3Studio çalışıyor!" -ForegroundColor Green
Write-Host "  Backend : ws://localhost:9001"
Write-Host "  UI      : http://localhost:3000"
Write-Host "`nKapatmak için bu pencereyi kapatın veya Ctrl+C"

# Backend kapanana kadar bekle
$Backend.WaitForExit()
