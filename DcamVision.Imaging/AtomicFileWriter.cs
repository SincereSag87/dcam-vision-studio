using System.Security.Cryptography;

namespace DcamVision.Imaging;

public static class AtomicFileWriter
{
    public static async Task<CaptureExportFileResult> WriteAsync(
        string path,
        CaptureExportFormat format,
        Func<Stream, CancellationToken, Task> writeAsync,
        string baseDirectory,
        CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await writeAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
            var bytes = new FileInfo(path).Length;
            var sha = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
            return new CaptureExportFileResult(format, Path.GetRelativePath(baseDirectory, path), bytes, sha);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
