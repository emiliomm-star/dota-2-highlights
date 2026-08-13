using DotaHighlights.Client.Capture;

namespace DotaHighlights.Client.Encoding;

public interface IClipEncoder
{
    /// <summary>
    /// Codifica los frames dados (JPEG en RAM) a un archivo de video y devuelve la ruta.
    /// </summary>
    Task<string> EncodeAsync(
        IReadOnlyList<CapturedFrame> frames,
        int fps,
        string outputPath,
        CancellationToken ct = default);
}
