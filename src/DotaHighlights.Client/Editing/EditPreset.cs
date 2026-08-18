using System.Globalization;

namespace DotaHighlights.Client.Editing;

/// <summary>
/// Contexto de tiempos del clip que usan los presets para construir su filtro.
/// El "momento clave" (kill) se conoce gracias a GSI.
/// </summary>
public sealed class EditContext
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public required double Duration { get; init; }
    public required double MoneyShot { get; init; }
    public required double SlowStart { get; init; }   // s0
    public required double SlowEnd { get; init; }      // s1
    public required double ZoomEnd { get; init; }      // z1
    public required double FadeOut { get; init; }      // dur - 0.6

    /// <summary>Expresión ffmpeg del pulso de brillo por beat (o null si no hay música).</summary>
    public string? BeatPulseExpr { get; init; }

    /// <summary>Formatea un número con punto decimal (ffmpeg no acepta coma).</summary>
    public static string F(double v) => v.ToString("0.###", Inv);
}

/// <summary>
/// Un preset de edición: sabe construir su cadena de filtros ffmpeg (que debe
/// terminar produciendo la etiqueta [v]).
/// </summary>
public sealed record EditPreset(
    string Id,
    string Name,
    int Rank,
    Func<EditContext, string> BuildFilter);
