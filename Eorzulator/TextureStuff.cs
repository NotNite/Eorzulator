using System;
using System.Runtime.InteropServices;
using LibRetriX;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = System.Buffer;
using Device = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace Eorzulator;

public class TextureStuff {
    private const uint LookupTableSize = ushort.MaxValue + 1;
    private static readonly uint[] Rgb0555LookupTable = new uint[LookupTableSize];
    private static readonly uint[] Rgb565LookupTable = new uint[LookupTableSize];

    static TextureStuff() {
        uint r, g, b;
        for (uint i = 0; i < LookupTableSize; i++) {
            r = (i >> 11) & 0x1F;
            g = (i >> 5) & 0x3F;
            b = (i & 0x1F);

            r = (uint) Math.Round(r * 255.0 / 31.0);
            g = (uint) Math.Round(g * 255.0 / 63.0);
            b = (uint) Math.Round(b * 255.0 / 31.0);

            Rgb565LookupTable[i] = 0xFF000000 | r << 16 | g << 8 | b;
        }

        for (uint i = 0; i < LookupTableSize; i++) {
            r = (i >> 10) & 0x1F;
            g = (i >> 5) & 0x1F;
            b = (i & 0x1F);

            r = (uint) Math.Round(r * 255.0 / 31.0);
            g = (uint) Math.Round(g * 255.0 / 31.0);
            b = (uint) Math.Round(b * 255.0 / 31.0);

            Rgb0555LookupTable[i] = 0xFF000000 | r << 16 | g << 8 | b;
        }
    }

    public static unsafe EmulatorTexture GetTexture(int width, int height) {
        var device = Services.PluginInterface.UiBuilder.Device;
        var desc = new Texture2DDescription {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.R8G8B8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.ShaderResource,
            CpuAccessFlags = CpuAccessFlags.Write,
            OptionFlags = ResourceOptionFlags.None
        };

        var bytes = new byte[width * height * 4];
        fixed (byte* pixelData = bytes) {
            var texture = new Texture2D(device, desc,
                                        new DataRectangle(new IntPtr(pixelData), width * 4));
            var resView = new ShaderResourceView(device, texture, new ShaderResourceViewDescription {
                Format = desc.Format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = {MipLevels = desc.MipLevels}
            });

            return new EmulatorTexture(resView, texture);
        }
    }

    public unsafe class EmulatorTexture(ShaderResourceView view, Texture2D tex) : IDisposable {
        public int Width => tex.Description.Width;
        public int Height => tex.Description.Height;
        public nint Handle => view.NativePointer;
        private DeviceContext context = new((nint) Device.Instance()->D3D11DeviceContext);

        public void Mutate(Action<DataBox, DataStream> mutate) {
            var box = this.context.MapSubresource(tex, 0, MapMode.WriteDiscard, MapFlags.None, out var stream);
            try {
                mutate(box, stream);
            } finally {
                this.context.UnmapSubresource(tex, 0);
            }
        }

        public void Dispose() {
            view.Dispose();
            tex.Dispose();
        }
    }

    public static unsafe void CopyTexture2D(
        byte[] src,
        byte* dst,
        uint width,
        uint height,
        int pixelWidth,
        uint rowPitch
    ) {
        if (src.Length < width * height * pixelWidth) {
            throw new InvalidOperationException("The source buffer is too short to copy.");
        }

        fixed (byte* s1 = src) {
            var s2 = s1;

            // Perform a row-by-row copy of the source image to the destination texture
            var rowSize = width * pixelWidth;
            for (var i = 0; i < height; i++) {
                Buffer.MemoryCopy(s2, dst, rowSize, rowSize);
                dst += rowPitch;
                s2 += rowSize;
            }
        }
    }

    public static byte[] FixTexture(byte[] orig, uint width, uint height, uint pitch, PixelFormats format) {
        switch (format) {
            case PixelFormats.XRGB8888: {
                FixXrgb8888(orig);
                return orig;
            }
            
            case PixelFormats.RGB0555: {
                var bytes = new byte[width * height * 4];
                ConvertFrameBufferUshortToXrgb8888WithLut(width, height, orig, (int) pitch, bytes, (int) width * 4,
                                                          Rgb0555LookupTable);
                FixXrgb8888(bytes);
                return bytes;
            }

            case PixelFormats.RGB565: {
                var bytes = new byte[width * height * 4];
                ConvertFrameBufferUshortToXrgb8888WithLut(width, height, orig, (int) pitch, bytes, (int) width * 4,
                                                          Rgb565LookupTable);
                FixXrgb8888(bytes);
                return bytes;
            }

            default: {
                return orig;
            }
        }
    }

    private static void FixXrgb8888(byte[] bytes) {
        for (var i = 0; i < bytes.Length; i += 4) {
            var (r, g, b) = (bytes[i], bytes[i + 1], bytes[i + 2]);
            bytes[i] = b;
            bytes[i + 1] = g;
            bytes[i + 2] = r;
            bytes[i + 3] = 255;
        }
    }

    private static void ConvertFrameBufferUshortToXrgb8888WithLut(
        uint width, uint height, ReadOnlySpan<byte> input, int inputPitch, Span<byte> output, int outputPitch,
        ReadOnlySpan<uint> lutPtr
    ) {
        var castInput = MemoryMarshal.Cast<byte, ushort>(input);
        var castInputPitch = inputPitch / sizeof(ushort);
        var castOutput = MemoryMarshal.Cast<byte, uint>(output);
        var castOutputPitch = outputPitch / sizeof(uint);

        for (var i = 0; i < height; i++) {
            var inputLine = castInput.Slice(i * castInputPitch, castInputPitch);
            var outputLine = castOutput.Slice(i * castOutputPitch, castOutputPitch);
            for (var j = 0; j < width; j++) {
                outputLine[j] = lutPtr[inputLine[j]];
            }
        }
    }
}
