# Unified build script for Squish - creates self-contained executables
# This script builds both the Squish console and UI applications with embedded .NET runtime
# so users don't need to install .NET separately.
#
# Usage:
#   .\build.ps1                        # Build for current platform only (default)
#   .\build.ps1 -All                   # Build for all supported platforms
#   .\build.ps1 -Dev                   # Fast development build (current platform, no publish)
#   .\build.ps1 win                    # Build for Windows (both x64 and ARM64)
#   .\build.ps1 macos                  # Build for macOS (both x64 and ARM64)
#   .\build.ps1 linux                  # Build for Linux (both x64 and ARM64)
#   .\build.ps1 win,linux              # Build for multiple platform families
#   .\build.ps1 win-x64                # Build for Windows x64 specifically
#   .\build.ps1 osx-arm64              # Build for macOS ARM64 specifically
#   .\build.ps1 linux-arm64            # Build for Linux ARM64 specifically

param(
    [string[]]$Platforms = @(),
    [switch]$All,
    [switch]$Dev
)

$ErrorActionPreference = "Stop"

# Detect current platform
function Get-CurrentPlatform {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    
    if ($IsWindows) {
        switch ($arch) {
            "X64" { return "win-x64" }
            "Arm64" { return "win-arm64" }
            default { return "win-x64" }
        }
    } elseif ($IsLinux) {
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
    return "win-x64"  # fallback
}

# All supported platforms (64-bit and ARM only)
$AllSupportedPlatforms = @("win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64")

# Parse command line arguments
$BuildPlatforms = @()

if ($Platforms.Count -eq 0 -and !$All -and !$Dev) {
    # Default: build for current platform only
    $CurrentPlatform = Get-CurrentPlatform
    $BuildPlatforms = @($CurrentPlatform)
    Write-Host "Building Squish for current platform: $CurrentPlatform" -ForegroundColor Green
} elseif ($All) {
    $BuildPlatforms = $AllSupportedPlatforms
    Write-Host "Building Squish for all supported platforms..." -ForegroundColor Green
} elseif ($Dev) {
    $CurrentPlatform = Get-CurrentPlatform
    $BuildPlatforms = @($CurrentPlatform)
    Write-Host " Fast development build for current platform: $CurrentPlatform" -ForegroundColor Green
} else {
    # Parse arguments - handle comma-separated values
    $ParsedArgs = @()
    foreach ($arg in $Platforms) {
        if ($arg -like "*,*") {
            $ParsedArgs += $arg -split ","
        } else {
            $ParsedArgs += $arg
        }
    }
    
    foreach ($arg in $ParsedArgs) {
        $arg = $arg.Trim()
        switch ($arg) {
            { $_ -in @("win", "windows") } { 
                $BuildPlatforms += @("win-x64", "win-arm64") 
            }
            { $_ -in @("macos", "osx") } { 
                $BuildPlatforms += @("osx-x64", "osx-arm64") 
            }
            "linux" { 
                $BuildPlatforms += @("linux-x64", "linux-arm64") 
            }
            { $_ -in @("win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64") } { 
                $BuildPlatforms += $_ 
            }
            default {
                Write-Host "Unknown platform: $arg" -ForegroundColor Red
                Write-Host "Valid platforms:" -ForegroundColor White
                Write-Host "  Platform families: win, macos, linux" -ForegroundColor White
                Write-Host "  Specific targets: win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64" -ForegroundColor White
                Write-Host "  Special options: -All, -Dev" -ForegroundColor White
                exit 1
            }
        }
    }
    
    if ($BuildPlatforms.Count -eq 0) {
        Write-Host "No valid platforms specified" -ForegroundColor Red
        exit 1
    }
    
    Write-Host " Building Squish for platforms: $($BuildPlatforms -join ', ')" -ForegroundColor Green
}

Write-Host ""

# Handle development vs publish builds
$ConsoleProject = "Squish.Console/Squish.Console.csproj"
$UIProject = "Squish.UI/Squish.UI.csproj"

if ($Dev) {
    # Fast development build - just compile, no publish
    $CleanPaths = @("Squish.Console/bin", "Squish.Console/obj", "Squish.UI/bin", "Squish.UI/obj", "Squish.Core/bin", "Squish.Core/obj")
    $NeedsCleaning = $CleanPaths | Where-Object { Test-Path $_ }
    
    if ($NeedsCleaning.Count -gt 0) {
        Write-Host " Cleaning previous development builds..." -ForegroundColor Yellow
        $NeedsCleaning | ForEach-Object { Remove-Item -Recurse -Force $_ }
    }
    
    Write-Host " Building optimized release (development)..." -ForegroundColor Cyan
    Write-Host "Building console application..." -ForegroundColor Cyan
    dotnet build $ConsoleProject -c Release
    Write-Host "Building UI application..." -ForegroundColor Cyan
    dotnet build $UIProject -c Release
    
    Write-Host ""
    Write-Host " Development build complete!" -ForegroundColor Green
    Write-Host " Console executable location: Squish.Console/bin/Release/net9.0/squish" -ForegroundColor White
    Write-Host " UI executable location: Squish.UI/bin/Release/net9.0/Squish.UI" -ForegroundColor White
    Write-Host ""
    Write-Host " Ready to run locally!" -ForegroundColor Green
    exit 0
}

# Production publish builds
if (Test-Path "publish") {
    Write-Host " Cleaning previous builds..." -ForegroundColor Yellow
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
        "win-x64" { return @{ Icon = "[WIN]"; Description = "Windows x64" } }
        "win-arm64" { return @{ Icon = "[WIN]"; Description = "Windows ARM64" } }
        "osx-x64" { return @{ Icon = "[MAC]"; Description = "macOS x64 (Intel)" } }
        "osx-arm64" { return @{ Icon = "[MAC]"; Description = "macOS ARM64 (Apple Silicon)" } }
        "linux-x64" { return @{ Icon = "[LNX]"; Description = "Linux x64" } }
        "linux-arm64" { return @{ Icon = "[LNX]"; Description = "Linux ARM64" } }
    }
}

