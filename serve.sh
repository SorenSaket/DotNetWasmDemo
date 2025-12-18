#!/bin/bash

echo "Starting web server..."
echo ""

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PUBLISH_DIR="$SCRIPT_DIR/bin/Release/net10.0/browser-wasm/publish"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found. Run ./build.sh first."
    exit 1
fi

cd "$PUBLISH_DIR"

# Check if Python 3 is available
if command -v python3 &> /dev/null; then
    echo "Serving at http://localhost:8080"
    echo "Press Ctrl+C to stop"
    echo ""
    python3 -m http.server 8080
else
    echo "Error: Python 3 not found. Please install Python 3."
    exit 1
fi
