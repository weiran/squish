#!/bin/bash

# Build script for Squish - creates self-contained executables for specified platforms
# This script builds the Squish console application with embedded .NET runtime
# so users don't need to install .NET separately.
#
# Usage:
#   ./build-all.sh                    # Build for all platforms
#   ./build-all.sh win                # Build for Windows only
#   ./build-all.sh macos              # Build for macOS (both x64 and ARM64)
#   ./build-all.sh linux              # Build for Linux only
#   ./build-all.sh win macos          # Build for Windows and macOS
#   ./build-all.sh win-x64            # Build for Windows x64 specifically
#   ./build-all.sh osx-x64            # Build for macOS x64 specifically
#   ./build-all.sh osx-arm64          # Build for macOS ARM64 specifically
#   ./build-all.sh linux-x64          # Build for Linux x64 specifically

set -e  # Exit on any error

# Parse command line arguments
PLATFORMS=()
if [ $# -eq 0 ]; then
    # Default: build for all platforms
    PLATFORMS=("win-x64" "osx-x64" "osx-arm64" "linux-x64")
    echo "🔨 Building Squish for all platforms..."
else
    # Parse platform arguments
    for arg in "$@"; do
        case $arg in
            win|windows)
                PLATFORMS+=("win-x64")
                ;;
            macos|osx)
                PLATFORMS+=("osx-x64" "osx-arm64")
                ;;
            linux)
                PLATFORMS+=("linux-x64")
                ;;
            win-x64|osx-x64|osx-arm64|linux-x64)
                PLATFORMS+=("$arg")
                ;;
            *)
                echo "❌ Unknown platform: $arg"
                echo "Valid platforms: win, macos, linux, win-x64, osx-x64, osx-arm64, linux-x64"
                exit 1
                ;;
        esac
    done
    echo "🔨 Building Squish for platforms: ${PLATFORMS[*]}"
fi
echo ""

# Clean previous builds
if [ -d "publish" ]; then
    echo "🧹 Cleaning previous builds..."
    rm -rf publish
fi

# Create publish directory
mkdir -p publish

# Common publish arguments
COMMON_ARGS="-c Release --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true"
PROJECT="Squish.Console/Squish.Console.csproj"

# Function to get platform display info
get_platform_info() {
    case $1 in
        win-x64)
            echo "🪟" "Windows x64"
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
    esac
}

# Build for each specified platform
for platform in "${PLATFORMS[@]}"; do
    read -r icon description <<< "$(get_platform_info "$platform")"
    echo "$icon Building for $description..."
    dotnet publish $PROJECT $COMMON_ARGS -r "$platform" -o "publish/$platform"
    echo "✅ $description build complete"
    echo ""
done

# Make executables executable on Unix systems
echo "🔧 Setting executable permissions..."
for platform in "${PLATFORMS[@]}"; do
    case $platform in
        osx-x64|osx-arm64|linux-x64)
            if [ -f "publish/$platform/squish" ]; then
                chmod +x "publish/$platform/squish"
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
    case $platform in
        win-x64)
            if [ -f "publish/$platform/squish.exe" ]; then
                echo "   • $description: publish/$platform/squish.exe"
            fi
            ;;
        *)
            if [ -f "publish/$platform/squish" ]; then
                echo "   • $description: publish/$platform/squish"
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
    case $platform in
        win-x64)
            if [ -f "publish/$platform/squish.exe" ]; then
                echo -n "   $description: "
                ls -lh "publish/$platform/squish.exe" | awk '{print $5}'
            fi
            ;;
        *)
            if [ -f "publish/$platform/squish" ]; then
                echo -n "   $description: "
                ls -lh "publish/$platform/squish" | awk '{print $5}'
            fi
            ;;
    esac
done

echo ""
echo "🚀 Ready to distribute!"