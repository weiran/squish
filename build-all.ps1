# Build script for Squish - creates self-contained executables for all platforms
# This script builds the Squish console application with embedded .NET runtime
# so users don't need to install .NET separately.

$ErrorActionPreference = "Stop"

Write-Host "🔨 Building Squish for all platforms..." -ForegroundColor Green
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

# Build for Windows x64
Write-Host "🪟 Building for Windows x64..." -ForegroundColor Cyan
dotnet publish $Project @CommonArgs -r win-x64 -o publish/win-x64
Write-Host "✅ Windows x64 build complete" -ForegroundColor Green
Write-Host ""

# Build for macOS x64 (Intel)
Write-Host "🍎 Building for macOS x64 (Intel)..." -ForegroundColor Cyan
dotnet publish $Project @CommonArgs -r osx-x64 -o publish/osx-x64
Write-Host "✅ macOS x64 build complete" -ForegroundColor Green
Write-Host ""

# Build for macOS ARM64 (Apple Silicon)
Write-Host "🍎 Building for macOS ARM64 (Apple Silicon)..." -ForegroundColor Cyan
dotnet publish $Project @CommonArgs -r osx-arm64 -o publish/osx-arm64
Write-Host "✅ macOS ARM64 build complete" -ForegroundColor Green
Write-Host ""

# Build for Linux x64
Write-Host "🐧 Building for Linux x64..." -ForegroundColor Cyan
dotnet publish $Project @CommonArgs -r linux-x64 -o publish/linux-x64
Write-Host "✅ Linux x64 build complete" -ForegroundColor Green
Write-Host ""

# Display build results
Write-Host "🎉 All builds completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📂 Executables created in:" -ForegroundColor White
Write-Host "   • Windows x64:     publish/win-x64/squish.exe" -ForegroundColor Gray
Write-Host "   • macOS x64:       publish/osx-x64/squish" -ForegroundColor Gray
Write-Host "   • macOS ARM64:     publish/osx-arm64/squish" -ForegroundColor Gray  
Write-Host "   • Linux x64:       publish/linux-x64/squish" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 These executables include the .NET runtime and can run without installing .NET" -ForegroundColor Yellow
Write-Host ""

# Show file sizes
Write-Host "📊 Executable sizes:" -ForegroundColor White

if (Test-Path "publish/win-x64/squish.exe") {
    $size = (Get-Item "publish/win-x64/squish.exe").Length
    $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
    Write-Host "   Windows x64:  $sizeStr" -ForegroundColor Gray
}

if (Test-Path "publish/osx-x64/squish") {
    $size = (Get-Item "publish/osx-x64/squish").Length
    $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
    Write-Host "   macOS x64:    $sizeStr" -ForegroundColor Gray
}

if (Test-Path "publish/osx-arm64/squish") {
    $size = (Get-Item "publish/osx-arm64/squish").Length
    $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
    Write-Host "   macOS ARM64:  $sizeStr" -ForegroundColor Gray
}

if (Test-Path "publish/linux-x64/squish") {
    $size = (Get-Item "publish/linux-x64/squish").Length
    $sizeStr = if ($size -gt 1MB) { "{0:N1} MB" -f ($size / 1MB) } else { "{0:N1} KB" -f ($size / 1KB) }
    Write-Host "   Linux x64:    $sizeStr" -ForegroundColor Gray
}

Write-Host ""
Write-Host "🚀 Ready to distribute!" -ForegroundColor Green