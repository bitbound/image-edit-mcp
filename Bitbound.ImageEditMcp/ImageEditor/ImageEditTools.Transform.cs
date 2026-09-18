using System.ComponentModel;

namespace Bitbound.ImageEditMcp.ImageEditor;

public sealed partial class ImageEditTools
{
    [McpServerTool(Name = "apply_matrix_transform")]
    [Description("Applies a custom 3x3 matrix transformation to the image (scale, skew, rotate, or combine). The matrix is provided as 9 comma-separated floats in row-major order: scaleX, skewY, transX, skewX, scaleY, transY, pers0, pers1, pers2.")]
    public string ApplyMatrixTransform(
        [Description("Matrix values as a comma-separated string of 9 floats.")]
        string matrix)
    {
        var values = matrix.Split(',').Select(float.Parse).ToArray();
        if (values.Length != 9)
            throw new ArgumentException("Matrix must have 9 values (3x3).");

        var skMatrix = new SKMatrix(values);

        _dataManager.ApplyEdit(canvas =>
        {
            canvas.SetMatrix(skMatrix);
            canvas.DrawBitmap(_dataManager.CloneCurrentBitmap(), 0, 0, SKSamplingOptions.Default);
        });

        return $"Applied matrix transform: [{matrix}]";
    }

    [McpServerTool(Name = "crop_image")]
    [Description("Crops the working copy to the specified rectangular region.")]
    public string CropImage(
        [Description("X coordinate of the crop region (left).")]
        int x,
        [Description("Y coordinate of the crop region (top).")]
        int y,
        [Description("Width of the crop region.")]
        int width,
        [Description("Height of the crop region.")]
        int height)
    {
        _dataManager.ReplaceBitmap(bmp =>
        {
            var x1 = Math.Max(0, x);
            var y1 = Math.Max(0, y);
            var x2 = Math.Min(bmp.Width, x + width);
            var y2 = Math.Min(bmp.Height, y + height);
            var w = x2 - x1;
            var h = y2 - y1;

            if (w <= 0 || h <= 0) return bmp.Copy();

            var cropped = new SKBitmap(w, h, bmp.ColorType, bmp.AlphaType);
            for (int row = 0; row < h; row++)
            {
                for (int col = 0; col < w; col++)
                {
                    cropped.SetPixel(col, row, bmp.GetPixel(x1 + col, y1 + row));
                }
            }
            return cropped;
        });

        var (w, h, _, _, _, _, _) = _dataManager.GetImageInfo();
        return $"Cropped to {w}x{h}";
    }

    [McpServerTool(Name = "flip_image")]
    [Description("Flips the working copy horizontally or vertically.")]
    public string FlipImage(
        [Description("Flip direction: horizontal or vertical. Default: horizontal.")]
        string direction = "horizontal")
    {
        var isHorizontal = direction.ToLowerInvariant() == "horizontal";

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);

            if (isHorizontal)
            {
                canvas.Scale(-1, 1);
                canvas.Translate(-bmp.Width, 0);
            }
            else
            {
                canvas.Scale(1, -1);
                canvas.Translate(0, -bmp.Height);
            }

            canvas.DrawBitmap(bmp, 0, 0, SKSamplingOptions.Default);
            return newBitmap;
        });

        return $"Flipped {direction}";
    }

    [McpServerTool(Name = "resize_image")]
    [Description("Resizes the working copy to the specified width and height. If only one dimension is 0, the other is computed to preserve aspect ratio.")]
    public string ResizeImage(
        [Description("Target width in pixels. Set to 0 to auto-calculate from height and aspect ratio.")]
        int width,
        [Description("Target height in pixels. Set to 0 to auto-calculate from width and aspect ratio.")]
        int height)
    {
        _dataManager.ReplaceBitmap(bmp =>
        {
            var w = width;
            var h = height;
            if (w == 0 && h == 0) return bmp.Copy();
            if (w == 0) w = (int)(bmp.Width * (float)h / bmp.Height);
            if (h == 0) h = (int)(bmp.Height * (float)w / bmp.Width);

            var newBitmap = new SKBitmap(w, h, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            canvas.DrawBitmap(bmp, new SKRect(0, 0, w, h), SKSamplingOptions.Default);
            return newBitmap;
        });

        var (w2, h2, _, _, _, _, _) = _dataManager.GetImageInfo();
        return $"Resized to {w2}x{h2}";
    }

    [McpServerTool(Name = "rotate_image")]
    [Description("Rotates the working copy by the specified angle in degrees. Positive angles rotate clockwise.")]
    public string RotateImage(
        [Description("Rotation angle in degrees (0-360).")]
        float angle,
        [Description("Fill color for newly exposed areas as hex (e.g. '#000000' for black, '#00000000' for transparent). Default: transparent.")]
        string? fillColor = "00000000")
    {
        var color = ParseColor(fillColor, SKColors.Transparent);
        var radians = angle * Math.PI / 180.0;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        var (w, h, _, _, _, _, _) = _dataManager.GetImageInfo();

        var newW = (int)Math.Ceiling(w * cos + h * sin);
        var newH = (int)Math.Ceiling(h * cos + w * sin);

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(newW, newH, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            canvas.Clear(color);
            canvas.Translate(newW / 2f, newH / 2f);
            canvas.RotateDegrees(angle);
            canvas.DrawBitmap(bmp, -bmp.Width / 2f, -bmp.Height / 2f, SKSamplingOptions.Default);
            return newBitmap;
        });

        return $"Rotated by {angle} degrees. New dimensions: {newW}x{newH}";
    }

    [McpServerTool(Name = "scale_image")]
    [Description("Scales the working copy by a uniform factor, preserving aspect ratio.")]
    public string ScaleImage(
        [Description("Scale factor (e.g. 0.5 for half size, 2.0 for double size).")]
        float scale)
    {
        var (w, h, _, _, _, _, _) = _dataManager.GetImageInfo();
        return ResizeImage(
            (int)(w * scale),
            (int)(h * scale));
    }
}
