using System.Text.Json.Serialization;

namespace DotaHighlights.Client.Gsi;

/// <summary>
/// Subconjunto del JSON que Dota 2 envía por Game State Integration.
/// Solo mapeamos lo que necesita el detector de highlights.
/// </summary>
public sealed class GsiState
{
    [JsonPropertyName("map")] public GsiMap? Map { get; set; }
    [JsonPropertyName("player")] public GsiPlayer? Player { get; set; }
    [JsonPropertyName("hero")] public GsiHero? Hero { get; set; }
    [JsonPropertyName("auth")] public GsiAuth? Auth { get; set; }
}

public sealed class GsiMap
{
    [JsonPropertyName("game_state")] public string? GameState { get; set; }
    [JsonPropertyName("clock_time")] public int? ClockTime { get; set; }
    [JsonPropertyName("paused")] public bool? Paused { get; set; }
}

public sealed class GsiPlayer
{
    [JsonPropertyName("kills")] public int? Kills { get; set; }
    [JsonPropertyName("deaths")] public int? Deaths { get; set; }
    [JsonPropertyName("assists")] public int? Assists { get; set; }
    [JsonPropertyName("kill_streak")] public int? KillStreak { get; set; }
}

public sealed class GsiHero
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("alive")] public bool? Alive { get; set; }
    [JsonPropertyName("level")] public int? Level { get; set; }
}

public sealed class GsiAuth
{
    [JsonPropertyName("token")] public string? Token { get; set; }
}
