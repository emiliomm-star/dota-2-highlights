namespace DotaHighlights.Client.Capture;

/// <summary>
/// Un frame capturado, ya comprimido a JPEG para ocupar poco en el buffer en RAM.
/// </summary>
/// <param name="Jpeg">Bytes de la imagen JPEG.</param>
/// <param name="Timestamp">Momento en que se capturó.</param>
public sealed record CapturedFrame(byte[] Jpeg, DateTimeOffset Timestamp);
