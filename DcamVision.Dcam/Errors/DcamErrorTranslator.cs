using DcamVision.Dcam.Interop;

namespace DcamVision.Dcam.Errors;

public static class DcamErrorTranslator
{
    private static readonly Dictionary<int, string> KnownErrors = new()
    {
        [0] = "FALSE",
        [DcamConstants.ErrorTimeout] = "TIMEOUT",
        [unchecked((int)0x80000101)] = "BUSY",
        [unchecked((int)0x80000103)] = "NOT READY",
        [unchecked((int)0x80000104)] = "NOT STABLE",
        [unchecked((int)0x80000105)] = "UNSTABLE",
        [unchecked((int)0x80000201)] = "INVALID CAMERA",
        [unchecked((int)0x80000202)] = "INVALID HANDLE",
        [unchecked((int)0x80000203)] = "INVALID PARAMETER",
        [unchecked((int)0x80000204)] = "INVALID VALUE",
        [unchecked((int)0x80000801)] = "NO RESOURCE",
        [unchecked((int)0x80000f01)] = "NOT INITIALIZED"
    };

    public static string Translate(int errorCode)
    {
        if (errorCode >= DcamConstants.Success)
        {
            return "SUCCESS";
        }

        return KnownErrors.TryGetValue(errorCode, out var description)
            ? description
            : $"Unknown DCAM error 0x{unchecked((uint)errorCode):X8}";
    }

    public static void ThrowIfFailed(int result, string operation, string friendlyMessage)
    {
        if (result < DcamConstants.Success)
        {
            throw new DcamException(operation, result, friendlyMessage);
        }
    }
}
