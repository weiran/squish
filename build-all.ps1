# Build script for Squish - creates self-contained executables for specified platforms
# This script builds the Squish console application with embedded .NET runtime
# so users don't need to install .NET separately.
#
# Usage:
#   .\build-all.ps1                   # Build for all platforms
#   .\build-all.ps1 win               # Build for Windows only
#   .\build-all.ps1 macos             # Build for macOS (both x64 and ARM64)
#   .\build-all.ps1 linux             # Build for Linux only
#   .\build-all.ps1 win,macos         # Build for Windows and macOS
#   .\build-all.ps1 win-x64           # Build for Windows x64 specifically
#   .\build-all.ps1 osx-x64           # Build for macOS x64 specifically
#   .\build-all.ps1 osx-arm64         # Build for macOS ARM64 specifically
#   .\build-all.ps1 linux-x64         # Build for Linux x64 specifically

param(
    [string[]]$Platforms = @()
)

$ErrorActionPreference = "Stop"

# Parse platform arguments
$BuildPlatforms = @()
if ($Platforms.Count -eq 0) {
    # Default: build for all platforms
    $BuildPlatforms = @("win-x64", "osx-x64", "osx-arm64", "linux-x64")
    Write-Host "🔨 Building Squish for all platforms..." -ForegroundColor Green
} else {
    # Parse platform arguments
    foreach ($arg in $Platforms) {
        switch ($arg) {
            { $_ -in @("win", "windows") } { $BuildPlatforms += "win-x64" }
            { $_ -in @("macos", "osx") } { $BuildPlatforms += @("osx-x64", "osx-arm64") }
            "linux" { $BuildPlatforms += "linux-x64" }
            { $_ -in @("win-x64", "osx-x64", "osx-arm64", "linux-x64") } { $BuildPlatforms += $_ }
            default {
                Write-Host "❌ Unknown platform: $arg" -ForegroundColor Red
                Write-Host "Valid platforms: win, macos, linux, win-x64, osx-x64, osx-arm64, linux-x64" -ForegroundColor White
                exit 1
            }
        }
    }
    Write-Host "🔨 Building Squish for platforms: $($BuildPlatforms -join ', ')" -ForegroundColor Green
}
Write-Host ""

# Clean previous builds
if (Test-Path "publish") {
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force "publish"
}

# Create publish directory
New-Item -ItemType Directory -Path "publish" -Force | Out-Null

# Common publish arguments
$CommonArgs = @("-c", "Release", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:PublishTrimmed=true")
$Project = "Squish.Console/Squish.Console.csproj"

# Function to get platform display info
function Get-PlatformInfo {
    param($Platform)
    switch ($Platform) {
        "win-x64" { return @{ Icon = "🪟"; Description = "Windows x64" } }
        "osx-x64" { return @{ Icon = "🍎"; Description = "macOS x64 (Intel)" } }
        "osx-arm64" { return @{ Icon = "🍎"; Description = "macOS ARM64 (Apple Silicon)" } }
        "linux-x64" { return @{ Icon = "🐧"; Description = "Linux x64" } }
    }
}

# Build for each specified platform
foreach ($platform in $BuildPlatforms) {
    $info = Get-PlatformInfo $platform
    Write-Host "$($info.Icon) Building for $($info.Description)..." -ForegroundColor Cyan
    dotnet publish $Project @CommonArgs -r $platform -o "publish/$platform"
    Write-Host "✅ $($info.Description) build complete" -ForegroundColor Green
    Write-Host ""
}

# Display build results
Write-Host "🎉 All builds completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📂 Executables created in:" -ForegroundColor White

foreach ($platform in $BuildPlatforms) {
    $info = Get-PlatformInfo $platform
    $executablePath = if ($platform -eq "win-x64") { "publish/$platform/squish.exe" } else { "publish/$platform/squish" }
    if (Test-Path $executablePath) {
        Write-Host "   • $($info.Description): $executablePath" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "💡 These executables include the .NET runtime and can run without installing .NET" -ForegroundColor Yellow
Write-Host ""

# Show file sizes
Write-Host "📊 Executable sizes:" -ForegroundColor White

foreach ($platform in $BuildPlatforms) {
    $info = Get-PlatformInfo $platform
    $executablePath = if ($platform -eq "win-x64") { "publish/$platform/squish.exe" } else { "publish/$platform/squish" }
    
    if (Test-Path $executablePath) {
        $size = (Get-Item $executablePath).Length
        $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
        Write-Host "   $($info.Description): $sizeStr" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "🚀 Ready to distribute!" -ForegroundColor Green