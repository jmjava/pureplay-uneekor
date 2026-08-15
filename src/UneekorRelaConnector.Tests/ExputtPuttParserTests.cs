using System.Text;
using Xunit;

namespace UneekorRelaConnector.Tests;

public class ExputtPuttParserTests
{
    [Fact]
    public void Parses_flat_exputt_json()
    {
        const string json = "{ \"source\": \"exputt\", \"speed\": 4.2, \"hla\": -1.5, \"vla\": 0, \"path\": 0.3, \"faceToTarget\": 0.1 }";

        Assert.True(ExputtPuttParser.TryParse(json, out var putt, out var error), error);
        Assert.Equal(4.2m, putt.Speed);
        Assert.Equal(-1.5m, putt.Hla);
        Assert.Equal(0.3m, putt.Path);
        Assert.Equal(0.1m, putt.FaceToTarget);
        Assert.Equal("exputt", putt.Source);
    }

    [Fact]
    public void Parses_open_connect_shot()
    {
        const string json = @"{
              ""DeviceID"": ""springbok"",
              ""BallData"": { ""Speed"": 6.1, ""HLA"": 2.0, ""VLA"": 0.0, ""TotalSpin"": 0 },
              ""ClubData"": { ""Path"": -0.4, ""FaceToTarget"": 0.2 },
              ""ShotDataOptions"": { ""ContainsBallData"": true, ""IsHeartBeat"": false }
            }";

        Assert.True(ExputtPuttParser.TryParse(json, out var putt, out var error), error);
        Assert.Equal(6.1m, putt.Speed);
        Assert.Equal(2.0m, putt.Hla);
        Assert.Equal(-0.4m, putt.Path);
        Assert.Equal("springbok", putt.Source);
    }

    [Fact]
    public void Parses_string_numbers_and_aliases()
    {
        const string json = "{ \"ballspeed\": \"5.5\", \"LaunchDirection\": \"-2\", \"face_to_target\": \"0.4\" }";
        Assert.True(ExputtPuttParser.TryParse(json, out var putt, out var error), error);
        Assert.Equal(5.5m, putt.Speed);
        Assert.Equal(-2m, putt.Hla);
        Assert.Equal(0.4m, putt.FaceToTarget);
    }

    [Theory]
    [InlineData(null, "empty payload")]
    [InlineData("", "empty payload")]
    [InlineData("   ", "empty payload")]
    [InlineData("[]", "json root must be an object")]
    [InlineData("{", "invalid json")]
    [InlineData("{ \"hla\": 1 }", "missing or invalid speed")]
    [InlineData("{ \"speed\": 0 }", "missing or invalid speed")]
    public void Rejects_bad_payloads(string? json, string expectedError)
    {
        Assert.False(ExputtPuttParser.TryParse(json!, out _, out var error));
        Assert.Contains(expectedError, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_heartbeat()
    {
        const string json = "{ \"ShotDataOptions\": { \"IsHeartBeat\": true, \"ContainsBallData\": false } }";

        Assert.True(ExputtPuttParser.IsHeartbeat(json));
        Assert.False(ExputtPuttParser.TryParse(json, out _, out var error));
        Assert.Equal("heartbeat", error);
    }

    [Fact]
    public void Treats_contains_ball_data_false_as_heartbeat()
    {
        const string json = "{ \"ShotDataOptions\": { \"ContainsBallData\": false } }";
        Assert.True(ExputtPuttParser.IsHeartbeat(json));
    }

    [Theory]
    [InlineData("PT", true)]
    [InlineData("putter", true)]
    [InlineData("PUTTING", true)]
    [InlineData("7I", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void Detects_putter_club(string? club, bool expected)
        => Assert.Equal(expected, ExputtPuttParser.IsPutterClub(club));

    [Theory]
    [InlineData("PUTTING", "7I", false, true)]
    [InlineData("NORMAL", "PT", true, true)]
    [InlineData("NORMAL", "PT", false, false)]
    [InlineData("NORMAL", "DR", true, false)]
    public void Detects_putting_active(string mode, string club, bool auto, bool expected)
        => Assert.Equal(expected, ExputtPuttParser.IsPuttingActive(mode, club, auto));

    [Fact]
    public void Extracts_concatenated_json_objects()
    {
        var pending = new StringBuilder("{\"a\":1}{\"b\":2}");
        var objects = GsproOpenConnectServer.ExtractJsonObjects(pending).ToList();
        Assert.Equal(2, objects.Count);
        Assert.Equal("{\"a\":1}", objects[0]);
        Assert.Equal("{\"b\":2}", objects[1]);
        Assert.Equal(0, pending.Length);
    }

    [Fact]
    public void Leaves_partial_json_in_buffer()
    {
        var pending = new StringBuilder("{\"a\":1}{\"b\":");
        var objects = GsproOpenConnectServer.ExtractJsonObjects(pending).ToList();
        Assert.Single(objects);
        Assert.Equal("{\"b\":", pending.ToString());
    }

    [Fact]
    public void Ignores_braces_inside_strings()
    {
        var pending = new StringBuilder("{\"note\":\"{not an object}\"}");
        var objects = GsproOpenConnectServer.ExtractJsonObjects(pending).ToList();
        Assert.Single(objects);
        Assert.Equal("{\"note\":\"{not an object}\"}", objects[0]);
        Assert.Equal(0, pending.Length);
    }
}
