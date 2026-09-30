using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UneekorRelaConnector;

/// <summary>
/// Minimal GSPro Open Connect v1 <em>server</em> so a community putting client
/// (typically [springbok](https://github.com/springbok/MLM2PRO-GSPro-Connector)
/// with ExPutt OCR) can send putts here instead of GSPro.
///
/// Spec: https://gsprogolf.com/GSProConnectV1.html
/// </summary>
internal sealed class GsproOpenConnectServer : IDisposable
{
    private readonly string _bindAddress;
    private readonly int _port;
    private readonly object _gate = new();
    private readonly List<TcpClient> _clients = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private string _handed = "RH";
    private string _club = "DR";

    public GsproOpenConnectServer(string bindAddress, int port)
    {
        _bindAddress = string.IsNullOrWhiteSpace(bindAddress) ? "127.0.0.1" : bindAddress;
        _port = port is >= 0 and <= 65535 ? port : 921;
    }

    public int Port { get; private set; }

    public event Action<ParsedPutt>? PuttDetected;
    public event Action<string>? Log;

    public void Start()
    {
        lock (_gate)
        {
            Stop_NoLock();
            _cts = new CancellationTokenSource();
            var ip = _bindAddress is "0.0.0.0" or "*"
                ? IPAddress.Any
                : IPAddress.Parse(_bindAddress);
            _listener = new TcpListener(ip, _port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            var token = _cts.Token;
            _acceptLoop = Task.Run(() => AcceptLoop(token), token);
            Log?.Invoke($"Open Connect listening on {_bindAddress}:{Port} (point springbok ExPutt at this host/port)");
        }
    }

    public void Stop()
    {
        lock (_gate) Stop_NoLock();
    }

    public void SetPlayer(string handed, string club)
    {
        _handed = string.Equals(handed, "LH", StringComparison.OrdinalIgnoreCase) ? "LH" : "RH";
        _club = string.IsNullOrWhiteSpace(club) ? "DR" : club.Trim();
        Broadcast(PlayerInfoJson(_handed, _club));
    }

    public void Dispose() => Stop();

    private void Stop_NoLock()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _listener?.Stop(); } catch { /* ignore */ }
        lock (_clients)
        {
            foreach (var c in _clients)
            {
                try { c.Close(); } catch { /* ignore */ }
            }
            _clients.Clear();
        }

        try { _acceptLoop?.Wait(500); } catch { /* ignore */ }
        _cts?.Dispose();
        _cts = null;
        _acceptLoop = null;
        _listener = null;
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                Log?.Invoke("Accept failed: " + ex.Message);
                continue;
            }

            lock (_clients) _clients.Add(client);
            Log?.Invoke("Open Connect client connected from " + client.Client.RemoteEndPoint);
            _ = Task.Run(() => ReadLoop(client, token), token);
            try { Send(client, PlayerInfoJson(_handed, _club)); } catch { /* ignore */ }
        }
    }

    private async Task ReadLoop(TcpClient client, CancellationToken token)
    {
        var buffer = new byte[8192];
        var pending = new StringBuilder();
        try
        {
            var stream = client.GetStream();
            while (!token.IsCancellationRequested && client.Connected)
            {
                int n;
                try
                {
                    n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch { break; }

                if (n <= 0)
                    break;

                pending.Append(Encoding.UTF8.GetString(buffer, 0, n));
                foreach (var json in ExtractJsonObjects(pending))
                    HandlePayload(client, json);
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke("Open Connect client error: " + ex.Message);
        }
        finally
        {
            lock (_clients) _clients.Remove(client);
            try { client.Close(); } catch { /* ignore */ }
            Log?.Invoke("Open Connect client disconnected.");
        }
    }

    private void HandlePayload(TcpClient client, string json)
    {
        try
        {
            if (ExputtPuttParser.IsHeartbeat(json))
            {
                Send(client, AckJson(200, "Heartbeat received"));
                return;
            }

            if (!ExputtPuttParser.TryParse(json, out var putt, out var error))
            {
                Send(client, AckJson(501, error ?? "Invalid shot"));
                return;
            }

            Send(client, AckJson(200, "Shot received successfully"));
            PuttDetected?.Invoke(putt);
        }
        catch (Exception ex)
        {
            Log?.Invoke("Open Connect payload error: " + ex.Message);
            try { Send(client, AckJson(500, ex.Message)); } catch { /* ignore */ }
        }
    }

    private void Broadcast(string json)
    {
        List<TcpClient> snapshot;
        lock (_clients) snapshot = _clients.ToList();
        foreach (var client in snapshot)
        {
            try { Send(client, json); }
            catch { /* drop on next read */ }
        }
    }

    private static void Send(TcpClient client, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        client.GetStream().Write(bytes, 0, bytes.Length);
    }

    private static string AckJson(int code, string message)
        => JsonSerializer.Serialize(new { Code = code, Message = message });

    private static string PlayerInfoJson(string handed, string club)
        => JsonSerializer.Serialize(new
        {
            Code = 201,
            Message = "GSPro Player Information",
            Player = new { Handed = handed, Club = club }
        });

    /// <summary>Pull complete top-level JSON objects from a TCP stream buffer.</summary>
    internal static IEnumerable<string> ExtractJsonObjects(StringBuilder pending)
    {
        var results = new List<string>();
        var text = pending.ToString();
        var start = -1;
        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escape) escape = false;
                else if (ch == '\\') escape = true;
                else if (ch == '"') inString = false;
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    results.Add(text.Substring(start, i - start + 1));
                    start = -1;
                }
            }
        }

        var consumed = 0;
        if (results.Count > 0)
        {
            var last = results[^1];
            consumed = text.LastIndexOf(last, StringComparison.Ordinal) + last.Length;
        }
        else
        {
            var firstBrace = text.IndexOf('{');
            consumed = firstBrace < 0 ? text.Length : firstBrace;
        }

        if (consumed > 0)
            pending.Remove(0, Math.Min(consumed, pending.Length));

        return results;
    }
}
