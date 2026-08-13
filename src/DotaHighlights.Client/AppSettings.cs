using System.IO;

namespace DotaHighlights.Client;

/// <summary>Configuración del MVP. En fases posteriores se persistirá a disco.</summary>
public sealed class AppSettings
{
    /// <summary>Segundos de historia que conserva el buffer (lo que se guarda al disparar).</summary>
    public int BufferSeconds { get; set; } = 15;

    public int Fps { get; set; } = 30;

    public int CaptureWidth { get; set; } = 1280;
    public int CaptureHeight { get; set; } = 720;

    /// <summary>Carpeta donde se guardan los clips.</summary>
    public string OutputFolder { get; set; } = DefaultOutputFolder();

    /// <summary>Ruta a ffmpeg.exe (o "ffmpeg" si está en el PATH).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    private static string DefaultOutputFolder()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        return Path.Combine(videos, "Dota2Highlights");
    }
}
