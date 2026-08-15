using Xunit;

namespace UneekorRelaConnector.Tests;

public class UneekorShotParserTests
{
    [Fact]
    public void Parses_shotinfo_and_club_name()
    {
        using var dir = new TempDir();
        var shot = Path.Combine(dir.Path, "42");
        Directory.CreateDirectory(shot);
        File.WriteAllText(Path.Combine(shot, "shotinfo.json"), @"{
          ""DATA"": {
            ""ballspeed"": ""147.5"",
            ""incline"": ""14.3"",
            ""azimuth"": ""2.3"",
            ""backspin"": ""2500"",
            ""sidespin"": ""-800"",
            ""spinmag2d"": ""3250"",
            ""spinaxis2d"": ""-13.2""
          }
        }");
        File.WriteAllText(Path.Combine(shot, "ProShotInfo.json"), @"{ ""ClubName"": ""7I"" }");

        Assert.True(UneekorShotParser.TryParseShotFolder(shot, out var parsed, out var error), error);
        Assert.Equal("42", parsed.FolderName);
        Assert.Equal(147.5m, parsed.Speed);
        Assert.Equal(14.3m, parsed.Vla);
        Assert.Equal(2.3m, parsed.Hla);
        Assert.Equal(2500m, parsed.BackSpin);
        Assert.Equal(-800m, parsed.SideSpin);
        Assert.Equal(3250m, parsed.TotalSpin);
        Assert.Equal(-13.2m, parsed.SpinAxis);
        Assert.Equal("7I", parsed.ClubName);
    }

    [Fact]
    public void Computes_total_spin_from_back_and_side()
    {
        using var dir = new TempDir();
        var shot = Path.Combine(dir.Path, "1");
        Directory.CreateDirectory(shot);
        File.WriteAllText(Path.Combine(shot, "shotinfo.json"), @"{
          ""DATA"": { ""ballspeed"": ""10"", ""backspin"": ""3"", ""sidespin"": ""4"" }
        }");

        Assert.True(UneekorShotParser.TryParseShotFolder(shot, out var parsed, out var error), error);
        Assert.Equal(5m, parsed.TotalSpin);
    }

    [Fact]
    public void Rejects_missing_or_invalid_shotinfo()
    {
        using var dir = new TempDir();
        Assert.False(UneekorShotParser.TryParseShotFolder(dir.Path, out _, out var missing));
        Assert.Equal("shotinfo.json missing", missing);

        File.WriteAllText(Path.Combine(dir.Path, "shotinfo.json"), "{ \"DATA\": { \"ballspeed\": \"0\" } }");
        Assert.False(UneekorShotParser.TryParseShotFolder(dir.Path, out _, out var invalid));
        Assert.Equal("invalid ballspeed", invalid);
    }
}
