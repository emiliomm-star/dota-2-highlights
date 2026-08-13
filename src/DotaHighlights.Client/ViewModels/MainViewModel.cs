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

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveHighlightAsync()
    {
        if (IsSaving) return;
        IsSaving = true;
        StatusText = "Guardando highlight…";
        try
        {
            var path = await Task.Run(() => _recorder.SaveHighlightAsync());
            StatusText = $"Guardado: {Path.GetFileName(path)}";
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

    /// <summary>Invocado por el gatillo (hotkey) desde el code-behind.</summary>
    public void TriggerSave()
    {
        if (SaveHighlightCommand.CanExecute(null))
            SaveHighlightCommand.Execute(null);
    }

    private void OnClipSaved(string path)
    {
        // ClipSaved puede llegar desde un hilo de fondo: marshalizamos a la UI.
        App.Current.Dispatcher.Invoke(() =>
            SavedClips.Insert(0, new ClipItem(Path.GetFileName(path), path)));
    }
}

public sealed record ClipItem(string Name, string FullPath);
