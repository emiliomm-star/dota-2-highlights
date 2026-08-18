using System.Globalization;

namespace DotaHighlights.Client.Gsi;

/// <summary>
/// Estado de la partida en curso (última muestra de GSI), thread-safe.
/// Lo actualiza el trigger y lo lee el editor para los overlays.
/// </summary>
public sealed class GameState
{
    private readonly Lock _gate = new();
    private string _hero = "";
    private int _kills;
    private int _killStreak;

    /// <summary>Nombre del héroe legible (p. ej. "Juggernaut").</summary>
    public string HeroDisplay { get { lock (_gate) return _hero; } }
    public int Kills { get { lock (_gate) return _kills; } }
    public int KillStreak { get { lock (_gate) return _killStreak; } }

    public void Update(string? heroRaw, int? kills, int? killStreak)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(heroRaw)) _hero = CleanHeroName(heroRaw);
            if (kills is int k) _kills = k;
            if (killStreak is int s) _killStreak = s;
        }
    }

    /// <summary>"npc_dota_hero_drow_ranger" -> "Drow Ranger".</summary>
    private static string CleanHeroName(string raw)
    {
        const string prefix = "npc_dota_hero_";
        string s = raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? raw[prefix.Length..] : raw;
        var words = s.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var ti = CultureInfo.InvariantCulture.TextInfo;
        return string.Join(' ', words.Select(w => ti.ToTitleCase(w)));
    }
}
