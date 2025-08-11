#!/bin/bash

# Simple build script for Squish - builds for the current platform only
# This is a fast build option for local development and testing.

set -e  # Exit on any error

echo "🔨 Building Squish for current platform..."
echo ""

# Clean previous builds
if [ -d "Squish.Console/bin" ]; then
    echo "🧹 Cleaning previous builds..."
    rm -rf Squish.Console/bin Squish.Console/obj
fi

# Build for current platform
echo "⚡ Building optimized release..."
dotnet build Squish.Console/Squish.Console.csproj -c Release

echo ""
echo "✅ Build complete!"
echo "📂 Executable location: Squish.Console/bin/Release/net9.0/squish"
echo ""
echo "🚀 Ready to run locally!"