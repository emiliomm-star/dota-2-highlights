using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DotaHighlights.Client.Imaging;

/// <summary>
/// Comprime píxeles BGRA crudos a JPEG usando el codificador de WPF
/// (no requiere dependencias externas). Los objetos se congelan para poder
/// usarse desde hilos en segundo plano.
/// </summary>
public static class JpegEncoder
{
    public static byte[] FromBgra(byte[] bgra, int width, int height, int stride, int quality = 80)
    {
        var source = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Bgra32, null, bgra, stride);
        source.Freeze();

        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
