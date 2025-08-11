#!/bin/bash

# Unified build script for Squish - creates self-contained executables
# This script builds the Squish console application with embedded .NET runtime
# so users don't need to install .NET separately.
#
# Usage:
#   ./build.sh                        # Build for current platform only (default)
#   ./build.sh --all                  # Build for all supported platforms
#   ./build.sh --dev                  # Fast development build (current platform, no publish)
#   ./build.sh win                    # Build for Windows (both x64 and ARM64)
#   ./build.sh macos                  # Build for macOS (both x64 and ARM64)
#   ./build.sh linux                  # Build for Linux (both x64 and ARM64)
#   ./build.sh win linux              # Build for multiple platform families
#   ./build.sh win-x64                # Build for Windows x64 specifically
#   ./build.sh osx-arm64              # Build for macOS ARM64 specifically
#   ./build.sh linux-arm64            # Build for Linux ARM64 specifically

set -e  # Exit on any error

# Detect current platform
detect_current_platform() {
    local os=$(uname -s)
    local arch=$(uname -m)
    
    case $os in
        Linux)
            case $arch in
                x86_64) echo "linux-x64" ;;
                aarch64|arm64) echo "linux-arm64" ;;
                *) echo "linux-x64" ;;  # fallback
            esac
            ;;
        Darwin)
            case $arch in
                x86_64) echo "osx-x64" ;;
                arm64) echo "osx-arm64" ;;
                *) echo "osx-x64" ;;  # fallback
            esac
            ;;
        MINGW*|CYGWIN*|MSYS*)
            case $arch in
                x86_64) echo "win-x64" ;;
                aarch64|arm64) echo "win-arm64" ;;
                *) echo "win-x64" ;;  # fallback
            esac
            ;;
        *)
            echo "linux-x64"  # fallback
            ;;
    esac
}

# All supported platforms (64-bit and ARM only)
ALL_PLATFORMS=("win-x64" "win-arm64" "osx-x64" "osx-arm64" "linux-x64" "linux-arm64")

# Parse command line arguments
PLATFORMS=()
DEV_BUILD=false

if [ $# -eq 0 ]; then
    # Default: build for current platform only
    CURRENT_PLATFORM=$(detect_current_platform)
    PLATFORMS=("$CURRENT_PLATFORM")
    echo "🔨 Building Squish for current platform: $CURRENT_PLATFORM"
else
    # Parse arguments
    for arg in "$@"; do
        case $arg in
            --all)
                PLATFORMS=("${ALL_PLATFORMS[@]}")
                echo "🔨 Building Squish for all supported platforms..."
                ;;
            --dev)
                DEV_BUILD=true
                CURRENT_PLATFORM=$(detect_current_platform)
                PLATFORMS=("$CURRENT_PLATFORM")
                echo "🔨 Fast development build for current platform: $CURRENT_PLATFORM"
                ;;
            win|windows)
                PLATFORMS+=("win-x64" "win-arm64")
                ;;
            macos|osx)
                PLATFORMS+=("osx-x64" "osx-arm64")
                ;;
            linux)
                PLATFORMS+=("linux-x64" "linux-arm64")
                ;;
            win-x64|win-arm64|osx-x64|osx-arm64|linux-x64|linux-arm64)
                PLATFORMS+=("$arg")
                ;;
            *)
                echo "❌ Unknown platform: $arg"
                echo "Valid platforms:"
                echo "  Platform families: win, macos, linux"
                echo "  Specific targets: win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64"
                echo "  Special options: --all, --dev"
                exit 1
                ;;
        esac
    done
    
    if [ ${#PLATFORMS[@]} -eq 0 ]; then
        echo "❌ No valid platforms specified"
        exit 1
    fi
    
    if [ "$DEV_BUILD" = false ]; then
        echo "🔨 Building Squish for platforms: ${PLATFORMS[*]}"
    fi
fi
echo ""

# Handle development vs publish builds
CONSOLE_PROJECT="Squish.Console/Squish.Console.csproj"
UI_PROJECT="Squish.UI/Squish.UI.csproj"

if [ "$DEV_BUILD" = true ]; then
    # Fast development build - just compile, no publish
    if [ -d "Squish.Console/bin" ] || [ -d "Squish.Console/obj" ] || [ -d "Squish.UI/bin" ] || [ -d "Squish.UI/obj" ]; then
        echo "🧹 Cleaning previous development builds..."
        rm -rf Squish.Console/bin Squish.Console/obj Squish.UI/bin Squish.UI/obj Squish.Core/bin Squish.Core/obj
    fi
    
    echo "⚡ Building optimized release (development)..."
    echo "Building Console application..."
    dotnet build $CONSOLE_PROJECT -c Release
    echo "Building UI application..."
    dotnet build $UI_PROJECT -c Release
    
    echo ""
    echo "✅ Development build complete!"
    echo "📂 Console app: Squish.Console/bin/Release/net9.0/squish"
    echo "📂 UI app: Squish.UI/bin/Release/net9.0/Squish.UI"
    echo ""
    echo "🚀 Ready to run locally!"
    exit 0
fi

# Production publish builds
if [ -d "publish" ]; then
    echo "🧹 Cleaning previous builds..."
    rm -rf publish
fi

# Create publish directory
mkdir -p publish

# Common publish arguments
COMMON_ARGS="-c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true"

# Function to get platform display info
get_platform_info() {
    case $1 in
        win-x64)
            echo "🪟" "Windows x64"
            ;;
        win-arm64)
            echo "🪟" "Windows ARM64"
            ;;
        osx-x64)
            echo "🍎" "macOS x64 (Intel)"
            ;;
        osx-arm64)
            echo "🍎" "macOS ARM64 (Apple Silicon)"
            ;;
        linux-x64)
            echo "🐧" "Linux x64"
            ;;
        linux-arm64)
            echo "🐧" "Linux ARM64"
            ;;
    esac
}