# Build for each specified platform
foreach ($platform in $BuildPlatforms) {
    $info = Get-PlatformInfo $platform
    Write-Host "$($info.Icon) Building for $($info.Description)..." -ForegroundColor Cyan
    
    # Build console application
    Write-Host "  Building console application..." -ForegroundColor Cyan
    dotnet publish $ConsoleProject @CommonArgs -r $platform -o "publish/$platform/console"
    
    # Build UI application with custom name
    Write-Host "  Building UI application (Squish)..." -ForegroundColor Cyan
    dotnet publish $UIProject @CommonArgs -r $platform -o "publish/$platform/ui"
    
    # Move executables to platform root and rename appropriately
    if ($platform -like "win-*") {
        if (Test-Path "publish/$platform/console/squish.exe") {
            Move-Item "publish/$platform/console/squish.exe" "publish/$platform/squish-console.exe" -Force
        }
        if (Test-Path "publish/$platform/ui/Squish.UI.exe") {
            Move-Item "publish/$platform/ui/Squish.UI.exe" "publish/$platform/Squish.exe" -Force
        }
    } else {
        if (Test-Path "publish/$platform/console/squish") {
            Move-Item "publish/$platform/console/squish" "publish/$platform/squish-console" -Force
        }
        if (Test-Path "publish/$platform/ui/Squish.UI") {
            Move-Item "publish/$platform/ui/Squish.UI" "publish/$platform/Squish" -Force
        }
    }
    
    # Clean up subdirectories
    @("publish/$platform/console", "publish/$platform/ui") | Where-Object { Test-Path $_ } | ForEach-Object { 
        Remove-Item -Recurse -Force $_ 
    }
    
    Write-Host " $($info.Description) build complete" -ForegroundColor Green
    Write-Host ""
}

# Make executables executable on Unix systems
Write-Host " Setting executable permissions..." -ForegroundColor Cyan
foreach ($platform in $BuildPlatforms) {
    if ($platform -like "osx-*" -or $platform -like "linux-*") {
        $consoleExe = "publish/$platform/squish-console"
        $uiExe = "publish/$platform/Squish"
        
        if (Test-Path $consoleExe) {
            try {
                if ($IsLinux -or $IsMacOS) {
                    & chmod +x $consoleExe 2>$null
                }
            } catch {
                # Ignore chmod errors on Windows or if chmod is not available
            }
        }
        if (Test-Path $uiExe) {
            try {
                if ($IsLinux -or $IsMacOS) {
                    & chmod +x $uiExe 2>$null
                }
            } catch {
                # Ignore chmod errors on Windows or if chmod is not available
            }
        }
    }
}

# Display build results
Write-Host " All builds completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host " Executables created in:" -ForegroundColor White

foreach ($platform in $BuildPlatforms) {
    $info = Get-PlatformInfo $platform
    
    if ($platform -like "win-*") {
        $consoleExe = "publish/$platform/squish-console.exe"
        $uiExe = "publish/$platform/Squish.exe"
        
        if (Test-Path $consoleExe) {
            Write-Host "   • $($info.Description) Console: $consoleExe" -ForegroundColor Gray
        }
        if (Test-Path $uiExe) {
            Write-Host "   • $($info.Description) UI: $uiExe" -ForegroundColor Gray
        }
    } else {
        $consoleExe = "publish/$platform/squish-console"
        $uiExe = "publish/$platform/Squish"
        
        if (Test-Path $consoleExe) {
            Write-Host "   • $($info.Description) Console: $consoleExe" -ForegroundColor Gray
        }
        if (Test-Path $uiExe) {
            Write-Host "   • $($info.Description) UI: $uiExe" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host " These executables include the .NET runtime and can run without installing .NET" -ForegroundColor Yellow
Write-Host ""

# Show file sizes
Write-Host " Executable sizes:" -ForegroundColor White
foreach ($platform in $BuildPlatforms) {
    $info = Get-PlatformInfo $platform
    
    if ($platform -like "win-*") {
        $consoleExe = "publish/$platform/squish-console.exe"
        $uiExe = "publish/$platform/Squish.exe"
        
        if (Test-Path $consoleExe) {
            $size = (Get-Item $consoleExe).Length
            $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
            Write-Host "   $($info.Description) Console: $sizeStr" -ForegroundColor Gray
        }
        if (Test-Path $uiExe) {
            $size = (Get-Item $uiExe).Length
            $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
            Write-Host "   $($info.Description) UI: $sizeStr" -ForegroundColor Gray
        }
    } else {
        $consoleExe = "publish/$platform/squish-console"
        $uiExe = "publish/$platform/Squish"
        
        if (Test-Path $consoleExe) {
            $size = (Get-Item $consoleExe).Length
            $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
            Write-Host "   $($info.Description) Console: $sizeStr" -ForegroundColor Gray
        }
        if (Test-Path $uiExe) {
            $size = (Get-Item $uiExe).Length
            $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
            Write-Host "   $($info.Description) UI: $sizeStr" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host " Ready to distribute!" -ForegroundColor Green