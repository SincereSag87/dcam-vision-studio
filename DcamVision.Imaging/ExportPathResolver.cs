namespace DcamVision.Imaging;

public sealed class ExportPathResolver
{
    private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);

    public string Resolve(
        string directory,
        string baseName,
        string extension,
        ExportCollisionBehavior collisionBehavior)
    {
        Directory.CreateDirectory(directory);
        var candidate = Path.Combine(directory, baseName + extension);

        if (collisionBehavior == ExportCollisionBehavior.Overwrite)
        {
            _reserved.Add(candidate);
            return candidate;
        }

        if (collisionBehavior == ExportCollisionBehavior.Skip && (File.Exists(candidate) || _reserved.Contains(candidate)))
        {
            return string.Empty;
        }

        var index = 0;
        var resolved = candidate;
        while (File.Exists(resolved) || _reserved.Contains(resolved))
        {
            index++;
            resolved = Path.Combine(directory, $"{baseName}_{index:000}{extension}");
        }

        _reserved.Add(resolved);
        return resolved;
    }
}
