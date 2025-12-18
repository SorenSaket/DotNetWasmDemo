// Main JavaScript entry point for .NET WASM WebGPU Demo

// WebGPU state
let adapter = null;
let device = null;
let context = null;
let pipeline = null;
let canvas = null;
let animationFrameId = null;

// Input state
let mouseX = 0;
let mouseY = 0;
let mousePressed = false;

// .NET exports (will be set after runtime loads)
let dotnetExports = null;

// Export functions for .NET interop
export function isWebGPUSupported() {
    return 'gpu' in navigator;
}

export function setStatus(message) {
    const statusEl = document.getElementById('status');
    if (statusEl) {
        statusEl.textContent = message;
    }
}

export async function initializeWebGPU() {
    try {
        canvas = document.getElementById('canvas');
        if (!canvas) {
            console.error('Canvas not found');
            return false;
        }

        // Request adapter
        adapter = await navigator.gpu.requestAdapter({
            powerPreference: 'high-performance'
        });

        if (!adapter) {
            console.error('Failed to get WebGPU adapter');
            return false;
        }

        console.log('WebGPU adapter acquired:', adapter);

        // Request device
        device = await adapter.requestDevice();
        if (!device) {
            console.error('Failed to get WebGPU device');
            return false;
        }

        console.log('WebGPU device acquired:', device);

        // Configure canvas context
        context = canvas.getContext('webgpu');
        if (!context) {
            console.error('Failed to get WebGPU context');
            return false;
        }

        const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
        context.configure({
            device: device,
            format: canvasFormat,
            alphaMode: 'premultiplied'
        });

        console.log('WebGPU context configured with format:', canvasFormat);

        return true;
    } catch (err) {
        console.error('WebGPU initialization error:', err);
        return false;
    }
}

export async function createRenderPipeline() {
    try {
        const canvasFormat = navigator.gpu.getPreferredCanvasFormat();

        // Vertex shader - renders a colorful triangle
        const vertexShaderCode = `
struct VertexOutput {
    @builtin(position) position: vec4f,
    @location(0) color: vec4f,
}

@vertex
fn vs_main(@builtin(vertex_index) vertex_index: u32) -> VertexOutput {
    var positions = array<vec2f, 3>(
        vec2f(0.0, 0.5),
        vec2f(-0.5, -0.5),
        vec2f(0.5, -0.5)
    );

    var colors = array<vec4f, 3>(
        vec4f(1.0, 0.0, 0.0, 1.0),  // Red
        vec4f(0.0, 1.0, 0.0, 1.0),  // Green
        vec4f(0.0, 0.0, 1.0, 1.0)   // Blue
    );

    var output: VertexOutput;
    output.position = vec4f(positions[vertex_index], 0.0, 1.0);
    output.color = colors[vertex_index];
    return output;
}
`;

        // Fragment shader
        const fragmentShaderCode = `
@fragment
fn fs_main(@location(0) color: vec4f) -> @location(0) vec4f {
    return color;
}
`;

        // Create shader module
        const shaderModule = device.createShaderModule({
            label: 'Triangle shader',
            code: vertexShaderCode + fragmentShaderCode
        });

        // Create pipeline
        pipeline = device.createRenderPipeline({
            label: 'Triangle pipeline',
            layout: 'auto',
            vertex: {
                module: shaderModule,
                entryPoint: 'vs_main'
            },
            fragment: {
                module: shaderModule,
                entryPoint: 'fs_main',
                targets: [{
                    format: canvasFormat
                }]
            },
            primitive: {
                topology: 'triangle-list',
                frontFace: 'ccw',
                cullMode: 'none'
            }
        });

        console.log('Render pipeline created');
        return true;
    } catch (err) {
        console.error('Pipeline creation error:', err);
        return false;
    }
}

