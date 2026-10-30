namespace FatigueGuard.Core.Sources;

public enum FrameSourceKind
{
    Camera,
    Video,
}

public sealed class FrameSourceException : Exception
{
    public FrameSourceException(FrameSourceKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public FrameSourceKind Kind { get; }
}
