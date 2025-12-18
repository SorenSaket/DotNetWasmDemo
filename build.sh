#!/bin/bash

echo "========================================"
echo " .NET NativeAOT-LLVM WebAssembly Build"
echo "========================================"
echo ""

# Check if emcc is available
if ! command -v emcc &> /dev/null; then
    echo "Error: emcc not found. Please ensure Emscripten is installed and in PATH."
    echo "On macOS with Homebrew: brew install emscripten"
    exit 1
fi

echo "Using Emscripten version:"
emcc --version | head -1
echo ""

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

echo "Building for browser-wasm..."
echo ""

# Clean previous build
rm -rf bin/Release/net10.0/browser-wasm/publish

# Build and publish
dotnet publish -r browser-wasm -c Release

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================"
    echo " Build successful!"
    echo "========================================"
    echo ""
    echo "Output: $SCRIPT_DIR/bin/Release/net10.0/browser-wasm/publish/"
    echo ""
    echo "Run './serve.sh' to start the web server"
    echo ""
else
    echo ""
    echo "Build failed with error code $?"
    echo ""
    exit 1
fi
