namespace DotaHighlights.Client.Editing;

/// <summary>Una versión editada derivada de un highlight.</summary>
/// <param name="StyleId">Identificador del estilo (promontage, slowmo, zoom, cinematic).</param>
/// <param name="StyleName">Nombre mostrado (con emoji).</param>
/// <param name="Path">Ruta del mp4 generado.</param>
/// <param name="Rank">Orden de recomendación (menor = se muestra antes).</param>
public sealed record EditedClip(string StyleId, string StyleName, string Path, int Rank);
