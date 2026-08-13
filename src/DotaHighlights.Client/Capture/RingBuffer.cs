using System.Diagnostics;

namespace DotaHighlights.Client.Capture;

/// <summary>
/// Buffer circular en RAM que conserva los últimos N segundos de frames.
/// Al añadir un frame descarta automáticamente los más viejos que la ventana.
/// Thread-safe: la fuente escribe desde un hilo de captura y el guardado
/// toma una instantánea desde otro.
/// </summary>
public sealed class RingBuffer
{
    private readonly Queue<CapturedFrame> _frames = new();
    private readonly Lock _gate = new();
    private TimeSpan _window;

    public RingBuffer(TimeSpan window) => _window = window;

    /// <summary>Segundos de historia que conserva el buffer.</summary>
    public TimeSpan Window
    {
        get { lock (_gate) return _window; }
        set { lock (_gate) _window = value; }
    }

    public int Count { get { lock (_gate) return _frames.Count; } }

    /// <summary>Bytes aproximados que ocupa el buffer en RAM.</summary>
    public long ApproxBytes { get { lock (_gate) return _totalBytes; } }
    private long _totalBytes;

    public void Add(CapturedFrame frame)
    {
        lock (_gate)
        {
            _frames.Enqueue(frame);
            _totalBytes += frame.Jpeg.Length;

            var cutoff = frame.Timestamp - _window;
            while (_frames.Count > 0 && _frames.Peek().Timestamp < cutoff)
            {
                _totalBytes -= _frames.Dequeue().Jpeg.Length;
            }
        }
    }

    /// <summary>Copia inmutable del contenido actual, en orden cronológico.</summary>
    public IReadOnlyList<CapturedFrame> Snapshot()
    {
        lock (_gate)
        {
            return _frames.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _frames.Clear();
            _totalBytes = 0;
        }
    }
}
