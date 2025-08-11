# Simple build script for Squish - builds for the current platform only
# This is a fast build option for local development and testing.

$ErrorActionPreference = "Stop"

Write-Host "🔨 Building Squish for current platform..." -ForegroundColor Green
Write-Host ""

# Clean previous builds
if (Test-Path "Squish.Console/bin") {
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force "Squish.Console/bin", "Squish.Console/obj"
}

# Build for current platform
Write-Host "⚡ Building optimized release..." -ForegroundColor Cyan
dotnet build Squish.Console/Squish.Console.csproj -c Release

Write-Host ""
Write-Host "✅ Build complete!" -ForegroundColor Green
Write-Host "📂 Executable location: Squish.Console/bin/Release/net9.0/squish" -ForegroundColor White
Write-Host ""
Write-Host "🚀 Ready to run locally!" -ForegroundColor Green