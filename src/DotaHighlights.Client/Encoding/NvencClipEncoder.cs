using System.Diagnostics;
using System.IO;
using DotaHighlights.Client.Capture;

namespace DotaHighlights.Client.Encoding;

/// <summary>
/// Codifica los frames del buffer a un .mp4 con NVENC (aceleración por GPU),
/// invocando ffmpeg.exe y pasándole los JPEG por la entrada estándar
/// (demuxer mjpeg -> encoder h264_nvenc).
/// </summary>
public sealed class NvencClipEncoder : IClipEncoder
{
    private readonly string _ffmpegPath;

    public NvencClipEncoder(string ffmpegPath = "ffmpeg") => _ffmpegPath = ffmpegPath;

    public async Task<string> EncodeAsync(
        IReadOnlyList<CapturedFrame> frames,
        int fps,
        string outputPath,
        CancellationToken ct = default)
    {
        if (frames.Count == 0)
            throw new InvalidOperationException("El buffer está vacío: no hay nada que guardar.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Entrada: secuencia de JPEG por stdin, a los fps de captura.
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-f"); psi.ArgumentList.Add("image2pipe");
        psi.ArgumentList.Add("-framerate"); psi.ArgumentList.Add(fps.ToString());
        psi.ArgumentList.Add("-c:v"); psi.ArgumentList.Add("mjpeg");
        psi.ArgumentList.Add("-i"); psi.ArgumentList.Add("-");
        // Salida: H.264 por hardware (NVENC).
        psi.ArgumentList.Add("-c:v"); psi.ArgumentList.Add("h264_nvenc");
        psi.ArgumentList.Add("-preset"); psi.ArgumentList.Add("p4");
        psi.ArgumentList.Add("-pix_fmt"); psi.ArgumentList.Add("yuv420p");
        psi.ArgumentList.Add(outputPath);

        using var proc = new Process { StartInfo = psi };

        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo iniciar ffmpeg ('{_ffmpegPath}'). ¿Está en el PATH? Detalle: {ex.Message}", ex);
        }

        // Captura stderr para diagnóstico si algo falla.
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        var stdin = proc.StandardInput.BaseStream;
        foreach (var frame in frames)
        {
            ct.ThrowIfCancellationRequested();
            await stdin.WriteAsync(frame.Jpeg, ct);
        }
        await stdin.FlushAsync(ct);
        proc.StandardInput.Close();

        await proc.WaitForExitAsync(ct);
        var stderr = await stderrTask;

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg terminó con código {proc.ExitCode}.\n{Tail(stderr, 1200)}");
        }

        return outputPath;
    }

    private static string Tail(string s, int max) =>
        s.Length <= max ? s : s[^max..];
}
