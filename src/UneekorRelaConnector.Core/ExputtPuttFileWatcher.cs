using System.IO;

namespace UneekorRelaConnector;

/// <summary>
/// Polls a folder for new ExPutt JSON drops (flat or GSPro Open Connect).
/// Processed files are moved to a <c>processed</c> subfolder.
/// </summary>
internal sealed class ExputtPuttFileWatcher : IDisposable
{
    private readonly string _puttDir;
    private readonly TimeSpan _pollInterval;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ExputtPuttFileWatcher(string puttDir, TimeSpan? pollInterval = null)
    {
        _puttDir = puttDir;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(400);
    }

    public event Action<ParsedPutt>? PuttDetected;
    public event Action<string>? Log;

    public static string DefaultPuttDirectory()
        => Path.Combine(AppContext.BaseDirectory, "Settings", "Other", "exputt-putts");

    public void Start(bool ignoreExisting = true)
    {
        lock (_gate)
        {
            Stop_NoLock();
            Directory.CreateDirectory(_puttDir);
            Directory.CreateDirectory(ProcessedDir());
            _cts = new CancellationTokenSource();
            if (ignoreExisting)
            {
                foreach (var name in ListJsonFiles())
                    _seen.Add(name);
            }

            var token = _cts.Token;
            _loop = Task.Run(() => PollLoop(token), token);
            Log?.Invoke($"Watching putt drops in {_puttDir} (existing={_seen.Count} ignored={ignoreExisting})");
        }
    }

    public void Stop()
    {
        lock (_gate) Stop_NoLock();
    }

    private void Stop_NoLock()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _loop?.Wait(500); } catch { /* ignore */ }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    public void Dispose() => Stop();

    private async Task PollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (Directory.Exists(_puttDir))
                {
                    foreach (var name in ListJsonFiles().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    {
                        if (_seen.Contains(name))
                            continue;

                        var path = Path.Combine(_puttDir, name);
                        string json;
                        try
                        {
                            json = File.ReadAllText(path);
                        }
                        catch
                        {
                            // mid-write
                            continue;
                        }

                        if (ExputtPuttParser.IsHeartbeat(json))
                        {
                            _seen.Add(name);
                            MoveProcessed(path, name);
                            continue;
                        }

                        if (!ExputtPuttParser.TryParse(json, out var putt, out var error))
                        {
                            if (error is not null && error.Contains("invalid json", StringComparison.OrdinalIgnoreCase))
                                continue;
                            Log?.Invoke($"Skip {name}: {error}");
                            _seen.Add(name);
                            MoveProcessed(path, name);
                            continue;
                        }

                        _seen.Add(name);
                        MoveProcessed(path, name);
                        PuttDetected?.Invoke(putt);
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("Putt watch error: " + ex.Message);
            }

            try { await Task.Delay(_pollInterval, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private IEnumerable<string> ListJsonFiles()
    {
        try
        {
            return Directory.EnumerateFiles(_puttDir, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private string ProcessedDir() => Path.Combine(_puttDir, "processed");

    private void MoveProcessed(string path, string name)
    {
        try
        {
            var dest = Path.Combine(ProcessedDir(), DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + name);
            File.Move(path, dest, overwrite: true);
        }
        catch
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }
}
