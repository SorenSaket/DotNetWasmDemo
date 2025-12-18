# Use .NET 10 SDK as base
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview

# Install dependencies for Emscripten
RUN apt-get update && apt-get install -y \
    git \
    python3 \
    cmake \
    xz-utils \
    && rm -rf /var/lib/apt/lists/*

# Install Emscripten SDK (latest version for SDL3 support)
WORKDIR /emsdk
RUN git clone https://github.com/emscripten-core/emsdk.git . && \
    ./emsdk install latest && \
    ./emsdk activate latest

# Set up Emscripten environment
ENV EMSDK=/emsdk
ENV PATH="/emsdk:/emsdk/upstream/emscripten:${PATH}"

# Set working directory for the project
WORKDIR /src

# Copy project files
COPY . .

# Build the project
RUN bash -c "source /emsdk/emsdk_env.sh && dotnet publish -r browser-wasm -c Release"

# Apply HEAP32 fix (use node from emsdk)
RUN bash -c "source /emsdk/emsdk_env.sh && node scripts/fix-heap-exports.js"

# Expose port for serving
EXPOSE 8080

# Serve the built output
CMD ["python3", "-m", "http.server", "8080", "--directory", "bin/Release/net10.0/browser-wasm/publish"]