# Build for each specified platform
for platform in "${PLATFORMS[@]}"; do
    read -r icon description <<< "$(get_platform_info "$platform")"
    echo "$icon Building for $description..."
    
    # Build Console application
    echo "  Building Console app..."
    dotnet publish $CONSOLE_PROJECT $COMMON_ARGS -r "$platform" -o "publish/$platform/console"
    
    # Build UI application
    echo "  Building UI app..."
    dotnet publish $UI_PROJECT $COMMON_ARGS -r "$platform" -o "publish/$platform/ui"
    
    echo "✅ $description build complete"
    echo ""
done

# Make executables executable on Unix systems
echo "🔧 Setting executable permissions..."
for platform in "${PLATFORMS[@]}"; do
    case $platform in
        osx-x64|osx-arm64|linux-x64|linux-arm64)
            if [ -f "publish/$platform/console/squish" ]; then
                chmod +x "publish/$platform/console/squish"
            fi
            if [ -f "publish/$platform/ui/Squish.UI" ]; then
                chmod +x "publish/$platform/ui/Squish.UI"
            fi
            ;;
    esac
done

# Display build results
echo "🎉 All builds completed successfully!"
echo ""
echo "📂 Executables created in:"

for platform in "${PLATFORMS[@]}"; do
    read -r icon description <<< "$(get_platform_info "$platform")"
    
    # Console app paths
    case $platform in
        win-x64|win-arm64)
            if [ -f "publish/$platform/console/squish.exe" ]; then
                echo "   • $description Console: publish/$platform/console/squish.exe"
            fi
            ;;
        *)
            if [ -f "publish/$platform/console/squish" ]; then
                echo "   • $description Console: publish/$platform/console/squish"
            fi
            ;;
    esac
    
    # UI app paths
    case $platform in
        win-x64|win-arm64)
            if [ -f "publish/$platform/ui/Squish.UI.exe" ]; then
                echo "   • $description UI: publish/$platform/ui/Squish.UI.exe"
            fi
            ;;
        *)
            if [ -f "publish/$platform/ui/Squish.UI" ]; then
                echo "   • $description UI: publish/$platform/ui/Squish.UI"
            fi
            ;;
    esac
done

echo ""
echo "💡 These executables include the .NET runtime and can run without installing .NET"
echo ""

# Show file sizes
echo "📊 Executable sizes:"
for platform in "${PLATFORMS[@]}"; do
    read -r icon description <<< "$(get_platform_info "$platform")"
    
    # Console app sizes
    case $platform in
        win-x64|win-arm64)
            if [ -f "publish/$platform/console/squish.exe" ]; then
                echo -n "   $description Console: "
                ls -lh "publish/$platform/console/squish.exe" | awk '{print $5}'
            fi
            ;;
        *)
            if [ -f "publish/$platform/console/squish" ]; then
                echo -n "   $description Console: "
                ls -lh "publish/$platform/console/squish" | awk '{print $5}'
            fi
            ;;
    esac
    
    # UI app sizes
    case $platform in
        win-x64|win-arm64)
            if [ -f "publish/$platform/ui/Squish.UI.exe" ]; then
                echo -n "   $description UI: "
                ls -lh "publish/$platform/ui/Squish.UI.exe" | awk '{print $5}'
            fi
            ;;
        *)
            if [ -f "publish/$platform/ui/Squish.UI" ]; then
                echo -n "   $description UI: "
                ls -lh "publish/$platform/ui/Squish.UI" | awk '{print $5}'
            fi
            ;;
    esac
done

echo ""
echo "🚀 Ready to distribute!"