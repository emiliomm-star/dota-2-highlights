using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace DotaHighlights.Client.Gsi;

/// <summary>
/// Servidor HTTP mínimo en localhost que recibe los POST de Game State
/// Integration de Dota 2 y emite el cuerpo JSON crudo. Se usa TcpListener
/// (en vez de HttpListener) para no requerir reservas de URL ni permisos de admin.
/// </summary>
public sealed class GsiListener : IDisposable
{
    private readonly int _port;
    private readonly ILogger _log;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public GsiListener(int port, ILogger log)
    {
        _port = port;
        _log = log;
    }

    /// <summary>Emite el JSON crudo de cada actualización de estado.</summary>
    public event Action<string>? PayloadReceived;

    public void Start()
    {
        if (_listener is not null) return;
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        _log.Information("GSI escuchando en http://127.0.0.1:{Port}", _port);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* ignore */ }
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener!.AcceptTcpClientAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { _log.Warning(ex, "GSI accept falló"); continue; }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                var body = await ReadHttpBodyAsync(stream, ct);

                // Responde 200 para que Dota no reintente / no bloquee.
                var response = System.Text.Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, ct);

                if (!string.IsNullOrWhiteSpace(body))
                    PayloadReceived?.Invoke(body);
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "GSI: error atendiendo conexión");
            }
        }
    }

    /// <summary>Lee una petición HTTP/1.1 simple y devuelve el cuerpo (JSON).</summary>
    private static async Task<string?> ReadHttpBodyAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        int headerEnd = -1;

        // Leer hasta encontrar el fin de cabeceras (\r\n\r\n).
        while (headerEnd < 0)
        {
            int read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            ms.Write(buffer, 0, read);
            headerEnd = FindHeaderEnd(ms.GetBuffer(), (int)ms.Length);
        }
        if (headerEnd < 0) return null;

        var all = ms.GetBuffer();
        int total = (int)ms.Length;
        string headers = System.Text.Encoding.ASCII.GetString(all, 0, headerEnd);
        int contentLength = ParseContentLength(headers);

        int bodyStart = headerEnd + 4;
        int bodyHave = total - bodyStart;

        using var bodyMs = new MemoryStream();
        if (bodyHave > 0) bodyMs.Write(all, bodyStart, bodyHave);

        while (bodyMs.Length < contentLength)
        {
            int read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            bodyMs.Write(buffer, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(bodyMs.GetBuffer(), 0, (int)bodyMs.Length);
    }

    private static int FindHeaderEnd(byte[] data, int length)
    {
        for (int i = 0; i + 3 < length; i++)
        {
            if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n')
                return i;
        }
        return -1;
    }

    private static int ParseContentLength(string headers)
    {
        foreach (var line in headers.Split("\r\n"))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            if (line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line[(colon + 1)..].Trim(), out int len))
                return len;
        }
        return 0;
    }

    public void Dispose() => Stop();
}
