using CommunityToolkit.Mvvm.ComponentModel;

namespace DotaHighlights.Client.ViewModels;

/// <summary>
/// Contenedor de navegación: alterna entre la tienda y la vista de una app.
/// La captura/GSI siguen corriendo en segundo plano independientemente de la
/// vista mostrada.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly StoreViewModel _store;
    private readonly MainViewModel _dotaCapture;

    [ObservableProperty]
    private object _currentView;

    public ShellViewModel(StoreViewModel store, MainViewModel dotaCapture)
    {
        _store = store;
        _dotaCapture = dotaCapture;

        _store.AppOpened += OnAppOpened;
        _dotaCapture.BackRequested += () => CurrentView = _store;

        _currentView = _store;
    }

    private void OnAppOpened(string id)
    {
        if (id == "dota2")
            CurrentView = _dotaCapture;
    }
}
