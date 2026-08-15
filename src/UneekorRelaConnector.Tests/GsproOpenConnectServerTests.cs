using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UneekorRelaConnector.Tests;

public class GsproOpenConnectServerTests
{
    [Fact]
    public async Task Accepts_putt_and_acks_200()
    {
        using var server = new GsproOpenConnectServer("127.0.0.1", 0);
        var tcs = new TaskCompletionSource<ParsedPutt>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.PuttDetected += putt => tcs.TrySetResult(putt);
        server.Start();
        Assert.True(server.Port > 0);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.Port);
        var stream = client.GetStream();
        await ReadJsonAsync(stream);
        await WriteAsync(stream, "{ \"DeviceID\": \"test\", \"BallData\": { \"Speed\": 5.5, \"HLA\": 1.0 } }");
        var ack = await ReadJsonAsync(stream);
        Assert.Equal(200, ack.GetProperty("Code").GetInt32());

        var putt = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(5.5m, putt.Speed);
        Assert.Equal(1.0m, putt.Hla);
    }

    [Fact]
    public async Task Sends_player_info_on_connect_and_update()
    {
        using var server = new GsproOpenConnectServer("127.0.0.1", 0);
        server.Start();
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.Port);
        var stream = client.GetStream();
        var hello = await ReadJsonAsync(stream);
        Assert.Equal(201, hello.GetProperty("Code").GetInt32());
        Assert.Equal("DR", hello.GetProperty("Player").GetProperty("Club").GetString());

        server.SetPlayer("LH", "PT");
        var update = await ReadJsonAsync(stream);
        Assert.Equal("LH", update.GetProperty("Player").GetProperty("Handed").GetString());
        Assert.Equal("PT", update.GetProperty("Player").GetProperty("Club").GetString());
    }

    [Fact]
    public async Task Heartbeat_does_not_emit_putt()
    {
        using var server = new GsproOpenConnectServer("127.0.0.1", 0);
        var seen = new List<ParsedPutt>();
        server.PuttDetected += seen.Add;
        server.Start();
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.Port);
        var stream = client.GetStream();
        await ReadJsonAsync(stream);
        await WriteAsync(stream, "{ \"ShotDataOptions\": { \"IsHeartBeat\": true } }");
        var ack = await ReadJsonAsync(stream);
        Assert.Equal(200, ack.GetProperty("Code").GetInt32());
        await Task.Delay(100);
        Assert.Empty(seen);
    }

    private static async Task WriteAsync(NetworkStream stream, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    private static async Task<JsonElement> ReadJsonAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
        Assert.True(n > 0, "expected JSON from server");
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, n));
        return doc.RootElement.Clone();
    }
}
