namespace DotaHighlights.Client.Triggers;

/// <summary>Datos del gatillo: por qué se pide guardar el highlight.</summary>
public sealed class HighlightTriggeredEventArgs(string reason) : EventArgs
{
    /// <summary>Motivo legible: "Hotkey F9", "Doble kill", "Triple kill", etc.</summary>
    public string Reason { get; } = reason;
}
