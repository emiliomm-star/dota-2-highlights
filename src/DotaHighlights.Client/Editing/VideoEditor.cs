using System.Diagnostics;
using System.Globalization;
using System.IO;
using Serilog;

namespace DotaHighlights.Client.Editing;

/// <summary>
/// Genera versiones editadas "con aura" de un highlight aplicando presets
/// (ver <see cref="EditPresets"/>) con ffmpeg + NVENC. El momento clave se
/// conoce gracias a GSI. Opcionalmente mezcla una pista de música.
/// </summary>
public sealed class VideoEditor
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly ILogger _log;
    private readonly string _fontsDir;
    private readonly string _fontFile; // nombre relativo (sin ruta) o "" si no hay

    public VideoEditor(string ffmpegPath, ILogger log)
    {
        _ffmpegPath = ffmpegPath;
        _log = log;
        var dir = Path.GetDirectoryName(ffmpegPath);
        _ffprobePath = string.IsNullOrEmpty(dir) ? "ffprobe" : Path.Combine(dir, "ffprobe.exe");

        // Copiamos una fuente a una carpeta propia y la referenciamos por nombre
        // relativo (ffmpeg tiene problemas con el ':' de rutas absolutas en drawtext).
        _fontsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dota2Highlights", "fonts");
        Directory.CreateDirectory(_fontsDir);
        _fontFile = EnsureFont();
    }

    private string EnsureFont()
    {
        foreach (var name in new[] { "impact.ttf", "ariblk.ttf", "arialbd.ttf" })
        {
            var dst = Path.Combine(_fontsDir, name);
            if (File.Exists(dst)) return name;
            var win = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), name);
            if (File.Exists(win))
            {
                try { File.Copy(win, dst, overwrite: true); return name; } catch { }
            }
        }
        _log.Warning("No se encontró una fuente para los overlays de texto");
        return "";
    }

    /// <param name="source">Ruta del highlight original.</param>
    /// <param name="moneyShotSeconds">Segundo aproximado del momento clave (kill).</param>
    /// <param name="musicPath">Audio a mezclar (o null/"" para sin música).</param>
    /// <param name="onEach">Se invoca cada vez que una edición queda lista.</param>
    public async Task<IReadOnlyList<EditedClip>> EditAllAsync(
        string source, double moneyShotSeconds, string? musicPath = null,
        OverlayInfo? overlay = null,
        IProgress<EditedClip>? onEach = null, CancellationToken ct = default)
    {
        double dur = await ProbeDurationAsync(source, ct);
        if (dur <= 1) dur = 6;

        double m = Math.Clamp(moneyShotSeconds, 0.8, Math.Max(0.8, dur - 0.5));
        var ctx = new EditContext
        {
            Duration = dur,
            MoneyShot = m,
            SlowStart = Math.Clamp(m - 0.7, 0.3, dur - 0.6),
            SlowEnd = Math.Clamp(m + 1.3, Math.Clamp(m - 0.7, 0.3, dur - 0.6) + 0.4, dur - 0.2),
            ZoomEnd = Math.Clamp(m + 1.8, Math.Clamp(m - 0.7, 0.3, dur - 0.6) + 0.5, dur - 0.2),
            FadeOut = Math.Max(0.2, dur - 0.6),
        };

        string? music = !string.IsNullOrWhiteSpace(musicPath) && File.Exists(musicPath) ? musicPath : null;

        var outDir = Path.Combine(Path.GetDirectoryName(source)!, "edited");
        Directory.CreateDirectory(outDir);
        string b = Path.GetFileNameWithoutExtension(source);

        // Genera primero los presets rápidos (rank alto) y el pesado al final,
        // pero conserva el Rank para que la UI los ordene por recomendación.
        var results = new List<EditedClip>();
        foreach (var preset in EditPresets.All.OrderByDescending(p => p.Rank))
        {
            ct.ThrowIfCancellationRequested();
            var outPath = Path.Combine(outDir, $"{b}_{preset.Id}.mp4");
            _log.Information("Editando ({Style}{Music}) -> {Out}",
                preset.Id, music is null ? "" : " + música", outPath);

            // El preset produce [vbase]; encima aplicamos el overlay (o passthrough).
            string overlayChain = BuildOverlay(overlay);
            string filter = preset.BuildFilter(ctx) + ";[vbase]" + overlayChain + "[v]";
            var args = BuildArgs(source, outPath, filter, music);
            var sw = Stopwatch.StartNew();
            int exit = await RunAsync(_ffmpegPath, args, ct);
            sw.Stop();

            if (exit == 0 && File.Exists(outPath))
            {
                var clip = new EditedClip(preset.Id, preset.Name, outPath, preset.Rank);
                results.Add(clip);
                onEach?.Report(clip);
                _log.Information("Edición {Style} lista en {Sec:0.0}s", preset.Id, sw.Elapsed.TotalSeconds);
            }
            else
            {
                _log.Warning("Edición {Style} falló (exit {Exit})", preset.Id, exit);
            }
        }
        return results;
    }

    /// <summary>Cadena de filtros drawtext para el overlay, o "null" (passthrough).</summary>
    private string BuildOverlay(OverlayInfo? ov)
    {
        if (ov is null || string.IsNullOrEmpty(_fontFile)) return "null";

        var parts = new List<string>();

        string title = Sanitize(ov.Title);
        if (title.Length > 0)
        {
            const string fade = "if(lt(t,0.2),0,if(lt(t,0.6),(t-0.2)/0.4,if(lt(t,2.8),1,if(lt(t,3.2),(3.2-t)/0.4,0))))";
            parts.Add($"drawtext=fontfile={_fontFile}:text={title}:fontsize=h/9:fontcolor=white:" +
                      $"borderw=6:bordercolor=black:x=(w-text_w)/2:y=h*0.10:alpha='{fade}'");
        }

        string sub = Sanitize(ov.Hero);
        if (ov.Kills > 0) sub = (sub + $"  x{ov.Kills}").Trim();
        if (sub.Length > 0)
            parts.Add($"drawtext=fontfile={_fontFile}:text={sub}:fontsize=h/26:fontcolor=white:" +
                      "borderw=3:bordercolor=black:x=64:y=h-h*0.14");

        return parts.Count > 0 ? string.Join(",", parts) : "null";
    }

    /// <summary>Deja solo letras, dígitos y espacios (evita romper el filtergraph).</summary>
    private static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var chars = s.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray();
        return new string(chars).Trim();
    }

    private static string[] BuildArgs(string src, string dst, string filter, string? music)
    {
        var a = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-i", src };

        if (music is not null)
        {
            a.Add("-stream_loop"); a.Add("-1");
            a.Add("-i"); a.Add(music);
        }

        a.Add("-filter_complex"); a.Add(filter);
        a.Add("-map"); a.Add("[v]");

        if (music is not null)
        {
            a.Add("-map"); a.Add("1:a");
            a.Add("-c:a"); a.Add("aac");
            a.Add("-shortest");
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
            // Para que fontfile=<nombre> (relativo) se resuelva sin problemas con ':'.
            WorkingDirectory = _fontsDir,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        string err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0 && !string.IsNullOrWhiteSpace(err))
            _log.Debug("ffmpeg edit stderr: {Err}", err.Length > 800 ? err[^800..] : err);
        return p.ExitCode;
    }
}
