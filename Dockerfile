# Use .NET 10 SDK as base
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview

# Install dependencies for Emscripten
RUN apt-get update && apt-get install -y \
    git \
    python3 \
    cmake \
    xz-utils \
    && rm -rf /var/lib/apt/lists/*

# Install Emscripten SDK
# NativeAOT-LLVM requires Emscripten 3.1.56 - do not change without verifying compatibility
# See: https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/eng/pipelines/runtimelab/install-emscripten.ps1
WORKDIR /emsdk
RUN git clone https://github.com/emscripten-core/emsdk.git . && \
    ./emsdk install 3.1.56 && \
    ./emsdk activate 3.1.56

# Set up Emscripten environment
ENV EMSDK=/emsdk
ENV PATH="/emsdk:/emsdk/upstream/emscripten:/emsdk/node/20.18.0_64bit/bin:${PATH}"
ENV EMSDK_NODE="/emsdk/node/20.18.0_64bit/bin/node"

# Set working directory for the project
WORKDIR /src

# Copy project files
COPY . .

# Build command - also applies HEAP32 fix for NativeAOT-LLVM
CMD ["/bin/bash", "-c", "source /emsdk/emsdk_env.sh && dotnet publish -r browser-wasm -c Release && node scripts/fix-heap-exports.js"]
