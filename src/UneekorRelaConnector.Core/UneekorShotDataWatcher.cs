using System.IO;

namespace UneekorRelaConnector;

/// <summary>
/// Polls Uneekor VIEW ShotData folders for new shots (same approach as Open-Birdie uneekor-watch.js).
/// </summary>
internal sealed class UneekorShotDataWatcher : IDisposable
{
    private readonly string _shotDataDir;
    private readonly TimeSpan _pollInterval;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public UneekorShotDataWatcher(string shotDataDir, TimeSpan? pollInterval = null)
    {
        _shotDataDir = shotDataDir;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(400);
    }

    public event Action<ParsedUneekorShot>? ShotDetected;
    public event Action<string>? Log;

    public static string? DefaultShotDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            return null;

        // %LOCALAPPDATA%\..\LocalLow\Uneekor\VIEW\ShotData
        var dir = Path.GetFullPath(Path.Combine(localAppData, "..", "LocalLow", "Uneekor", "VIEW", "ShotData"));
        return Directory.Exists(dir) ? dir : dir; // return expected path even if missing; Connect validates
    }

    public void Start(bool ignoreExisting = true)
    {
        lock (_gate)
        {
            Stop_NoLock();
            _cts = new CancellationTokenSource();
            if (ignoreExisting && Directory.Exists(_shotDataDir))
            {
                foreach (var name in ListShotFolders())
                    _seen.Add(name);
            }

            var token = _cts.Token;
            _loop = Task.Run(() => PollLoop(token), token);
            Log?.Invoke($"Watching {_shotDataDir} (existing={_seen.Count} ignored={ignoreExisting})");
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
        var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (Directory.Exists(_shotDataDir))
                {
                    foreach (var name in ListShotFolders())
                    {
                        if (!_seen.Contains(name))
                            pending.Add(name);
                    }

                    foreach (var name in pending.OrderBy(NumericKey))
                    {
                        var folder = Path.Combine(_shotDataDir, name);
                        if (!UneekorShotParser.TryParseShotFolder(folder, out var shot, out var error))
                        {
                            // mid-write: retry next poll
                            if (error is not null && error.Contains("not ready", StringComparison.OrdinalIgnoreCase))
                                continue;
                            continue;
                        }

                        pending.Remove(name);
                        _seen.Add(name);
                        ShotDetected?.Invoke(shot);
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("Watch error: " + ex.Message);
            }

            try { await Task.Delay(_pollInterval, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private IEnumerable<string> ListShotFolders()
    {
        try
        {
            return Directory.EnumerateDirectories(_shotDataDir)
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

    private static int NumericKey(string name)
        => int.TryParse(name, out var n) ? n : int.MaxValue;
}
