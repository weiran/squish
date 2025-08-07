#!/bin/bash

# Build script for Squish - creates self-contained executables for all platforms
# This script builds the Squish console application with embedded .NET runtime
# so users don't need to install .NET separately.

set -e  # Exit on any error

echo "🔨 Building Squish for all platforms..."
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

# Build for Windows x64
echo "🪟 Building for Windows x64..."
dotnet publish $PROJECT $COMMON_ARGS -r win-x64 -o publish/win-x64
echo "✅ Windows x64 build complete"
echo ""

# Build for macOS x64 (Intel)
echo "🍎 Building for macOS x64 (Intel)..."
dotnet publish $PROJECT $COMMON_ARGS -r osx-x64 -o publish/osx-x64
echo "✅ macOS x64 build complete"
echo ""

# Build for macOS ARM64 (Apple Silicon)
echo "🍎 Building for macOS ARM64 (Apple Silicon)..."
dotnet publish $PROJECT $COMMON_ARGS -r osx-arm64 -o publish/osx-arm64
echo "✅ macOS ARM64 build complete"
echo ""

# Build for Linux x64
echo "🐧 Building for Linux x64..."
dotnet publish $PROJECT $COMMON_ARGS -r linux-x64 -o publish/linux-x64
echo "✅ Linux x64 build complete"
echo ""

# Make executables executable on Unix systems
echo "🔧 Setting executable permissions..."
chmod +x publish/osx-x64/squish
chmod +x publish/osx-arm64/squish
chmod +x publish/linux-x64/squish

# Display build results
echo "🎉 All builds completed successfully!"
echo ""
echo "📂 Executables created in:"
echo "   • Windows x64:     publish/win-x64/squish.exe"
echo "   • macOS x64:       publish/osx-x64/squish"
echo "   • macOS ARM64:     publish/osx-arm64/squish"
echo "   • Linux x64:       publish/linux-x64/squish"
echo ""
echo "💡 These executables include the .NET runtime and can run without installing .NET"
echo ""

# Show file sizes
echo "📊 Executable sizes:"
if [ -f "publish/win-x64/squish.exe" ]; then
    echo -n "   Windows x64:  "
    ls -lh publish/win-x64/squish.exe | awk '{print $5}'
fi

if [ -f "publish/osx-x64/squish" ]; then
    echo -n "   macOS x64:    "
    ls -lh publish/osx-x64/squish | awk '{print $5}'
fi

if [ -f "publish/osx-arm64/squish" ]; then
    echo -n "   macOS ARM64:  "
    ls -lh publish/osx-arm64/squish | awk '{print $5}'
fi

if [ -f "publish/linux-x64/squish" ]; then
    echo -n "   Linux x64:    "
    ls -lh publish/linux-x64/squish | awk '{print $5}'
fi

echo ""
echo "🚀 Ready to distribute!"