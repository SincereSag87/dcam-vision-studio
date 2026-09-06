namespace DcamVision.Imaging.Diagnostics;

public static class DiagnosticsPaths
{
    public static string DefaultApplicationDataDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DCAMVisionStudio");
    }

    public static string DefaultLogDirectory()
    {
        return Path.Combine(DefaultApplicationDataDirectory(), "Logs");
    }

    public static string DefaultSupportBundleDirectory()
    {
        return Path.Combine(DefaultApplicationDataDirectory(), "SupportBundles");
    }
}
