namespace DotaHighlights.Client.Triggers;

/// <summary>
/// Fuente de "gatillos" que piden guardar un highlight.
/// Fase 1: <c>HotkeyTrigger</c> (una tecla). Fase 2: un trigger que escucha
/// al servicio de IA en Python (doble/triple kill, etc.) implementará esta
/// misma interfaz, sin tocar el resto del sistema.
/// </summary>
public interface IHighlightTrigger : IDisposable
{
    /// <summary>Se dispara cuando hay que guardar el clip de los últimos N segundos.</summary>
    event EventHandler<HighlightTriggeredEventArgs>? Triggered;

    void Start();
    void Stop();
}
