namespace FatigueGuard.Core.Config;

public sealed class ConfigValidationException : Exception
{
    public ConfigValidationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
