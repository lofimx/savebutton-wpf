using System.IO;
using System.Windows.Media.Imaging;
using Svg.Skia;

namespace Kaya.Wpf;

public static class SvgRenderer
{
    public static BitmapImage? RenderToBitmap(string filePath, int maxWidth = 512)
    {
        using var svg = new SKSvg();
        if (svg.Load(filePath) is null)
            return null;

        var picture = svg.Picture!;
        var bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        var scale = Math.Min(maxWidth / bounds.Width, maxWidth / bounds.Height);
        var width = (int)Math.Ceiling(bounds.Width * scale);
        var height = (int)Math.Ceiling(bounds.Height * scale);

        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        canvas.Scale(scale, scale);
        canvas.DrawPicture(picture);

        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = ms;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
