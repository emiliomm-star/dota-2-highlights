using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotaHighlights.Client.Recording;
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
        StatusText = "Detenido";
        OutputFolder = settings.OutputFolder;
        BufferSeconds = settings.BufferSeconds;
    }

    public ObservableCollection<ClipItem> SavedClips { get; } = new();

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
            var path = await Task.Run(() => _recorder.SaveHighlightAsync());
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
}

public sealed record ClipItem(string Name, string FullPath, string Reason);
