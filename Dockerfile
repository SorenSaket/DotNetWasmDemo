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
WORKDIR /emsdk
RUN git clone https://github.com/emscripten-core/emsdk.git . && \
    ./emsdk install latest && \
    ./emsdk activate latest

# Set up Emscripten environment
ENV EMSDK=/emsdk
ENV PATH="/emsdk:/emsdk/upstream/emscripten:/emsdk/node/20.18.0_64bit/bin:${PATH}"
ENV EMSDK_NODE="/emsdk/node/20.18.0_64bit/bin/node"

# Set working directory for the project
WORKDIR /src

# Copy project files
COPY . .

# Build command
CMD ["/bin/bash", "-c", "source /emsdk/emsdk_env.sh && dotnet publish -r browser-wasm -c Release"]
