using SkiaSharp;

namespace RaspberryAzure.ImageRecognition;

public class ImageComparer
{
    public static float ComputePixelDiff(byte[] lastBytes, byte[] incomingBytes)
    {
        using var lastBitmap     = SKBitmap.Decode(lastBytes);
        using var incomingBitmap = SKBitmap.Decode(incomingBytes);

        // Resize to 64x64
        var info = new SKImageInfo(64, 64, SKColorType.Gray8);
        using var lastResized     = lastBitmap.Resize(info, SKFilterQuality.Low);
        using var incomingResized = incomingBitmap.Resize(info, SKFilterQuality.Low);

        int totalPixels  = 64 * 64;
        int changedPixels = 0;

        var lastSpan     = lastResized.GetPixelSpan();
        var incomingSpan = incomingResized.GetPixelSpan();

        for (int i = 0; i < lastSpan.Length; i++)
        {
            var diff = Math.Abs(lastSpan[i] - incomingSpan[i]);
            if (diff > 30)
                changedPixels++;
        }

        return (float)changedPixels / totalPixels;
    }
}