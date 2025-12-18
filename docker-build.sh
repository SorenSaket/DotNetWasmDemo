#!/bin/bash

echo "========================================"
echo " Docker-based NativeAOT-LLVM WASM Build"
echo "========================================"
echo ""

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Error: Docker is not running. Please start Docker Desktop."
    exit 1
fi

echo "Building Docker image..."
docker build -t dotnet-wasm-builder .

if [ $? -ne 0 ]; then
    echo "Error: Failed to build Docker image"
    exit 1
fi

echo ""
echo "Running build in Docker container..."
docker run --rm \
    -v "$SCRIPT_DIR:/src" \
    -w /src \
    dotnet-wasm-builder

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
    echo "Build failed!"
    exit 1
fi
