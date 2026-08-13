using System.Text.Json;
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
    private readonly Lock _gate = new();

    private int _lastKills = -1;
    private int _lastDeaths = -1;
    private readonly List<double> _recentKillTimes = new();
    private bool _firedThisCluster;

    public event EventHandler<HighlightTriggeredEventArgs>? Triggered;

    /// <param name="minKills">Kills mínimos en la ventana para considerarlo highlight (2 = doble kill).</param>
    /// <param name="windowSeconds">Ventana de tiempo del multi-kill (Dota usa ~18s).</param>
    public GsiMultiKillTrigger(GsiListener listener, ILogger log, int minKills = 2, double windowSeconds = 18)
    {
        _listener = listener;
        _log = log;
        _minKills = Math.Max(2, minKills);
        _windowSeconds = windowSeconds;
    }

    public void Start()
    {
        _listener.PayloadReceived += OnPayload;
        _listener.Start();
    }

    public void Stop()
    {
        _listener.PayloadReceived -= OnPayload;
        _listener.Stop();
    }

    private void OnPayload(string json)
    {
        GsiState? state;
        try { state = JsonSerializer.Deserialize<GsiState>(json, JsonOpts); }
        catch (Exception ex) { _log.Debug(ex, "GSI: JSON no parseable"); return; }

        var player = state?.Player;
        if (player?.Kills is not int kills) return;

        // Reloj del juego si está disponible; si no, tiempo real.
        double now = state!.Map?.ClockTime ?? (Environment.TickCount64 / 1000.0);
        int deaths = player.Deaths ?? _lastDeaths;

        EventHandler<HighlightTriggeredEventArgs>? fire = null;
        string reason = "";

        lock (_gate)
        {
            // Primera muestra: fija la línea base sin disparar.
            if (_lastKills < 0)
            {
                _lastKills = kills;
                _lastDeaths = deaths;
                return;
            }

            // Al morir se corta la racha de multi-kill.
            if (deaths > _lastDeaths)
            {
                ResetCluster();
                _lastDeaths = deaths;
            }

            // Partida nueva / reinicio de contadores.
            if (kills < _lastKills)
            {
                ResetCluster();
                _lastKills = kills;
                return;
            }

            if (kills > _lastKills)
            {
                int delta = kills - _lastKills;
                for (int i = 0; i < delta; i++) _recentKillTimes.Add(now);

                // Descarta kills fuera de la ventana.
                _recentKillTimes.RemoveAll(t => now - t > _windowSeconds);

                int inWindow = _recentKillTimes.Count;
                if (!_firedThisCluster && inWindow >= _minKills)
                {
                    _firedThisCluster = true;
                    reason = KillWords[Math.Min(inWindow, KillWords.Length - 1)];
                    fire = Triggered;
                    _log.Information("GSI multi-kill detectado: {Reason} ({Kills} kills en ventana)", reason, inWindow);
                }
            }

            // Si pasó la ventana sin nuevos kills, cierra el cluster.
            if (_recentKillTimes.Count > 0 && now - _recentKillTimes[^1] > _windowSeconds)
                ResetCluster();

            _lastKills = kills;
        }

        fire?.Invoke(this, new HighlightTriggeredEventArgs(reason));
    }

    private void ResetCluster()
    {
        _recentKillTimes.Clear();
        _firedThisCluster = false;
    }

    public void Dispose() => Stop();
}
