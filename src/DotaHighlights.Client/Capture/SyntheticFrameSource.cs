using System.Diagnostics;
using DotaHighlights.Client.Imaging;

namespace DotaHighlights.Client.Capture;

/// <summary>
/// Fuente de prueba: genera frames con una barra de color en movimiento.
/// Sirve para validar el pipeline buffer -> mp4 sin depender de la captura real.
/// </summary>
public sealed class SyntheticFrameSource : IFrameSource
{
    private readonly int _width;
    private readonly int _height;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public SyntheticFrameSource(int width = 1280, int height = 720, int fps = 30)
    {
        _width = width;
        _height = height;
        Fps = fps;
    }

    public int Fps { get; }

    public event EventHandler<CapturedFrame>? FrameArrived;

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _loop = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var frameInterval = TimeSpan.FromSeconds(1.0 / Fps);
        int stride = _width * 4;
        var bgra = new byte[stride * _height];
        var sw = Stopwatch.StartNew();
        long frameIndex = 0;

        while (!ct.IsCancellationRequested)
        {
            var target = frameInterval * frameIndex;
            var wait = target - sw.Elapsed;
            if (wait > TimeSpan.Zero)
            {
                try { await Task.Delay(wait, ct); } catch (OperationCanceledException) { break; }
            }

            DrawFrame(bgra, stride, (int)(frameIndex % _width));
            var jpeg = JpegEncoder.FromBgra(bgra, _width, _height, stride, quality: 80);
            FrameArrived?.Invoke(this, new CapturedFrame(jpeg, DateTimeOffset.UtcNow));
            frameIndex++;
        }
    }

    private void DrawFrame(byte[] bgra, int stride, int barX)
    {
        // Fondo azul oscuro con una barra vertical amarilla que se desplaza.
        for (int y = 0; y < _height; y++)
        {
            int row = y * stride;
            for (int x = 0; x < _width; x++)
            {
                int i = row + x * 4;
                bool bar = x >= barX && x < barX + 40;
                bgra[i + 0] = (byte)(bar ? 0 : 60);    // B
                bgra[i + 1] = (byte)(bar ? 220 : 30);  // G
                bgra[i + 2] = (byte)(bar ? 255 : 10);  // R
                bgra[i + 3] = 255;                     // A
            }
        }
    }

    public void Dispose() => Stop();
}
