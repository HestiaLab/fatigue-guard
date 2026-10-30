using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Tests;

public sealed class ConfigTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        IReadOnlyList<string> errors = ConfigLoader.Validate(new FatigueConfig());
        Assert.Empty(errors);
    }

    [Fact]
    public void Load_OverridesOnlyProvidedValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fatigueguard-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "camera": { "index": 2 },
              "fatigue": { "perclosWarn": 0.2 },
              "display": { "language": "en" }
            }
            """);

        try
        {
            FatigueConfig config = ConfigLoader.Load(path);

            Assert.Equal(2, config.Camera.Index);
            Assert.Equal(640, config.Camera.Width);
            Assert.Equal(0.2, config.Fatigue.PerclosWarn, 6);
            Assert.Equal("en", config.Display.Language);
            Assert.Empty(ConfigLoader.Validate(config));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidJson_IsReported()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fatigueguard-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");

        try
        {
            ConfigValidationException ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(path));
            Assert.Contains("JSON", ex.Errors[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvertedPerclosThresholds_AreRejectedWithFieldName()
    {
        var config = new FatigueConfig();
        config.Fatigue.PerclosCritical = 0.10;
        config.Fatigue.PerclosWarn = 0.20;

        IReadOnlyList<string> errors = ConfigLoader.Validate(config);

        Assert.Contains(errors, e => e.Contains("fatigue.perclosCritical"));
    }

    [Fact]
    public void WindowSmallerThanMinimumValid_IsRejected()
    {
        var config = new FatigueConfig();
        config.Metrics.WindowSeconds = 20;
        config.Metrics.MinValidSeconds = 30;

        IReadOnlyList<string> errors = ConfigLoader.Validate(config);

        Assert.Contains(errors, e => e.Contains("metrics.minValidSeconds"));
    }

    [Fact]
    public void NegativeValues_AreRejected()
    {
        var config = new FatigueConfig();
        config.Camera.Fps = -1;
        config.Blink.MinClosedMs = -5;
        config.Display.RefreshHz = 0;

        IReadOnlyList<string> errors = ConfigLoader.Validate(config);

        Assert.Contains(errors, e => e.Contains("camera.fps"));
        Assert.Contains(errors, e => e.Contains("blink.minClosedMs"));
        Assert.Contains(errors, e => e.Contains("display.refreshHz"));
    }

    [Fact]
    public void NullSection_IsRejected()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fatigueguard-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "camera": null }""");

        try
        {
            FatigueConfig config = ConfigLoader.Load(path);
            IReadOnlyList<string> errors = ConfigLoader.Validate(config);
            Assert.Contains(errors, e => e.Contains("camera"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidLanguageOrColor_IsRejected()
    {
        var config = new FatigueConfig();
        config.Display.Language = "fr";
        config.Display.Color = "rainbow";

        IReadOnlyList<string> errors = ConfigLoader.Validate(config);

        Assert.Contains(errors, e => e.Contains("display.language"));
        Assert.Contains(errors, e => e.Contains("display.color"));
    }
}
