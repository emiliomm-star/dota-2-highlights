namespace DotaHighlights.Client.Capture;

/// <summary>
/// Fuente de frames (captura de pantalla, ventana, o fuente sintética de prueba).
/// Emite <see cref="FrameArrived"/> por cada frame ya comprimido a JPEG.
/// </summary>
public interface IFrameSource : IDisposable
{
    event EventHandler<CapturedFrame>? FrameArrived;

    /// <summary>Frames por segundo a los que emite la fuente.</summary>
    int Fps { get; }

    void Start();
    void Stop();
}
