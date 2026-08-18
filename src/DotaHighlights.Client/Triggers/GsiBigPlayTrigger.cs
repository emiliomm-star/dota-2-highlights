using System.Text.Json;
using System.Threading;
using DotaHighlights.Client.Gsi;
using Serilog;

namespace DotaHighlights.Client.Triggers;

/// <summary>
/// Gatillo de "buenas jugadas SIN kill", con heurísticas sobre los datos de GSI:
///   • Escape clutch: sobrevivir tras caer a muy poca vida sin morir.
///   • Teamfight: subida rápida de asistencias (participar en varias kills).
///   • Racha de kills: kill_streak alto (Mega Kill, Godlike…) aunque estén espaciadas.
/// Comparte el <see cref="GsiListener"/> con el trigger de multi-kills.
/// </summary>
public sealed class GsiBigPlayTrigger : IHighlightTrigger
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Nombres de racha de Dota (índice = kill_streak). Empezamos en 5 para no
    // solapar con el trigger de multi-kills (2-4).
    private static readonly Dictionary<int, string> StreakNames = new()
    {
        [5] = "Mega Kill", [6] = "Unstoppable", [7] = "Wicked Sick",
        [8] = "Monster Kill", [9] = "Godlike", [10] = "Beyond Godlike",
    };

    private readonly GsiListener _listener;
    private readonly ILogger _log;
    private readonly double _postRollSeconds;
    private readonly Lock _gate = new();

    // Clutch (supervivencia).
    private const int LowHp = 15;       // umbral de "en peligro"
    private const int RecoverHp = 45;   // umbral de "a salvo"
    private bool _inDanger;
    private double _dangerStart;

    // Teamfight (asistencias).
    private const double AssistWindow = 14;
    private readonly List<double> _recentAssists = new();
    private int _lastAssists = -1;

    // Estado común.
    private int _lastDeaths = -1;
    private int _lastStreak;
    private int _highestStreakFired;
    private bool _first = true;

    // Guardado diferido (post-roll).
    private Timer? _fireTimer;
    private string _pendingReason = "";

    public event EventHandler<HighlightTriggeredEventArgs>? Triggered;

    public GsiBigPlayTrigger(GsiListener listener, ILogger log, double postRollSeconds = 6)
    {
        _listener = listener;
        _log = log;
        _postRollSeconds = postRollSeconds;
    }

    public void Start()
    {
        _fireTimer = new Timer(OnPostRoll, null, Timeout.Infinite, Timeout.Infinite);
        _listener.PayloadReceived += OnPayload;
        _listener.Start();
    }

    public void Stop()
    {
        _listener.PayloadReceived -= OnPayload;
        _fireTimer?.Dispose();
        _fireTimer = null;
    }

    private void OnPayload(string json)
    {
        GsiState? state;
        try { state = JsonSerializer.Deserialize<GsiState>(json, JsonOpts); }
        catch { return; }

        var p = state?.Player;
        if (p is null) return;

        // Solo en partida en curso.
        if (state!.Map?.GameState is string gs && gs != "DOTA_GAMERULES_STATE_GAME_IN_PROGRESS")
            return;

        double now = state.Map?.ClockTime ?? (Environment.TickCount64 / 1000.0);
        int deaths = p.Deaths ?? _lastDeaths;
        int assists = p.Assists ?? _lastAssists;
        int streak = p.KillStreak ?? _lastStreak;
        int? hp = state.Hero?.HealthPercent;
        bool alive = state.Hero?.Alive ?? true;

        lock (_gate)
        {
            if (_first)
            {
                _first = false;
                _lastDeaths = deaths; _lastAssists = assists; _lastStreak = streak;
                return;
            }

            bool died = deaths > _lastDeaths;
            if (died)
            {
                // Al morir se cancela el peligro y se reinicia la racha.
                _inDanger = false;
                _recentAssists.Clear();
                _highestStreakFired = 0;
            }

            // --- Escape clutch ---
            if (hp is int h && alive && !died)
            {
                if (!_inDanger && h <= LowHp)
                {
                    _inDanger = true;
                    _dangerStart = now;
                }
                else if (_inDanger && h >= RecoverHp)
                {
                    _inDanger = false;
                    Schedule("Escape clutch");
                }
                else if (_inDanger && now - _dangerStart > 12)
                {
                    _inDanger = false; // estuvo bajo mucho tiempo sin recuperar claramente
                }
            }

            // --- Teamfight (asistencias) ---
            if (assists > _lastAssists)
            {
                int delta = assists - _lastAssists;
                for (int i = 0; i < delta; i++) _recentAssists.Add(now);
                _recentAssists.RemoveAll(t => now - t > AssistWindow);
                if (_recentAssists.Count >= 2)
                {
                    Schedule("Teamfight");
                    _recentAssists.Clear();
                }
            }

            // --- Racha de kills ---
            if (streak < _lastStreak) _highestStreakFired = 0; // se reinició
            if (streak > _highestStreakFired && StreakNames.TryGetValue(streak, out var name))
            {
                _highestStreakFired = streak;
                Schedule(name);
            }

            _lastDeaths = deaths;
            _lastAssists = assists;
            _lastStreak = streak;
        }
    }

    private void Schedule(string reason)
    {
        _pendingReason = reason;
        _log.Information("GSI big-play: {Reason} — grabando desenlace {Post}s", reason, _postRollSeconds);
        _fireTimer?.Change(TimeSpan.FromSeconds(_postRollSeconds), Timeout.InfiniteTimeSpan);
    }

    private void OnPostRoll(object? _)
    {
        string reason;
        lock (_gate) { reason = _pendingReason; _pendingReason = ""; }
        if (!string.IsNullOrEmpty(reason))
        {
            _log.Information("GSI: guardando highlight (sin kill) tras post-roll: {Reason}", reason);
            Triggered?.Invoke(this, new HighlightTriggeredEventArgs(reason));
        }
    }

    public void Dispose() => Stop();
}
