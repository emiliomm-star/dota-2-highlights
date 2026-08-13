using System.IO;

namespace DotaHighlights.Client.Encoding;

/// <summary>
/// Localiza ffmpeg.exe de forma robusta, sin depender de que el proceso haya
/// heredado el PATH actualizado. Busca en el PATH del registro (usuario y
/// máquina) y en las ubicaciones típicas de instalación (winget, etc.).
/// </summary>
public static class FfmpegLocator
{
    public static string Resolve(string configured = "ffmpeg")
    {
        // 1. Si la config ya apunta a un archivo real, úsalo.
        if (!string.IsNullOrWhiteSpace(configured) &&
            configured.Contains(Path.DirectorySeparatorChar) && File.Exists(configured))
            return configured;

        foreach (var candidate in Candidates())
        {
            if (File.Exists(candidate)) return candidate;
        }

        // Último recurso: confiar en el PATH del proceso.
        return "ffmpeg";
    }

    private static IEnumerable<string> Candidates()
    {
        // PATH combinado de las tres fuentes (proceso, usuario, máquina).
        foreach (var scope in new[]
                 {
                     EnvironmentVariableTarget.Process,
                     EnvironmentVariableTarget.User,
                     EnvironmentVariableTarget.Machine,
                 })
        {
            var path = Environment.GetEnvironmentVariable("Path", scope);
            if (string.IsNullOrEmpty(path)) continue;
            foreach (var dir in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string p;
                try { p = Path.Combine(dir, "ffmpeg.exe"); } catch { continue; }
                yield return p;
            }
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Enlaces de winget.
        yield return Path.Combine(local, "Microsoft", "WinGet", "Links", "ffmpeg.exe");

        // Paquetes de winget (Gyan.FFmpeg, BtbN).
        var pkgRoot = Path.Combine(local, "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(pkgRoot))
        {
            IEnumerable<string> hits;
            try { hits = Directory.EnumerateFiles(pkgRoot, "ffmpeg.exe", SearchOption.AllDirectories); }
            catch { hits = Array.Empty<string>(); }
            foreach (var h in hits) yield return h;
        }
    }
}
