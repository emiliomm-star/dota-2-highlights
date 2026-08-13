using System.Diagnostics;
using System.Globalization;
using System.IO;
using Serilog;

namespace DotaHighlights.Client.Editing;

/// <summary>
/// Genera versiones editadas "con aura" de un highlight usando ffmpeg (NVENC):
/// slow-motion en el momento clave, zoom punch, y un grado cinematográfico.
/// El momento clave se conoce gracias a GSI (cuándo fue el kill).
/// Opcionalmente mezcla una pista de música elegida por el usuario.
/// </summary>
public sealed class VideoEditor
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly ILogger _log;

    public VideoEditor(string ffmpegPath, ILogger log)
    {
        _ffmpegPath = ffmpegPath;
        _log = log;
        var dir = Path.GetDirectoryName(ffmpegPath);
        _ffprobePath = string.IsNullOrEmpty(dir) ? "ffprobe" : Path.Combine(dir, "ffprobe.exe");
    }

    /// <param name="source">Ruta del highlight original.</param>
    /// <param name="moneyShotSeconds">Segundo aproximado del momento clave (kill).</param>
    /// <param name="musicPath">Ruta a un audio a mezclar (o null/"" para sin música).</param>
    public async Task<IReadOnlyList<EditedClip>> EditAllAsync(
        string source, double moneyShotSeconds, string? musicPath = null, CancellationToken ct = default)
    {
        double dur = await ProbeDurationAsync(source, ct);
        if (dur <= 1) dur = 6;

        double m = Math.Clamp(moneyShotSeconds, 0.8, Math.Max(0.8, dur - 0.5));
        double s0 = Math.Clamp(m - 0.7, 0.3, dur - 0.6);
        double s1 = Math.Clamp(m + 1.3, s0 + 0.4, dur - 0.2);
        double z1 = Math.Clamp(m + 1.8, s0 + 0.5, dur - 0.2);
        double fadeOut = Math.Max(0.2, dur - 0.6);

        string? music = !string.IsNullOrWhiteSpace(musicPath) && File.Exists(musicPath) ? musicPath : null;

        var outDir = Path.Combine(Path.GetDirectoryName(source)!, "edited");
        Directory.CreateDirectory(outDir);
        string b = Path.GetFileNameWithoutExtension(source);

        var jobs = new (string id, string name, string outFile, string filter)[]
        {
            ("slowmo", "🐢 Slow-mo", $"{b}_slowmo.mp4",
                $"[0:v]trim=0:{F(s0)},setpts=PTS-STARTPTS[a];" +
                $"[0:v]trim={F(s0)}:{F(s1)},setpts=(PTS-STARTPTS)/0.35,eq=saturation=1.2:contrast=1.05[bb];" +
                $"[0:v]trim={F(s1)},setpts=PTS-STARTPTS[c];" +
                $"[a][bb][c]concat=n=3:v=1[v]"),

            ("zoom", "🔍 Zoom épico", $"{b}_zoom.mp4",
                $"[0:v]trim=0:{F(s0)},setpts=PTS-STARTPTS,eq=saturation=1.25:contrast=1.08[a];" +
                $"[0:v]trim={F(s0)}:{F(z1)},setpts=PTS-STARTPTS,scale=w=iw*1.3:h=ih*1.3,crop=iw/1.3:ih/1.3,eq=saturation=1.4:contrast=1.12[bb];" +
                $"[0:v]trim={F(z1)},setpts=PTS-STARTPTS,eq=saturation=1.25:contrast=1.08[c];" +
                $"[a][bb][c]concat=n=3:v=1[v]"),

            ("cinematic", "🎞️ Cinematic", $"{b}_cinematic.mp4",
                "[0:v]eq=contrast=1.12:saturation=1.15:gamma=0.95," +
                "drawbox=y=0:w=iw:h=ih*0.10:color=black@1:t=fill," +
                "drawbox=y=ih*0.90:w=iw:h=ih*0.10:color=black@1:t=fill," +
                $"fade=t=in:st=0:d=0.6,fade=t=out:st={F(fadeOut)}:d=0.6[v]"),
        };

        var results = new List<EditedClip>();
        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            var outPath = Path.Combine(outDir, job.outFile);
            _log.Information("Editando ({Style}{Music}) -> {Out}",
                job.id, music is null ? "" : " + música", outPath);
            var args = BuildArgs(source, outPath, job.filter, music);
            int exit = await RunAsync(_ffmpegPath, args, ct);
            if (exit == 0 && File.Exists(outPath))
                results.Add(new EditedClip(job.id, job.name, outPath));
            else
                _log.Warning("Edición {Style} falló (exit {Exit})", job.id, exit);
        }
        return results;
    }

    private static string F(double v) => v.ToString("0.###", Inv);

    /// <summary>
    /// Construye los argumentos de ffmpeg. Entrada 0 = video; si hay música,
    /// entrada 1 = audio (en bucle infinito, recortado a la duración del video).
    /// </summary>
    private static string[] BuildArgs(string src, string dst, string filter, string? music)
    {
        var a = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-i", src };

        if (music is not null)
        {
            a.Add("-stream_loop"); a.Add("-1");   // repite la canción si es más corta
            a.Add("-i"); a.Add(music);
        }

        a.Add("-filter_complex"); a.Add(filter);
        a.Add("-map"); a.Add("[v]");

        if (music is not null)
        {
            a.Add("-map"); a.Add("1:a");
            a.Add("-c:a"); a.Add("aac");
            a.Add("-shortest");                   // corta el audio al final del video
        }
        else
        {
            a.Add("-an");
        }

        a.Add("-c:v"); a.Add("h264_nvenc");
        a.Add("-preset"); a.Add("p5");
        a.Add("-pix_fmt"); a.Add("yuv420p");
        a.Add(dst);
        return a.ToArray();
    }

    private async Task<double> ProbeDurationAsync(string source, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in new[]
                     {
                         "-v", "error", "-show_entries", "format=duration",
                         "-of", "default=noprint_wrappers=1:nokey=1", source,
                     })
                psi.ArgumentList.Add(a);

            using var p = Process.Start(psi)!;
            string outp = await p.StandardOutput.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            return double.TryParse(outp.Trim(), NumberStyles.Float, Inv, out double d) ? d : 0;
        }
        catch { return 0; }
    }

    private async Task<int> RunAsync(string exe, string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        string err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(err))
            _log.Debug("ffmpeg edit stderr: {Err}", err.Length > 500 ? err[^500..] : err);
        return p.ExitCode;
    }
}
