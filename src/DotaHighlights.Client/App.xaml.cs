using System.IO;
using System.Windows;
using DotaHighlights.Client.Capture;
using DotaHighlights.Client.Encoding;
using DotaHighlights.Client.Recording;
using DotaHighlights.Client.ViewModels;
using Serilog;

namespace DotaHighlights.Client;

public partial class App : Application
{
    private HighlightRecorder? _recorder;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = new AppSettings();

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dota2Highlights", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("Aplicación iniciada");

        // --- Composición de dependencias (Fase 1) ---
        // Fuente sintética por ahora; se sustituye por captura real (WGC) a continuación.
        IFrameSource source = new SyntheticFrameSource(
            settings.CaptureWidth, settings.CaptureHeight, settings.Fps);

        IClipEncoder encoder = new NvencClipEncoder(settings.FfmpegPath);

        _recorder = new HighlightRecorder(source, encoder, settings, Log.Logger);

        var vm = new MainViewModel(_recorder, settings, Log.Logger);
        var window = new MainWindow(vm);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _recorder?.Dispose();
            Log.Information("Aplicación cerrada");
            Log.CloseAndFlush();
        }
        catch { /* ignore */ }
        base.OnExit(e);
    }
}
