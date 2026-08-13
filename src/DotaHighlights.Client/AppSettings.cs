using System.IO;

namespace DotaHighlights.Client;

/// <summary>Configuración del MVP. En fases posteriores se persistirá a disco.</summary>
public sealed class AppSettings
{
    /// <summary>Segundos de historia que conserva el buffer (lo que se guarda al disparar).
    /// Debe cubrir la construcción de la jugada + los kills + el post-roll.</summary>
    public int BufferSeconds { get; set; } = 25;

    /// <summary>Segundos que se sigue grabando tras el último kill antes de guardar (el "después").</summary>
    public double PostRollSeconds { get; set; } = 8;

    public int Fps { get; set; } = 30;

    public int CaptureWidth { get; set; } = 1280;
    public int CaptureHeight { get; set; } = 720;

    /// <summary>Carpeta donde se guardan los clips.</summary>
    public string OutputFolder { get; set; } = DefaultOutputFolder();

    /// <summary>Ruta a ffmpeg.exe (o "ffmpeg" si está en el PATH).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>Puerto local donde escuchamos el Game State Integration de Dota 2.
    /// Se evita el rango 50000-53362 que Hyper-V/WSL reservan en esta máquina.</summary>
    public int GsiPort { get; set; } = 8801;

    /// <summary>Kills en la ventana para considerar highlight (2 = doble kill).</summary>
    public int MinKillsForHighlight { get; set; } = 2;

    /// <summary>Empezar a capturar automáticamente al abrir la app.</summary>
    public bool AutoStartCapture { get; set; } = true;

    /// <summary>Token de autenticación del GSI (debe coincidir con el del .cfg).</summary>
    public string GsiAuthToken { get; set; } = "dota2highlights";

    private static string DefaultOutputFolder()
    {
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        return Path.Combine(videos, "Dota2Highlights");
    }
}
