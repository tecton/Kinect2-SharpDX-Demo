/* Modified on SharpDX Demo
 * Ning Tang
 * 2015/1/20
 */
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace DX11_Kinect
{
    class Monitor
    {
        public RenderForm form = new RenderForm("Monitor");

        Device device;
        DeviceContext context;

        SwapChainDescription swapChainDesc;
        SwapChain swapChain;

        Texture2D backBuffer;
        RenderTargetView renderView;
        Texture2D depthBuffer;
        DepthStencilView depthView;
        Texture2D depthMapTexture;
        ShaderResourceView depthMapView;

        ShaderBytecode vertexShaderByteCode;
        VertexShader vertexShader;
        ShaderBytecode pixelShaderByteCode;
        PixelShader pixelShader;

        InputLayout layout;
        Buffer vertices;
        Buffer contantBuffer;

        Matrix worldViewProj;
        Factory factory;

        DataStream stream;
        byte[] depthPixels;
        int depthMapWidth, depthMapHeight;

        public delegate void MonitorRenderCallback();

        public void Dispose()
        {
            // Release all resources
            vertexShaderByteCode.Dispose();
            vertexShader.Dispose();
            pixelShaderByteCode.Dispose();
            pixelShader.Dispose();
            vertices.Dispose();
            layout.Dispose();
            renderView.Dispose();
            backBuffer.Dispose();
            context.ClearState();
            context.Flush();
            device.Dispose();
            context.Dispose();
            swapChain.Dispose();
            factory.Dispose();
        }

        public void init()
        {
            // SwapChain description
            swapChainDesc = new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription =
                    new ModeDescription(form.ClientSize.Width, form.ClientSize.Height,
                                        new Rational(60, 1), Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = form.Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            // Create Device and SwapChain
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.Debug, swapChainDesc, out device, out swapChain);

            // Ignore all windows events
            factory = swapChain.GetParent<Factory>();
            factory.MakeWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAll);

            // New RenderTargetView from the backbuffer
            backBuffer = Texture2D.FromSwapChain<Texture2D>(swapChain, 0);
            renderView = new RenderTargetView(device, backBuffer);

            // Compile Vertex and Pixel shaders
            vertexShaderByteCode = ShaderBytecode.CompileFromFile("shader.fx", "VS", "vs_4_0");
            vertexShader = new VertexShader(device, vertexShaderByteCode);

            pixelShaderByteCode = ShaderBytecode.CompileFromFile("shader.fx", "PS", "ps_4_0");
            pixelShader = new PixelShader(device, pixelShaderByteCode);

            // Layout from VertexShader input signature
            layout = new InputLayout(device, ShaderSignature.GetInputSignature(vertexShaderByteCode), new[]
                    {
                        new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                        new InputElement("TEXCOORD", 0, Format.R32G32_Float, 16, 0)
                    });

            // Instantiate Vertex buiffer from vertex data
            vertices = Buffer.Create(device, BindFlags.VertexBuffer, new[]
                                  {
                                      // 3D coordinates              UV Texture coordinates
                                      -1.0f, -1.0f, -1.0f, 1.0f,     0.0f, 1.0f,
                                      -1.0f,  1.0f, -1.0f, 1.0f,     0.0f, 0.0f,
                                       1.0f,  1.0f, -1.0f, 1.0f,     1.0f, 0.0f,
                                      -1.0f, -1.0f, -1.0f, 1.0f,     0.0f, 1.0f,
                                       1.0f,  1.0f, -1.0f, 1.0f,     1.0f, 0.0f,
                                       1.0f, -1.0f, -1.0f, 1.0f,     1.0f, 1.0f,
                            });

            // Create Constant Buffer
            contantBuffer = new Buffer(device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            // Create Depth Buffer & View
            depthBuffer = new Texture2D(device, new Texture2DDescription()
            {
                Format = Format.D32_Float_S8X24_UInt,
                ArraySize = 1,
                MipLevels = 1,
                Width = form.ClientSize.Width,
                Height = form.ClientSize.Height,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            depthView = new DepthStencilView(device, depthBuffer);

            var sampler = new SamplerState(device, new SamplerStateDescription()
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = Color.Black,
                ComparisonFunction = Comparison.Never,
                MaximumAnisotropy = 16,
                MipLodBias = 0,
                MinimumLod = 0,
                MaximumLod = 16,
            });

            // Prepare All the stages
            context = device.ImmediateContext;
            context.InputAssembler.InputLayout = layout;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertices, Utilities.SizeOf<Vector4>() + Utilities.SizeOf<Vector2>(), 0));
            context.VertexShader.SetConstantBuffer(0, contantBuffer);
            context.VertexShader.Set(vertexShader);
            context.Rasterizer.SetViewport(new Viewport(0, 0, form.ClientSize.Width, form.ClientSize.Height, 0.0f, 1.0f));
            context.PixelShader.Set(pixelShader);
            context.PixelShader.SetSampler(0, sampler);
            context.OutputMerger.SetTargets(depthView, renderView);
        }

        public void render(MonitorRenderCallback callback)
        {
            RenderLoop.Run(form, () =>
            {
                callback();

                // Clear views
                context.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                context.ClearRenderTargetView(renderView, Color.Black);

                DataBox databox = context.MapSubresource(depthMapTexture, 0, MapMode.WriteDiscard, MapFlags.None, out stream);

                if (!databox.IsEmpty)
                    stream.Write(depthPixels, 0, 4 * depthMapWidth * depthMapHeight);
                context.UnmapSubresource(depthMapTexture, 0);

                // Draw the square
                context.Draw(6, 0);

                // Present!
                swapChain.Present(0, PresentFlags.None);
            });
        }

        public void setDepthMapSize(int width, int height)
        {
            depthMapWidth = width;
            depthMapHeight = height;
            depthMapTexture = new Texture2D(device, new Texture2DDescription()
            {
                Format = Format.R8G8B8A8_UNorm,
                ArraySize = 1,
                MipLevels = 1,
                Width = width,
                Height = height,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
            });
            depthMapView = new ShaderResourceView(device, depthMapTexture);
            context.PixelShader.SetShaderResource(0, depthMapView);
            stream = new DataStream(4 * width * height, true, true);
            depthPixels = new byte[width * height * 4];
            for (int i = 0; i < width * height; ++i)
            {
                // set alpha to 1 once time
                depthPixels[4 * i + 3] = 1;
            }
        }

        public void renewDepthmap(byte[] depthmap)
        {
            for (int i = 0; i < depthMapWidth * depthMapHeight; ++i)
            {
                // R = G = B
                depthPixels[4 * i + 1] = depthPixels[4 * i + 2] = depthPixels[4 * i] = depthmap[i];
            }
        }

        public void setDepthMapMatrix(Matrix viewProj)
        {
            worldViewProj = viewProj;
            worldViewProj.Transpose();
            context.UpdateSubresource(ref worldViewProj, contantBuffer);
        }
    }
}
