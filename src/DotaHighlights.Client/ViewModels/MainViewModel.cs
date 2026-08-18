using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotaHighlights.Client.Editing;
using DotaHighlights.Client.Recording;
using Microsoft.Win32;
using Serilog;

namespace DotaHighlights.Client.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly HighlightRecorder _recorder;
    private readonly AppSettings _settings;
    private readonly ILogger _log;

    public MainViewModel(HighlightRecorder recorder, AppSettings settings, ILogger log)
    {
        _recorder = recorder;
        _settings = settings;
        _log = log;
        _recorder.ClipSaved += OnClipSaved;
        _recorder.EditsStarted += OnEditsStarted;
        _recorder.EditReady += OnEditReady;
        _recorder.EditsCompleted += OnEditsCompleted;
        StatusText = "Detenido";
        OutputFolder = settings.OutputFolder;
        BufferSeconds = settings.BufferSeconds;
        MusicLabel = string.IsNullOrWhiteSpace(settings.MusicPath)
            ? "Ninguna" : Path.GetFileName(settings.MusicPath);
    }

    public ObservableCollection<ClipItem> SavedClips { get; } = new();

    /// <summary>Las 3 ediciones "con aura" del último highlight guardado.</summary>
    public ObservableCollection<EditedClip> RecommendedEdits { get; } = new();

    [ObservableProperty]
    private bool _isEditing;

    /// <summary>Nombre del archivo de música elegido (o "Ninguna").</summary>
    [ObservableProperty]
    private string _musicLabel = "Ninguna";

    [RelayCommand]
    private void LoadMusic()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Elige una canción para tus ediciones",
            Filter = "Audio (*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg)|*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg|Todos|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            _settings.MusicPath = dlg.FileName;
            MusicLabel = Path.GetFileName(dlg.FileName);
            StatusText = $"🎵 Música: {MusicLabel} (se usará en las próximas ediciones)";
        }
    }

    [RelayCommand]
    private void ClearMusic()
    {
        _settings.MusicPath = "";
        MusicLabel = "Ninguna";
        StatusText = "🎵 Música quitada (ediciones sin sonido)";
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveHighlightCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _outputFolder = "";

    [ObservableProperty]
    private int _bufferSeconds;

    [ObservableProperty]
    private bool _isSaving;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        _settings.BufferSeconds = BufferSeconds;
        _recorder.Start();
        IsRunning = true;
        StatusText = $"Capturando · buffer {BufferSeconds}s";
    }

    private bool CanStart() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _recorder.Stop();
        IsRunning = false;
        StatusText = "Detenido";
    }

    private bool CanStop() => IsRunning;

    private string _lastReason = "Manual";

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveHighlightAsync()
    {
        if (IsSaving) return;
        var reason = _lastReason;
        IsSaving = true;
        StatusText = $"Guardando highlight ({reason})…";
        try
        {
            var path = await Task.Run(() => _recorder.SaveHighlightAsync(reason));
            StatusText = $"⭐ {reason} guardado: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Fallo al guardar highlight");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanSave() => IsRunning && !IsSaving;

    /// <summary>Se emite cuando el usuario quiere volver a la tienda.</summary>
    public event Action? BackRequested;

    [RelayCommand]
    private void BackToStore() => BackRequested?.Invoke();

    /// <summary>Invocado por cualquier gatillo (hotkey o IA/GSI). Debe llamarse en el hilo de UI.</summary>
    public void TriggerSave(string reason)
    {
        _lastReason = reason;
        if (SaveHighlightCommand.CanExecute(null))
            SaveHighlightCommand.Execute(null);
    }

    private void OnClipSaved(string path)
    {
        var reason = _lastReason;
        // ClipSaved puede llegar desde un hilo de fondo: marshalizamos a la UI.
        App.Current.Dispatcher.Invoke(() =>
            SavedClips.Insert(0, new ClipItem(Path.GetFileName(path), path, reason)));
    }

    private void OnEditsStarted() => App.Current.Dispatcher.Invoke(() =>
    {
        IsEditing = true;
        RecommendedEdits.Clear();
        StatusText = "✨ Generando ediciones con aura…";
    });

    private void OnEditReady(EditedClip clip) => App.Current.Dispatcher.Invoke(() =>
    {
        // Inserta manteniendo el orden por Rank (Pro Montage primero).
        int i = 0;
        while (i < RecommendedEdits.Count && RecommendedEdits[i].Rank <= clip.Rank) i++;
        RecommendedEdits.Insert(i, clip);
    });

    private void OnEditsCompleted(int count) => App.Current.Dispatcher.Invoke(() =>
    {
        IsEditing = false;
        if (count > 0) StatusText = $"✨ {count} ediciones listas del último highlight";
    });

    [RelayCommand]
    private void PlayEdit(EditedClip? clip)
    {
        if (clip is not null && File.Exists(clip.Path)) OpenFile(clip.Path);
    }

    [RelayCommand]
    private void PlayClip(ClipItem? clip)
    {
        if (clip is not null && File.Exists(clip.FullPath)) OpenFile(clip.FullPath);
    }

    private void OpenFile(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { _log.Warning(ex, "No se pudo abrir {Path}", path); }
    }
}

public sealed record ClipItem(string Name, string FullPath, string Reason);
