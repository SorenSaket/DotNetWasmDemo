using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SDL3;
using WebGpuSharp;
using WebGpuSharp.FFI;
using WebGpuSharp.Marshalling;

namespace DotNetWasmDemo;

public static class Program
{
    // Window dimensions
    private const int WINDOW_WIDTH = 800;
    private const int WINDOW_HEIGHT = 600;

    // Application state
    private static nint _window;
    private static bool _running = true;
    private static float _hue = 0.0f;
    private static float _mouseX = 0;
    private static float _mouseY = 0;
    private static bool _mousePressed = false;

    // WebGPU state
    private static Instance? _instance;
    private static Adapter? _adapter;
    private static Device? _device;
    private static Surface? _surface;
    private static Queue? _queue;
    private static RenderPipeline? _pipeline;

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Starting .NET WASM WebGPU Demo...");

        // Initialize SDL3
        if (!InitializeSDL())
        {
            Console.WriteLine("Failed to initialize SDL3");
            return -1;
        }

        Console.WriteLine("SDL3 initialized successfully!");

        // Initialize WebGPU
        if (!await InitializeWebGPU())
        {
            Console.WriteLine("Failed to initialize WebGPU");
            Cleanup();
            return -1;
        }

        Console.WriteLine("WebGPU initialized successfully!");

        // Create render pipeline
        if (!CreateRenderPipeline())
        {
            Console.WriteLine("Failed to create render pipeline");
            Cleanup();
            return -1;
        }

        Console.WriteLine("Render pipeline created successfully!");
        Console.WriteLine("Controls: Space = reset color, Mouse click = brighten, ESC = quit");

        // Main loop
        #if BROWSER_WASM
        // For WASM, we use Emscripten's main loop
        EmscriptenSetMainLoop(MainLoop, 0, 1);
        #else
        // For native, use a simple while loop
        while (_running)
        {
            MainLoop();
            await Task.Delay(16); // ~60 FPS
        }
        #endif

