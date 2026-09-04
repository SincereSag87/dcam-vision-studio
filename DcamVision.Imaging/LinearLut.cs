namespace DcamVision.Imaging;

public sealed record LinearLut(ushort BlackPoint, ushort WhitePoint)
{
    public byte Map(ushort value)
    {
        if (WhitePoint <= BlackPoint)
        {
            throw new InvalidOperationException("White point must be greater than black point.");
        }

        if (value <= BlackPoint)
        {
            return 0;
        }

        if (value >= WhitePoint)
        {
            return byte.MaxValue;
        }

        var normalized = (value - BlackPoint) / (double)(WhitePoint - BlackPoint);
        return (byte)Math.Round(normalized * byte.MaxValue);
    }
}
