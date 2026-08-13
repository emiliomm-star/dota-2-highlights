using System.Diagnostics;
using System.Runtime.InteropServices;
using DotaHighlights.Client.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace DotaHighlights.Client.Capture;

/// <summary>
/// Captura real de pantalla con Windows.Graphics.Capture. Por defecto captura el
/// monitor principal; se puede capturar una ventana concreta (p. ej. Dota 2).
/// Cada frame se copia a una textura CPU, se comprime a JPEG y se emite,
/// limitando a los FPS objetivo para no saturar el buffer.
/// </summary>
public sealed class GraphicsCaptureSource : IFrameSource
{
    private const int MONITOR_DEFAULTTOPRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, int flags);

    private readonly IntPtr? _windowHandle;
    private readonly TimeSpan _frameInterval;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDirect3DDevice? _winrtDevice;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private ID3D11Texture2D? _staging;
    private SizeInt32 _lastSize;
    private readonly Stopwatch _sinceLastEmit = new();
    private readonly Lock _gate = new();
    private bool _running;

    public int Fps { get; }

    public event EventHandler<CapturedFrame>? FrameArrived;

    /// <param name="fps">FPS objetivo del buffer.</param>
    /// <param name="windowHandle">HWND a capturar; si es null, captura el monitor principal.</param>
    public GraphicsCaptureSource(int fps = 30, IntPtr? windowHandle = null)
    {
        Fps = fps;
        _frameInterval = TimeSpan.FromSeconds(1.0 / fps);
        _windowHandle = windowHandle;
    }

    public void Start()
    {
        if (_running) return;

        Vortice.Direct3D11.D3D11.D3D11CreateDevice(
            (Vortice.DXGI.IDXGIAdapter)null!, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            (FeatureLevel[])null!, out _device, out _context).CheckError();

        _winrtDevice = Direct3D11Interop.CreateWinRtDevice(_device!);

        _item = _windowHandle is { } hwnd
            ? Direct3D11Interop.CreateItemForWindow(hwnd)
            : Direct3D11Interop.CreateItemForMonitor(PrimaryMonitor());

        _item.Closed += OnItemClosed;
        _lastSize = _item.Size;

        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _lastSize);
        _framePool.FrameArrived += OnFrameArrived;

        _session = _framePool.CreateCaptureSession(_item);

        _sinceLastEmit.Restart();
        _session.StartCapture();
        _running = true;
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
        }

        _session?.Dispose(); _session = null;
        if (_framePool is not null) _framePool.FrameArrived -= OnFrameArrived;
        _framePool?.Dispose(); _framePool = null;
        if (_item is not null) _item.Closed -= OnItemClosed;
        _item = null;
        _staging?.Dispose(); _staging = null;
        _winrtDevice?.Dispose(); _winrtDevice = null;
        _context?.Dispose(); _context = null;
        _device?.Dispose(); _device = null;
    }

    private static IntPtr PrimaryMonitor()
        => MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);

    private void OnItemClosed(GraphicsCaptureItem sender, object args) => Stop();

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame is null) return;

        // Reajusta el pool si cambió la resolución del contenido.
        if (frame.ContentSize.Width != _lastSize.Width || frame.ContentSize.Height != _lastSize.Height)
        {
            _lastSize = frame.ContentSize;
            sender.Recreate(_winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _lastSize);
            _staging?.Dispose();
            _staging = null;
            return;
        }

        // Limita a los FPS objetivo (WGC entrega a la tasa de refresco del monitor).
        if (_sinceLastEmit.Elapsed < _frameInterval) return;
        _sinceLastEmit.Restart();

        lock (_gate)
        {
            if (!_running) return;
            try
            {
                var jpeg = CaptureJpeg(frame);
                if (jpeg is not null)
                    FrameArrived?.Invoke(this, new CapturedFrame(jpeg, DateTimeOffset.UtcNow));
            }
            catch
            {
                // Un frame fallido no debe tumbar la captura.
            }
        }
    }

    private byte[]? CaptureJpeg(Direct3D11CaptureFrame frame)
    {
        using var texture = Direct3D11Interop.GetTexture(frame.Surface);
        var desc = texture.Description;

        if (_staging is null)
        {
            _staging = _device!.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)desc.Width,
                Height = (uint)desc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = desc.Format,
                SampleDescription = new(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None,
            });
        }

        _context!.CopyResource(_staging, texture);

        int width = (int)desc.Width;
        int height = (int)desc.Height;
        var map = _context.Map(_staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            int dstStride = width * 4;
            var bgra = new byte[dstStride * height];
            for (int y = 0; y < height; y++)
            {
                IntPtr srcRow = map.DataPointer + y * (int)map.RowPitch;
                Marshal.Copy(srcRow, bgra, y * dstStride, dstStride);
            }
            return JpegEncoder.FromBgra(bgra, width, height, dstStride, quality: 80);
        }
        finally
        {
            _context.Unmap(_staging, 0);
        }
    }

    public void Dispose() => Stop();
}
