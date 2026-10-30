namespace FatigueGuard.Cli;

public static class ModelLocator
{
    public static string? Find(string fileName)
    {
        foreach (string root in Roots())
        {
            string candidate = Path.Combine(root, "models", fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    public static IEnumerable<string> Roots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? root in new[]
                 {
                     AppContext.BaseDirectory,
                     Directory.GetCurrentDirectory(),
                     Path.GetDirectoryName(Environment.ProcessPath),
                 })
        {
            if (string.IsNullOrEmpty(root))
            {
                continue;
            }

            string full = new DirectoryInfo(root).FullName;
            if (seen.Add(full))
            {
                yield return full;
            }
        }
    }
}
