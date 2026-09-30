using Xunit;

namespace UneekorRelaConnector.Tests;

public class ExputtPuttFileWatcherTests
{
    [Fact]
    public async Task Detects_new_putt_and_moves_processed()
    {
        using var dir = new TempDir();
        using var watcher = new ExputtPuttFileWatcher(dir.Path, TimeSpan.FromMilliseconds(40));
        var tcs = new TaskCompletionSource<ParsedPutt>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.PuttDetected += putt => tcs.TrySetResult(putt);
        watcher.Start(ignoreExisting: true);

        File.WriteAllText(Path.Combine(dir.Path, "putt.json"), "{ \"speed\": 4.2, \"hla\": -1.5 }");
        var putt = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(4.2m, putt.Speed);
        Assert.Equal(-1.5m, putt.Hla);
        Assert.False(File.Exists(Path.Combine(dir.Path, "putt.json")));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(dir.Path, "processed"), "*.json"));
    }

    [Fact]
    public async Task Ignores_existing_files_on_start()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "old.json"), "{ \"speed\": 3 }");
        using var watcher = new ExputtPuttFileWatcher(dir.Path, TimeSpan.FromMilliseconds(40));
        var seen = new List<ParsedPutt>();
        watcher.PuttDetected += seen.Add;
        watcher.Start(ignoreExisting: true);
        await Task.Delay(200);
        Assert.Empty(seen);
    }

    [Fact]
    public async Task Skips_heartbeat_without_emitting()
    {
        using var dir = new TempDir();
        using var watcher = new ExputtPuttFileWatcher(dir.Path, TimeSpan.FromMilliseconds(40));
        var seen = new List<ParsedPutt>();
        watcher.PuttDetected += seen.Add;
        watcher.Start(ignoreExisting: true);
        File.WriteAllText(
            Path.Combine(dir.Path, "hb.json"),
            "{ \"ShotDataOptions\": { \"IsHeartBeat\": true } }");
        await Task.Delay(300);
        Assert.Empty(seen);
        Assert.False(File.Exists(Path.Combine(dir.Path, "hb.json")));
    }
}
