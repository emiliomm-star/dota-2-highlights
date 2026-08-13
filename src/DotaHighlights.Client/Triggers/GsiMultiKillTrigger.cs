using System.Text.Json;
using System.Threading;
using DotaHighlights.Client.Gsi;
using Serilog;

namespace DotaHighlights.Client.Triggers;

/// <summary>
/// Gatillo AUTOMÁTICO (sin botones): escucha el Game State Integration de Dota 2
/// y dispara cuando el jugador consigue un multi-kill (varias muertes en una
/// ventana corta de tiempo, como el doble/triple kill del juego).
///
/// Dota no envía un evento "doble kill" explícito, así que se infiere contando
/// los incrementos de <c>player.kills</c> dentro de una ventana temporal.
///
/// El guardado NO es instantáneo: tras el primer multi-kill se espera un
/// "post-roll" para incluir el desenlace en el clip. Si llegan más kills durante
/// esa espera, el nivel sube (Doble → Triple → Ultra → Rampage) y la espera se
/// reinicia, de modo que toda la escalada queda en UN solo clip.
/// </summary>
public sealed class GsiMultiKillTrigger : IHighlightTrigger
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string[] KillWords =
        { "", "", "Doble kill", "Triple kill", "Ultra kill", "Rampage" };

    private readonly GsiListener _listener;
    private readonly ILogger _log;
    private readonly int _minKills;
    private readonly double _windowSeconds;
    private readonly double _postRollSeconds;
    private readonly Lock _gate = new();

    private int _lastKills = -1;
    private int _lastDeaths = -1;
    private readonly List<double> _recentKillTimes = new();
    private bool _loggedFirstPayload;

    // Guardado diferido (post-roll).
    private Timer? _fireTimer;
    private string _pendingReason = "";
    private int _pendingLevel;

    public event EventHandler<HighlightTriggeredEventArgs>? Triggered;

    /// <param name="minKills">Kills mínimos en la ventana para considerarlo highlight (2 = doble kill).</param>
    /// <param name="postRollSeconds">Segundos que se sigue grabando tras el último kill antes de guardar.</param>
    /// <param name="windowSeconds">Ventana de tiempo del multi-kill (Dota usa ~18s).</param>
    public GsiMultiKillTrigger(
        GsiListener listener, ILogger log,
        int minKills = 2, double postRollSeconds = 8, double windowSeconds = 18)
    {
        _listener = listener;
        _log = log;
        _minKills = Math.Max(2, minKills);
        _postRollSeconds = postRollSeconds;
        _windowSeconds = windowSeconds;
    }

    public void Start()
    {
        _fireTimer = new Timer(OnPostRollElapsed, null, Timeout.Infinite, Timeout.Infinite);
        _listener.PayloadReceived += OnPayload;
        _listener.Start();
    }

    public void Stop()
    {
        _listener.PayloadReceived -= OnPayload;
        _listener.Stop();
        _fireTimer?.Dispose();
        _fireTimer = null;
    }

    private void OnPayload(string json)
    {
        GsiState? state;
        try { state = JsonSerializer.Deserialize<GsiState>(json, JsonOpts); }
        catch (Exception ex) { _log.Debug(ex, "GSI: JSON no parseable"); return; }

        if (!_loggedFirstPayload)
        {
            _loggedFirstPayload = true;
            _log.Information("GSI: recibiendo datos de Dota 2 ✔ (estado: {State})",
                state?.Map?.GameState ?? "?");
        }

        var player = state?.Player;
        if (player?.Kills is not int kills) return;

        // Reloj del juego si está disponible; si no, tiempo real.
        double now = state!.Map?.ClockTime ?? (Environment.TickCount64 / 1000.0);
        int deaths = player.Deaths ?? _lastDeaths;

        lock (_gate)
        {
            // Primera muestra: fija la línea base sin disparar.
            if (_lastKills < 0)
            {
                _lastKills = kills;
                _lastDeaths = deaths;
                return;
            }

            // Al morir se corta la racha para el CONTEO futuro (un guardado ya
            // programado sigue en pie: la muerte forma parte del desenlace).
            if (deaths > _lastDeaths)
            {
                _recentKillTimes.Clear();
                _lastDeaths = deaths;
            }

            // Partida nueva / reinicio de contadores.
            if (kills < _lastKills)
            {
                _recentKillTimes.Clear();
                _lastKills = kills;
                return;
            }

            if (kills > _lastKills)
            {
                int delta = kills - _lastKills;
                _log.Information("GSI: kill! total={Kills} (+{Delta}), t={Clock:0}s", kills, delta, now);
                for (int i = 0; i < delta; i++) _recentKillTimes.Add(now);

                // Descarta kills fuera de la ventana del multi-kill.
                _recentKillTimes.RemoveAll(t => now - t > _windowSeconds);

                int inWindow = _recentKillTimes.Count;
                if (inWindow >= _minKills && inWindow > _pendingLevel)
                {
                    _pendingLevel = inWindow;
                    _pendingReason = KillWords[Math.Min(inWindow, KillWords.Length - 1)];
                    _log.Information("GSI multi-kill en progreso: {Reason} ({Kills} kills) — grabando desenlace {Post}s",
                        _pendingReason, inWindow, _postRollSeconds);

                    // (Re)programa el guardado para dentro de _postRollSeconds.
                    _fireTimer?.Change(
                        TimeSpan.FromSeconds(_postRollSeconds), Timeout.InfiniteTimeSpan);
                }
            }

            _lastKills = kills;
        }
    }

    private void OnPostRollElapsed(object? _)
    {
        string reason;
        lock (_gate)
        {
            reason = _pendingReason;
            _pendingReason = "";
            _pendingLevel = 0;
            _recentKillTimes.Clear();
        }
        if (!string.IsNullOrEmpty(reason))
        {
            _log.Information("GSI: guardando highlight tras post-roll: {Reason}", reason);
            Triggered?.Invoke(this, new HighlightTriggeredEventArgs(reason));
        }
    }

    public void Dispose() => Stop();
}
