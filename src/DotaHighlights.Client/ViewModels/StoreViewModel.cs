using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotaHighlights.Client.Models;

namespace DotaHighlights.Client.ViewModels;

/// <summary>Pantalla principal tipo tienda/librería: catálogo de apps.</summary>
public sealed partial class StoreViewModel : ObservableObject
{
    public StoreViewModel()
    {
        Apps.Add(new StoreAppItem(
            "dota2", "Dota 2 Highlights",
            "Captura automática de tus mejores jugadas", Available: true));

        // Marcadores de posición para dar sensación de tienda (aún no disponibles).
        Apps.Add(new StoreAppItem("valorant", "Valorant Highlights", "Próximamente", Available: false));
        Apps.Add(new StoreAppItem("lol", "League Highlights", "Próximamente", Available: false));
        Apps.Add(new StoreAppItem("cs2", "CS2 Highlights", "Próximamente", Available: false));
    }

    public ObservableCollection<StoreAppItem> Apps { get; } = new();

    /// <summary>Se emite con el Id de la app cuando el usuario pulsa "Entrar".</summary>
    public event Action<string>? AppOpened;

    [RelayCommand]
    private void Open(StoreAppItem? item)
    {
        if (item is { Available: true })
            AppOpened?.Invoke(item.Id);
    }
}
