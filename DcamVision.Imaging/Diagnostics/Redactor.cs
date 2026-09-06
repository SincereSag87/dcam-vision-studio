using System.Text.RegularExpressions;

namespace DcamVision.Imaging.Diagnostics;

public static partial class Redactor
{
    public static string Redact(string value, bool includeFullPaths = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = includeFullPaths ? value : UserProfilePathRegex().Replace(value, "%USERPROFILE%");
        redacted = LocalAppDataRegex().Replace(redacted, "%LOCALAPPDATA%");
        return redacted;
    }

    [GeneratedRegex(@"[A-Za-z]:\\Users\\[^\\\r\n]+", RegexOptions.IgnoreCase)]
    private static partial Regex UserProfilePathRegex();

    [GeneratedRegex(@"[A-Za-z]:\\Users\\[^\\\r\n]+\\AppData\\Local", RegexOptions.IgnoreCase)]
    private static partial Regex LocalAppDataRegex();
}
