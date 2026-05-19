using SkiaSharp;

namespace RaspberryAzure.ImageRecognition.Services;

public class ImageComparer
{
    public static float ComputePixelDiff(byte[] lastBytes, byte[] incomingBytes)
    {
        using var lastBitmap     = SKBitmap.Decode(lastBytes);
        using var incomingBitmap = SKBitmap.Decode(incomingBytes);

        var info = new SKImageInfo(64, 64, SKColorType.Gray8);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear);
        using var lastResized     = lastBitmap.Resize(info, sampling);
        using var incomingResized = incomingBitmap.Resize(info, sampling);

        var lastSpan     = lastResized.GetPixelSpan();
        var incomingSpan = incomingResized.GetPixelSpan();

        int changedPixels = 0;
        for (int i = 0; i < lastSpan.Length; i++)
        {
            if (Math.Abs(lastSpan[i] - incomingSpan[i]) > 30)
                changedPixels++;
        }

        return (float)changedPixels / (64 * 64);
    }
}
