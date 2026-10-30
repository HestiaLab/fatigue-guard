using System.Globalization;
using System.Text;
using FatigueGuard.Core;

namespace FatigueGuard.Cli;

public sealed class MetricsCsvWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private long _nextSecond;

    public MetricsCsvWriter(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(fullPath, false, new UTF8Encoding(false))
        {
            NewLine = "\n",
        };
        _writer.WriteLine(
            "time_s,face_count,ear,ear_baseline,ear_threshold,perclos60,blink_rate_per_min,valid_seconds," +
            "blinks_60,long_closures_60,microsleeps_60,quality_valid_ratio_10s,state");
    }

    public void MaybeWrite(FatigueUpdate update)
    {
        double seconds = update.StreamTime.TotalSeconds;
        if (seconds < _nextSecond)
        {
            return;
        }

        _writer.Write(_nextSecond.ToString(CultureInfo.InvariantCulture));
        _writer.Write(',');
        _writer.Write(update.FaceCount.ToString(CultureInfo.InvariantCulture));
        _writer.Write(',');
        _writer.Write(Format(update.SmoothedEar));
        _writer.Write(',');
        _writer.Write(Format(update.BaselineEar));
        _writer.Write(',');
        _writer.Write(Format(update.ClosedThreshold));
        _writer.Write(',');
        _writer.Write(Format(update.Perclos));
        _writer.Write(',');
        _writer.Write(Format(update.BlinkRatePerMinute));
        _writer.Write(',');
        _writer.Write(Format(update.ValidSecondsInWindow));
        _writer.Write(',');
        _writer.Write(update.BlinksInWindow.ToString(CultureInfo.InvariantCulture));
        _writer.Write(',');
        _writer.Write(update.LongClosuresInWindow.ToString(CultureInfo.InvariantCulture));
        _writer.Write(',');
        _writer.Write(update.MicrosleepsInLookback.ToString(CultureInfo.InvariantCulture));
        _writer.Write(',');
        _writer.Write(Format(update.QualityValidRatio));
        _writer.Write(',');
        _writer.Write(((int)update.DisplayState).ToString(CultureInfo.InvariantCulture));
        _writer.Write('\n');
        _writer.Flush();

        _nextSecond = (long)Math.Floor(seconds) + 1;
    }

    public void Dispose() => _writer.Dispose();

    private static string Format(double? value)
        => value.HasValue ? value.Value.ToString("F3", CultureInfo.InvariantCulture) : string.Empty;
}