        Cleanup();
        return 0;
    }

    private static bool InitializeSDL()
    {
        // Initialize SDL with video subsystem
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            Console.WriteLine($"SDL_Init failed: {SDL.GetError()}");
            return false;
        }

        // Create window
        _window = SDL.CreateWindow(
            "WebGPU Demo - .NET WASM",
            WINDOW_WIDTH,
            WINDOW_HEIGHT,
            SDL.WindowFlags.Resizable
        );

        if (_window == 0)
        {
            Console.WriteLine($"SDL_CreateWindow failed: {SDL.GetError()}");
            return false;
        }

        return true;
    }

    private static async Task<bool> InitializeWebGPU()
    {
        try
        {
            // Create WebGPU instance
            var instanceDesc = new InstanceDescriptor();
            _instance = WebGPU.CreateInstance(instanceDesc);
            if (_instance == null)
            {
                Console.WriteLine("Failed to create WebGPU instance");
                return false;
            }

            // Create surface from SDL window first (needed for adapter request)
            _surface = CreateSurfaceFromWindow();
            if (_surface == null)
            {
                Console.WriteLine("Failed to create WebGPU surface");
                return false;
            }

            // Request adapter
            var adapterOptions = new RequestAdapterOptions
            {
                PowerPreference = PowerPreference.HighPerformance,
                CompatibleSurface = _surface
            };

            _adapter = await _instance.RequestAdapterAsync(adapterOptions);
            if (_adapter == null)
            {
                Console.WriteLine("Failed to get WebGPU adapter");
                return false;
            }

            // Request device
            var deviceDescriptor = new DeviceDescriptor
            {
                Label = "Main Device"
            };

            _device = await _adapter.RequestDeviceAsync(deviceDescriptor);
            if (_device == null)
            {
                Console.WriteLine("Failed to get WebGPU device");
                return false;
            }

            // Get queue
            _queue = _device.GetQueue();

            // Configure surface
            var surfaceConfig = new SurfaceConfiguration
            {
                Device = _device,
                Format = TextureFormat.BGRA8Unorm,
                Usage = TextureUsage.RenderAttachment,
                Width = WINDOW_WIDTH,
                Height = WINDOW_HEIGHT,
                PresentMode = PresentMode.Fifo
            };

            _surface.Configure(surfaceConfig);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebGPU initialization error: {ex.Message}");
            return false;
        }
    }

    private static Surface? CreateSurfaceFromWindow()
    {
        if (_instance == null) return null;

        #if BROWSER_WASM
        // For browser WASM, create surface from canvas using native P/Invoke
        return CreateCanvasSurface(_instance, "#canvas");
        #else
        // For native, create surface from SDL window handle
        var surfaceDescriptor = new SurfaceDescriptor();
        return _instance.CreateSurface(surfaceDescriptor);
        #endif
    }

    #if BROWSER_WASM
    // Native interop for Emscripten canvas surface creation
    private static Surface? CreateCanvasSurface(Instance instance, string selector)
    {
        // Get the native instance handle using WebGPUMarshal
        var instanceHandle = WebGPUMarshal.GetBorrowHandle(instance);

        // Create surface descriptor with canvas selector
        var selectorBytes = System.Text.Encoding.UTF8.GetBytes(selector + "\0");
        unsafe
        {
            fixed (byte* selectorPtr = selectorBytes)
            {
                var canvasSource = new WGPUEmscriptenSurfaceSourceCanvasHTMLSelector
                {
                    chain = new WGPUChainedStruct
                    {
                        next = nint.Zero,
                        sType = WGPUSType.EmscriptenSurfaceSourceCanvasHTMLSelector
                    },
                    selector = new WGPUStringView
                    {
                        data = (nint)selectorPtr,
                        length = (nuint)(selectorBytes.Length - 1)
                    }
                };

                var surfaceDesc = new WGPUSurfaceDescriptor
                {
                    nextInChain = (nint)(&canvasSource),
                    label = new WGPUStringView { data = nint.Zero, length = 0 }
                };

                var surfaceHandle = wgpuInstanceCreateSurface((IntPtr)(UIntPtr)instanceHandle, &surfaceDesc);
                if (surfaceHandle == nint.Zero)
                    return null;

                // Wrap the native handle in a Surface object using WebGPUMarshal
                return WebGPUMarshal.ToSafeHandle<Surface, SurfaceHandle>(new SurfaceHandle((UIntPtr)surfaceHandle));
            }
        }
    }

    // Native WebGPU types for Emscripten
    private enum WGPUSType : uint
    {
        EmscriptenSurfaceSourceCanvasHTMLSelector = 0x00040004
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WGPUChainedStruct
    {
        public nint next;
        public WGPUSType sType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WGPUStringView
    {
        public nint data;
        public nuint length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WGPUEmscriptenSurfaceSourceCanvasHTMLSelector
    {
        public WGPUChainedStruct chain;
        public WGPUStringView selector;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WGPUSurfaceDescriptor
    {
        public nint nextInChain;
        public WGPUStringView label;
    }

    // For Emscripten, the webgpu functions are linked statically, use the library name from emdawnwebgpu
    [DllImport("webgpu")]
    private static extern unsafe nint wgpuInstanceCreateSurface(nint instance, WGPUSurfaceDescriptor* descriptor);
    #endif

    private static bool CreateRenderPipeline()
    {
        if (_device == null) return false;

        try
        {
            // Vertex shader (WGSL)
            string vertexShaderCode = @"
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
        vec4f(1.0, 0.0, 0.0, 1.0),
        vec4f(0.0, 1.0, 0.0, 1.0),
        vec4f(0.0, 0.0, 1.0, 1.0)
    );

    var output: VertexOutput;
    output.position = vec4f(positions[vertex_index], 0.0, 1.0);
    output.color = colors[vertex_index];
    return output;
}
";

            // Fragment shader (WGSL)
            string fragmentShaderCode = @"
@fragment
fn fs_main(@location(0) color: vec4f) -> @location(0) vec4f {
    return color;
}
";

            // Create shader modules using WGSL
            var vertexWgslDesc = new ShaderModuleWGSLDescriptor
            {
                Code = vertexShaderCode
            };
            var vertexModule = _device.CreateShaderModuleWGSL(vertexWgslDesc);

            var fragmentWgslDesc = new ShaderModuleWGSLDescriptor
            {
                Code = fragmentShaderCode
            };
            var fragmentModule = _device.CreateShaderModuleWGSL(fragmentWgslDesc);

            if (vertexModule == null || fragmentModule == null)
            {
                Console.WriteLine("Failed to create shader modules");
                return false;
            }

            // Create empty pipeline layout (auto layout)
            var pipelineLayoutDesc = new PipelineLayoutDescriptor
            {
                BindGroupLayouts = Array.Empty<BindGroupLayout>()
            };
            var pipelineLayout = _device.CreatePipelineLayout(pipelineLayoutDesc);

            // Create render pipeline
            var pipelineDescriptor = new RenderPipelineDescriptor
            {
                Label = "Main Pipeline",
                Layout = pipelineLayout,
                Vertex = new VertexState
                {
                    Module = vertexModule,
                    EntryPoint = "vs_main"
                },
                Fragment = new FragmentState
                {
                    Module = fragmentModule,
                    EntryPoint = "fs_main",
                    Targets = new[]
                    {
                        new ColorTargetState
                        {
                            Format = TextureFormat.BGRA8Unorm,
                            WriteMask = ColorWriteMask.All
                        }
                    }
                },
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    FrontFace = FrontFace.CCW,
                    CullMode = CullMode.None
                },
                Multisample = new MultisampleState
                {
                    Count = 1,
                    Mask = 0xFFFFFFFF
                }
            };

            _pipeline = _device.CreateRenderPipeline(pipelineDescriptor);

            return _pipeline != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pipeline creation error: {ex.Message}");
            return false;
        }
    }

    private static void MainLoop()
    {
        // Process SDL events
        ProcessEvents();

        if (!_running) return;

        // Update animation
        _hue += 0.5f;
        if (_hue >= 360.0f) _hue = 0.0f;

        // Render frame
        Render();
    }

    private static void ProcessEvents()
    {
        SDL.Event sdlEvent;
        while (SDL.PollEvent(out sdlEvent))
        {
            // Use if-else chain instead of switch for enum comparison
            if (sdlEvent.Type == (uint)SDL.EventType.Quit)
            {
                _running = false;
                Console.WriteLine("Quit event received");
            }
            else if (sdlEvent.Type == (uint)SDL.EventType.KeyDown)
            {
                HandleKeyDown(sdlEvent.Key);
            }
            else if (sdlEvent.Type == (uint)SDL.EventType.KeyUp)
            {
                HandleKeyUp(sdlEvent.Key);
            }
            else if (sdlEvent.Type == (uint)SDL.EventType.MouseMotion)
            {
                _mouseX = sdlEvent.Motion.X;
                _mouseY = sdlEvent.Motion.Y;
            }
            else if (sdlEvent.Type == (uint)SDL.EventType.MouseButtonDown)
            {
                _mousePressed = true;
                Console.WriteLine($"Mouse pressed at ({_mouseX}, {_mouseY})");
            }
            else if (sdlEvent.Type == (uint)SDL.EventType.MouseButtonUp)
            {
                _mousePressed = false;
                Console.WriteLine($"Mouse released at ({_mouseX}, {_mouseY})");
            }
            else if (sdlEvent.Type == (uint)SDL.EventType.WindowResized)
            {
                HandleResize(sdlEvent.Window.Data1, sdlEvent.Window.Data2);
            }
        }
    }

    private static void HandleKeyDown(SDL.KeyboardEvent keyEvent)
    {
        Console.WriteLine($"Key pressed: {keyEvent.Scancode}");

        // ESC to quit
        if (keyEvent.Scancode == SDL.Scancode.Escape)
        {
            _running = false;
        }

        // Space to reset color
        if (keyEvent.Scancode == SDL.Scancode.Space)
        {
            _hue = 0.0f;
            Console.WriteLine("Color reset!");
        }
    }

    private static void HandleKeyUp(SDL.KeyboardEvent keyEvent)
    {
        Console.WriteLine($"Key released: {keyEvent.Scancode}");
    }

    private static void HandleResize(int width, int height)
    {
        Console.WriteLine($"Window resized to {width}x{height}");

        if (_surface != null && _device != null)
        {
            // Reconfigure surface with new size
            var surfaceConfig = new SurfaceConfiguration
            {
                Device = _device,
                Format = TextureFormat.BGRA8Unorm,
                Usage = TextureUsage.RenderAttachment,
                Width = (uint)width,
                Height = (uint)height,
                PresentMode = PresentMode.Fifo
            };

            _surface.Configure(surfaceConfig);
        }
    }

    private static void Render()
    {
        if (_device == null || _surface == null || _queue == null || _pipeline == null)
            return;

        try
        {
            // Get current texture from surface
            var surfaceTexture = _surface.GetCurrentTexture();
            if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.SuccessOptimal)
            {
                Console.WriteLine($"Failed to get surface texture: {surfaceTexture.Status}");
                return;
            }

            var texture = surfaceTexture.Texture;
            if (texture == null)
            {
                Console.WriteLine("Surface texture is null");
                return;
            }

            var textureView = texture.CreateView();
            if (textureView == null)
            {
                Console.WriteLine("Failed to create texture view");
                return;
            }

            // Create command encoder
            var commandEncoder = _device.CreateCommandEncoder();

            // Calculate clear color based on hue and mouse position
            var (r, g, b) = HsvToRgb(_hue, 0.7f, 0.3f);

            // Adjust based on mouse position for interactivity
            float mouseInfluence = _mousePressed ? 0.3f : 0.0f;
            r = Math.Clamp(r + mouseInfluence, 0.0f, 1.0f);
            g = Math.Clamp(g + mouseInfluence, 0.0f, 1.0f);

            // Begin render pass
            var renderPassDescriptor = new RenderPassDescriptor
            {
                Label = "Main Render Pass",
                ColorAttachments = new[]
                {
                    new RenderPassColorAttachment
                    {
                        View = textureView,
                        LoadOp = LoadOp.Clear,
                        StoreOp = StoreOp.Store,
                        ClearValue = new Color { R = r, G = g, B = b, A = 1.0 }
                    }
                }
            };

            var renderPass = commandEncoder.BeginRenderPass(renderPassDescriptor);

            // Draw triangle
            renderPass.SetPipeline(_pipeline);
            renderPass.Draw(3, 1, 0, 0);
            renderPass.End();

            // Submit commands
            var commandBuffer = commandEncoder.Finish();
            _queue.Submit(new[] { commandBuffer });

            // Present
            _surface.Present();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Render error: {ex.Message}");
        }
    }

    private static (float r, float g, float b) HsvToRgb(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1 - Math.Abs((h / 60.0f) % 2 - 1));
        float m = v - c;

        float r, g, b;

        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return (r + m, g + m, b + m);
    }

    private static void Cleanup()
    {
        Console.WriteLine("Cleaning up...");

        _pipeline = null;
        _surface = null;
        _queue = null;
        _device = null;
        _adapter = null;
        _instance = null;

        if (_window != 0)
        {
            SDL.DestroyWindow(_window);
            _window = 0;
        }

        SDL.Quit();

        Console.WriteLine("Cleanup complete!");
    }

    #if BROWSER_WASM
    [DllImport("emscripten")]
    private static extern void emscripten_set_main_loop(Action callback, int fps, int simulateInfiniteLoop);

    private static void EmscriptenSetMainLoop(Action callback, int fps, int simulateInfiniteLoop)
    {
        emscripten_set_main_loop(callback, fps, simulateInfiniteLoop);
    }
    #endif
}
