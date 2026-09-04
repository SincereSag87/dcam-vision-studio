namespace DcamVision.Imaging;

public static class HistogramScaleTransformer
{
    public static double Transform(int count, HistogramScale scale)
    {
        if (count <= 0)
        {
            return 0;
        }

        return scale switch
        {
            HistogramScale.Linear => count,
            HistogramScale.Logarithmic => Math.Log10(count + 1),
            _ => count
        };
    }
}
