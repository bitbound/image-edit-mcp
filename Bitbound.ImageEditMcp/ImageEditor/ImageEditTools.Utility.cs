using System.ComponentModel;
using ModelContextProtocol.Server;
using SkiaSharp;

namespace Bitbound.ImageEditMcp.ImageEditor;

public sealed partial class ImageEditTools
{
    [McpServerTool(Name = "create_thumbnail")]
    [Description("Resizes the working copy to fit within the given maximum dimensions, preserving aspect ratio. Does not upscale.")]
    public string CreateThumbnail(
        [Description("Maximum width of the thumbnail.")]
        int maxWidth,
        [Description("Maximum height of the thumbnail.")]
        int maxHeight)
    {
        var (w, h, _, _, _, _, _) = _dataManager.GetImageInfo();
        var ratio = Math.Min((float)maxWidth / w, (float)maxHeight / h);
        if (ratio > 1) ratio = 1;

        var newW = (int)(w * ratio);
        var newH = (int)(h * ratio);

        if (newW == w && newH == h)
            return $"Image already within {maxWidth}x{maxHeight}. Dimensions: {w}x{h}";

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(newW, newH, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            canvas.DrawBitmap(bmp, new SKRect(0, 0, newW, newH));
            return newBitmap;
        });

        return $"Created thumbnail: {newW}x{newH}";
    }

    [McpServerTool(Name = "trim_image")]
    [Description("Trims border pixels that match the corner color from the edges of the image.")]
    public string TrimImage(
        [Description("Tolerance for color matching at borders (0-255). Default: 0 (exact match).")]
        int tolerance = 0)
    {
        _dataManager.ReplaceBitmap(bmp =>
        {
            var tol = (byte)Math.Clamp(tolerance, 0, 255);
            var tl = bmp.GetPixel(0, 0);
            var tr = bmp.GetPixel(bmp.Width - 1, 0);
            var bl = bmp.GetPixel(0, bmp.Height - 1);
            var br = bmp.GetPixel(bmp.Width - 1, bmp.Height - 1);

            var minX = bmp.Width;
            var minY = bmp.Height;
            var maxX = 0;
            var maxY = 0;

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var color = bmp.GetPixel(x, y);
                    if (ColorDistance(color, tl) > tol &&
                        ColorDistance(color, tr) > tol &&
                        ColorDistance(color, bl) > tol &&
                        ColorDistance(color, br) > tol)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY) return bmp.Copy();

            var w = maxX - minX + 1;
            var h = maxY - minY + 1;
            var cropped = new SKBitmap(w, h, bmp.ColorType, bmp.AlphaType);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    cropped.SetPixel(x, y, bmp.GetPixel(minX + x, minY + y));
                }
            }
            return cropped;
        });

        var (w, h, _, _, _, _, _) = _dataManager.GetImageInfo();
        return $"Trimmed to {w}x{h}";
    }
}
