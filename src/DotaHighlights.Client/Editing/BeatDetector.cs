using System.Diagnostics;
using System.IO;
using Serilog;

namespace DotaHighlights.Client.Editing;

/// <summary>
/// Detección de beats sencilla basada en energía/onset: decodifica el audio a
/// PCM mono con ffmpeg y busca picos de aumento de energía (golpes). No es un
/// tracker de tempo perfecto, pero da flashes reactivos a la música.
/// </summary>
public sealed class BeatDetector
{
    private const int SampleRate = 11025;
    private const int Hop = 256;

    private readonly string _ffmpegPath;
    private readonly ILogger _log;

    public BeatDetector(string ffmpegPath, ILogger log)
    {
        _ffmpegPath = ffmpegPath;
        _log = log;
    }

    /// <summary>Devuelve los tiempos (segundos) de los beats dentro de [0, maxSeconds].</summary>
    public async Task<IReadOnlyList<double>> DetectAsync(
        string musicPath, double maxSeconds, CancellationToken ct = default)
    {
        short[] pcm;
        try { pcm = await DecodePcmAsync(musicPath, maxSeconds + 0.5, ct); }
        catch (Exception ex) { _log.Debug(ex, "Beat: no se pudo decodificar audio"); return Array.Empty<double>(); }

        int frames = pcm.Length / Hop;
        if (frames < 4) return Array.Empty<double>();

        // Envolvente de energía por ventana.
        var energy = new double[frames];
        for (int i = 0; i < frames; i++)
        {
            double sum = 0;
            int off = i * Hop;
            for (int j = 0; j < Hop; j++) { double s = pcm[off + j] / 32768.0; sum += s * s; }
            energy[i] = sum / Hop;
        }

        // Flux de onset = aumento positivo de energía.
        var flux = new double[frames];
        for (int i = 1; i < frames; i++)
            flux[i] = Math.Max(0, energy[i] - energy[i - 1]);

        // Umbral global (media + k·desviación).
        double mean = flux.Average();
        double std = Math.Sqrt(flux.Select(f => (f - mean) * (f - mean)).Average());
        double threshold = mean + 1.4 * std;

        double frameTime = (double)Hop / SampleRate;
        int minGap = (int)(0.22 / frameTime); // separación mínima entre beats
        int last = -minGap;

        var beats = new List<double>();
        for (int i = 1; i < frames - 1; i++)
        {
            if (flux[i] >= threshold && flux[i] >= flux[i - 1] && flux[i] >= flux[i + 1]
                && (i - last) >= minGap)
            {
                double t = i * frameTime;
                if (t > maxSeconds) break;
                beats.Add(Math.Round(t, 3));
                last = i;
            }
        }

        // Cap para no generar una expresión gigantesca.
        if (beats.Count > 80) beats = beats.Take(80).ToList();
        _log.Information("Beat: {Count} beats detectados en {Sec:0.0}s de música", beats.Count, maxSeconds);
        return beats;
    }

    private async Task<short[]> DecodePcmAsync(string musicPath, double seconds, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in new[]
                 {
                     "-hide_banner", "-loglevel", "error",
                     "-t", seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                     "-i", musicPath,
                     "-ac", "1", "-ar", SampleRate.ToString(),
                     "-f", "s16le", "-",
                 })
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        using var ms = new MemoryStream();
        var readTask = p.StandardOutput.BaseStream.CopyToAsync(ms, ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);
        await readTask;
        await p.WaitForExitAsync(ct);
        await errTask;

        var bytes = ms.ToArray();
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
        return samples;
    }
}
