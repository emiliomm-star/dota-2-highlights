using System.IO;
using DotaHighlights.Client.Capture;
using DotaHighlights.Client.Editing;
using DotaHighlights.Client.Encoding;
using DotaHighlights.Client.Gsi;
using Serilog;

namespace DotaHighlights.Client.Recording;

/// <summary>
/// Orquesta el pipeline de la Fase 1: la fuente alimenta el buffer circular en
/// RAM de forma continua; cuando llega un gatillo (manual ahora, IA en Fase 2)
/// se vuelca el buffer a un .mp4 con NVENC.
/// </summary>
public sealed class HighlightRecorder : IDisposable
{
    private readonly IFrameSource _source;
    private readonly RingBuffer _buffer;
    private readonly IClipEncoder _encoder;
    private readonly VideoEditor _editor;
    private readonly AppSettings _settings;
    private readonly GameState _gameState;
    private readonly ILogger _log;
    private volatile bool _running;

    public HighlightRecorder(
        IFrameSource source,
        IClipEncoder encoder,
        AppSettings settings,
        GameState gameState,
        ILogger log)
    {
        _source = source;
        _encoder = encoder;
        _settings = settings;
        _gameState = gameState;
        _log = log;
        _editor = new VideoEditor(settings.FfmpegPath, log);
        _buffer = new RingBuffer(TimeSpan.FromSeconds(settings.BufferSeconds));
    }

    public bool IsRunning => _running;
    public int BufferedFrames => _buffer.Count;
    public long BufferBytes => _buffer.ApproxBytes;

    /// <summary>Se emite cuando un clip queda guardado (ruta del archivo).</summary>
    public event Action<string>? ClipSaved;

    /// <summary>Se emite al empezar a generar las ediciones derivadas.</summary>
    public event Action? EditsStarted;

    /// <summary>Se emite cada vez que UNA edición queda lista.</summary>
    public event Action<EditedClip>? EditReady;

    /// <summary>Se emite cuando terminan todas (con el total generado).</summary>
    public event Action<int>? EditsCompleted;

    public void Start()
    {
        if (_running) return;
        _buffer.Window = TimeSpan.FromSeconds(_settings.BufferSeconds);
        _source.FrameArrived += OnFrame;
        _source.Start();
        _running = true;
        _log.Information("Captura iniciada ({Fps} fps, buffer {Sec}s)", _source.Fps, _settings.BufferSeconds);
    }

    public void Stop()
    {
        if (!_running) return;
        _source.Stop();
        _source.FrameArrived -= OnFrame;
        _running = false;
        _log.Information("Captura detenida");
    }

    private void OnFrame(object? sender, CapturedFrame frame) => _buffer.Add(frame);

    /// <summary>Vuelca el contenido actual del buffer a un archivo .mp4.</summary>
    /// <param name="reason">Motivo del highlight (para el overlay de texto).</param>
    public async Task<string> SaveHighlightAsync(string reason = "Manual", CancellationToken ct = default)
    {
        var frames = _buffer.Snapshot();
        if (frames.Count == 0)
            throw new InvalidOperationException("El buffer está vacío. ¿La captura está corriendo?");

        var name = $"highlight_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        var path = Path.Combine(_settings.OutputFolder, name);

        _log.Information("Guardando highlight: {Frames} frames -> {Path}", frames.Count, path);
        await _encoder.EncodeAsync(frames, _source.Fps, path, ct);
        _log.Information("Highlight guardado: {Path}", path);

        ClipSaved?.Invoke(path);

        // Genera las ediciones derivadas en segundo plano (no bloquea el guardado).
        _ = GenerateEditsAsync(path, frames.Count, reason);
        return path;
    }

    private async Task GenerateEditsAsync(string source, int frameCount, string reason)
    {
        try
        {
            EditsStarted?.Invoke();
            double duration = frameCount / (double)Math.Max(1, _source.Fps);
            // El momento clave (kill) es ~postRoll segundos antes del final.
            double moneyShot = duration - _settings.PostRollSeconds;
            var overlay = new OverlayInfo(BuildTitle(reason), _gameState.HeroDisplay, _gameState.Kills);
            var progress = new Progress<EditedClip>(clip => EditReady?.Invoke(clip));
            var edits = await _editor.EditAllAsync(source, moneyShot, _settings.MusicPath, overlay, progress);
            _log.Information("Ediciones generadas: {Count}", edits.Count);
            EditsCompleted?.Invoke(edits.Count);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Fallo generando ediciones derivadas");
        }
    }

    /// <summary>Texto grande del overlay a partir del motivo del highlight.</summary>
    private static string BuildTitle(string reason)
    {
        if (reason.Contains("kill", StringComparison.OrdinalIgnoreCase))
            return reason.ToUpperInvariant();
        if (reason.StartsWith("Hotkey", StringComparison.OrdinalIgnoreCase))
            return "HIGHLIGHT";
        return reason.ToUpperInvariant();
    }

    public void Dispose()
    {
        Stop();
        _source.Dispose();
    }
}
