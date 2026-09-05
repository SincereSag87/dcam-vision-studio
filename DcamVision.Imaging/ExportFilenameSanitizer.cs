namespace DcamVision.Imaging;

public static class ExportFilenameSanitizer
{
    private static readonly char[] InvalidCharacters = Path.GetInvalidFileNameChars();

    public static string SanitizeComponent(string value, int maximumLength = 96)
    {
        var sanitized = new string(value.Select(character =>
            InvalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character).ToArray());
        sanitized = string.Join('_', sanitized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        sanitized = sanitized.Trim().TrimEnd('.', ' ');

        if (sanitized.Length > maximumLength)
        {
            sanitized = sanitized[..maximumLength].TrimEnd('.', ' ');
        }

        return sanitized.Length == 0 ? "capture" : sanitized;
    }
}
