# Unified build script for Squish - creates self-contained executables
# This script builds the Squish console application with embedded .NET runtime
# so users don't need to install .NET separately.
#
# Usage:
#   .\build.ps1                       # Build for current platform only (default)
#   .\build.ps1 -All                  # Build for all supported platforms
#   .\build.ps1 -Dev                  # Fast development build (current platform, no publish)
#   .\build.ps1 win                   # Build for Windows (both x64 and ARM64)
#   .\build.ps1 macos                 # Build for macOS (both x64 and ARM64) 
#   .\build.ps1 linux                 # Build for Linux (both x64 and ARM64)
#   .\build.ps1 win,linux             # Build for multiple platform families
#   .\build.ps1 win-x64               # Build for Windows x64 specifically
#   .\build.ps1 osx-arm64             # Build for macOS ARM64 specifically
#   .\build.ps1 linux-arm64           # Build for Linux ARM64 specifically

param(
    [string[]]$Platforms = @(),
    [switch]$All,
    [switch]$Dev
)

$ErrorActionPreference = "Stop"

# Detect current platform
function Get-CurrentPlatform {
    $os = [System.Environment]::OSVersion.Platform
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    
    if ($os -eq "Win32NT") {
        switch ($arch) {
            "X64" { return "win-x64" }
            "Arm64" { return "win-arm64" }
            default { return "win-x64" }
        }
    } elseif ($os -eq "Unix") {
        if ($IsLinux) {
            switch ($arch) {
                "X64" { return "linux-x64" }
                "Arm64" { return "linux-arm64" }
                default { return "linux-x64" }
            }
        } elseif ($IsMacOS) {
            switch ($arch) {
                "X64" { return "osx-x64" }
                "Arm64" { return "osx-arm64" }
                default { return "osx-x64" }
            }
        }
    }
    return "win-x64"  # fallback
}

# All supported platforms (64-bit and ARM only)
$AllSupportedPlatforms = @("win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64")

# Parse platform arguments
$BuildPlatforms = @()

if ($All) {
    $BuildPlatforms = $AllSupportedPlatforms
    Write-Host "🔨 Building Squish for all supported platforms..." -ForegroundColor Green
} elseif ($Dev) {
    $CurrentPlatform = Get-CurrentPlatform
    $BuildPlatforms = @($CurrentPlatform)
    Write-Host "🔨 Fast development build for current platform: $CurrentPlatform" -ForegroundColor Green
} elseif ($Platforms.Count -eq 0) {
    # Default: build for current platform only
    $CurrentPlatform = Get-CurrentPlatform
    $BuildPlatforms = @($CurrentPlatform)
    Write-Host "🔨 Building Squish for current platform: $CurrentPlatform" -ForegroundColor Green
} else {
    # Parse platform arguments
    foreach ($arg in $Platforms) {
        switch ($arg) {
            { $_ -in @("win", "windows") } { $BuildPlatforms += @("win-x64", "win-arm64") }
            { $_ -in @("macos", "osx") } { $BuildPlatforms += @("osx-x64", "osx-arm64") }
            "linux" { $BuildPlatforms += @("linux-x64", "linux-arm64") }
            { $_ -in @("win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64") } { $BuildPlatforms += $_ }
            default {
                Write-Host "❌ Unknown platform: $arg" -ForegroundColor Red
                Write-Host "Valid platforms:" -ForegroundColor White
                Write-Host "  Platform families: win, macos, linux" -ForegroundColor White
                Write-Host "  Specific targets: win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64" -ForegroundColor White
                Write-Host "  Special options: -All, -Dev" -ForegroundColor White
                exit 1
            }
        }
    }
    
    if ($BuildPlatforms.Count -eq 0) {
        Write-Host "❌ No valid platforms specified" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "🔨 Building Squish for platforms: $($BuildPlatforms -join ', ')" -ForegroundColor Green
}
Write-Host ""

# Handle development vs publish builds
$Project = "Squish.Console/Squish.Console.csproj"

if ($Dev) {
    # Fast development build - just compile, no publish
    if ((Test-Path "Squish.Console/bin") -or (Test-Path "Squish.Console/obj")) {
        Write-Host "🧹 Cleaning previous development builds..." -ForegroundColor Yellow
        if (Test-Path "Squish.Console/bin") { Remove-Item -Recurse -Force "Squish.Console/bin" }
        if (Test-Path "Squish.Console/obj") { Remove-Item -Recurse -Force "Squish.Console/obj" }
        if (Test-Path "Squish.Core/bin") { Remove-Item -Recurse -Force "Squish.Core/bin" }
        if (Test-Path "Squish.Core/obj") { Remove-Item -Recurse -Force "Squish.Core/obj" }
    }
    
    Write-Host "⚡ Building optimized release (development)..." -ForegroundColor Cyan
    dotnet build $Project -c Release
    
    Write-Host ""
    Write-Host "✅ Development build complete!" -ForegroundColor Green
    Write-Host "📂 Executable location: Squish.Console/bin/Release/net9.0/squish" -ForegroundColor White
    Write-Host ""
    Write-Host "🚀 Ready to run locally!" -ForegroundColor Green
    exit 0
}

# Production publish builds
if (Test-Path "publish") {
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force "publish"
}

# Create publish directory
New-Item -ItemType Directory -Path "publish" -Force | Out-Null

# Common publish arguments
$CommonArgs = @("-c", "Release", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:PublishTrimmed=true")

# Function to get platform display info
function Get-PlatformInfo {
    param($Platform)
    switch ($Platform) {
        "win-x64" { return @{ Icon = "🪟"; Description = "Windows x64" } }
        "win-arm64" { return @{ Icon = "🪟"; Description = "Windows ARM64" } }
        "osx-x64" { return @{ Icon = "🍎"; Description = "macOS x64 (Intel)" } }
        "osx-arm64" { return @{ Icon = "🍎"; Description = "macOS ARM64 (Apple Silicon)" } }
        "linux-x64" { return @{ Icon = "🐧"; Description = "Linux x64" } }
        "linux-arm64" { return @{ Icon = "🐧"; Description = "Linux ARM64" } }
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
    $executablePath = if ($platform -like "win-*") { "publish/$platform/squish.exe" } else { "publish/$platform/squish" }
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
    $executablePath = if ($platform -like "win-*") { "publish/$platform/squish.exe" } else { "publish/$platform/squish" }
    
    if (Test-Path $executablePath) {
        $size = (Get-Item $executablePath).Length
        $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
        Write-Host "   $($info.Description): $sizeStr" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "🚀 Ready to distribute!" -ForegroundColor Green