export function registerInputHandlers() {
    if (!canvas) {
        canvas = document.getElementById('canvas');
    }

    // Mouse events
    canvas.addEventListener('mousemove', (e) => {
        const rect = canvas.getBoundingClientRect();
        mouseX = e.clientX - rect.left;
        mouseY = e.clientY - rect.top;

        if (dotnetExports) {
            dotnetExports.DotNetWasmDemo.Program.OnMouseMove(mouseX, mouseY);
        }
    });

    canvas.addEventListener('mousedown', (e) => {
        mousePressed = true;
        const rect = canvas.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;

        if (dotnetExports) {
            dotnetExports.DotNetWasmDemo.Program.OnMouseDown(x, y, e.button);
        }
    });

    canvas.addEventListener('mouseup', (e) => {
        mousePressed = false;
        const rect = canvas.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;

        if (dotnetExports) {
            dotnetExports.DotNetWasmDemo.Program.OnMouseUp(x, y, e.button);
        }
    });

    canvas.addEventListener('mouseleave', () => {
        mousePressed = false;
    });

    // Keyboard events
    document.addEventListener('keydown', (e) => {
        if (dotnetExports) {
            dotnetExports.DotNetWasmDemo.Program.OnKeyDown(e.key);
        }
    });

    document.addEventListener('keyup', (e) => {
        if (dotnetExports) {
            dotnetExports.DotNetWasmDemo.Program.OnKeyUp(e.key);
        }
    });

    // Prevent context menu on right-click
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());

    console.log('Input handlers registered');
}

export function startRenderLoop() {
    function frame(timestamp) {
        if (dotnetExports) {
            dotnetExports.DotNetWasmDemo.Program.OnFrame(timestamp);
        }
        animationFrameId = requestAnimationFrame(frame);
    }
    animationFrameId = requestAnimationFrame(frame);
    console.log('Render loop started');
}

export function stopRenderLoop() {
    if (animationFrameId !== null) {
        cancelAnimationFrame(animationFrameId);
        animationFrameId = null;
        console.log('Render loop stopped');
    }
}

export function renderFrame(r, g, b) {
    if (!device || !context || !pipeline) return;

    try {
        // Get current texture from canvas
        const textureView = context.getCurrentTexture().createView();

        // Create command encoder
        const commandEncoder = device.createCommandEncoder();

        // Begin render pass
        const renderPass = commandEncoder.beginRenderPass({
            colorAttachments: [{
                view: textureView,
                clearValue: { r: r, g: g, b: b, a: 1.0 },
                loadOp: 'clear',
                storeOp: 'store'
            }]
        });

        // Draw triangle
        renderPass.setPipeline(pipeline);
        renderPass.draw(3, 1, 0, 0);
        renderPass.end();

        // Submit commands
        device.queue.submit([commandEncoder.finish()]);
    } catch (err) {
        console.error('Render error:', err);
    }
}

export function getMouseX() {
    return mouseX;
}

export function getMouseY() {
    return mouseY;
}

export function isMousePressed() {
    return mousePressed;
}

// Initialize .NET runtime
async function init() {
    try {
        const { dotnet } = await import('./_framework/dotnet.js');

        setStatus('Initializing .NET runtime...');

        const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
            .withDiagnosticTracing(false)
            .create();

        // Register our module imports
        setModuleImports('main.js', {
            isWebGPUSupported,
            setStatus,
            initializeWebGPU,
            createRenderPipeline,
            registerInputHandlers,
            startRenderLoop,
            stopRenderLoop,
            renderFrame,
            getMouseX,
            getMouseY,
            isMousePressed
        });

        // Get exports from .NET assembly
        const config = getConfig();
        const assemblyName = config.mainAssemblyName.replace('.dll', '');
        dotnetExports = await getAssemblyExports(assemblyName);

        console.log('.NET exports:', dotnetExports);

        // Run main
        await runMain();

    } catch (err) {
        console.error('Failed to initialize .NET runtime:', err);
        setStatus(`Error: ${err.message}`);
    }
}

// Start initialization
init();
