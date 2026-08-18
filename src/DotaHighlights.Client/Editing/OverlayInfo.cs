namespace DotaHighlights.Client.Editing;

/// <summary>Datos a quemar sobre el video (provienen de GSI).</summary>
/// <param name="Title">Texto grande, p. ej. "RAMPAGE" (vacío = sin título).</param>
/// <param name="Hero">Nombre del héroe (vacío = no mostrar).</param>
/// <param name="Kills">Kills totales (0 = no mostrar contador).</param>
public sealed record OverlayInfo(string Title, string Hero, int Kills);
