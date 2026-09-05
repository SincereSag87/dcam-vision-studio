namespace DcamVision.Dcam.Errors;

public sealed class DcamException : Exception
{
    public DcamException(string operation, int errorCode, string message)
        : base($"{message} DCAM reported: {DcamErrorTranslator.Translate(errorCode)}")
    {
        Operation = operation;
        ErrorCode = errorCode;
    }

    public string Operation { get; }

    public int ErrorCode { get; }
}
