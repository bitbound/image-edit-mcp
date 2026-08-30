using System.ComponentModel;
using ImageEditMcp.ImageEditor;
using ModelContextProtocol.Server;
using SkiaSharp;

namespace ImageEditMcp;

/// <summary>
/// MCP tools that expose SkiaSharp image editing APIs.
/// Each tool operates on a working copy loaded by <c>load_image</c>.
/// </summary>
[McpServerToolType]
public sealed class ImageEditTools
{
    private readonly ImageDataManager _dataManager;

    public ImageEditTools(ImageDataManager dataManager)
    {
        _dataManager = dataManager;
    }

    #region Core Operations

    [McpServerTool(Name = "load_image")]
    [Description("Loads an image from the given file path and creates a working copy in the local data folder. All subsequent edits operate on this working copy until a new image is loaded.")]
    public string LoadImage(
        [Description("Absolute or relative path to the image file to load (PNG, JPEG, WEBP, BMP, GIF, TGA).")]
        string filePath)
    {
        var (width, height, format, sizeBytes) = _dataManager.LoadImage(filePath);
        return $"Loaded image '{filePath}' into working copy.\n" +
               $"Dimensions: {width}x{height}\n" +
               $"Format: {format}\n" +
               $"Size: {sizeBytes} bytes\n" +
               $"Data directory: {_dataManager.DataDirectory}";
    }

    [McpServerTool(Name = "read_image")]
    [Description("Reads the current working copy image and returns it as a base64-encoded PNG data URI. After any edit tool is called, this returns the most recent state of the image.")]
    public string ReadImage()
    {
        var base64 = _dataManager.GetImageDataBase64();
        return $"data:image/png;base64,{base64}";
    }

    [McpServerTool(Name = "save_image")]
    [Description("Saves the current working copy to a new file path. The original loaded file is not modified.")]
    public string SaveImage(
        [Description("Output file path to save the image to (format determined by extension).")]
        string outputPath,
        [Description("Encoding quality 0-100 (mainly affects JPEG/WEBP). Default 90.")]
        int quality = 90)
    {
        var savedPath = _dataManager.SaveImage(outputPath, quality);
        return $"Image saved to: {savedPath}";
    }

    [McpServerTool(Name = "get_image_info")]
    [Description("Returns metadata about the currently loaded image including dimensions, format, source path, working copy path, and dirty state.")]
    public string GetImageInfo()
    {
        var (width, height, format, sourcePath, workingCopyPath, dirty, workingCopySize) = _dataManager.GetImageInfo();
        return $"Image Info:\n" +
               $"  Dimensions: {width}x{height}\n" +
               $"  Format: {format}\n" +
               $"  Source: {sourcePath ?? "none"}\n" +
               $"  Working Copy: {workingCopyPath ?? "none"} ({workingCopySize} bytes)\n" +
               $"  Has Unsaved Changes: {dirty}";
    }

    [McpServerTool(Name = "save_snapshot")]
    [Description("Saves a named snapshot of the current working copy state, allowing you to revert to it later with load_snapshot.")]
    public string SaveSnapshot(
        [Description("A name for the snapshot. If empty, uses a timestamp.")]
        string snapshotName = "")
    {
        var path = _dataManager.SaveSnapshot(snapshotName);
        return $"Snapshot saved to: {path}";
    }

    [McpServerTool(Name = "load_snapshot")]
    [Description("Loads a previously saved snapshot, restoring the working copy to that state.")]
    public string LoadSnapshot(
        [Description("Name of the snapshot file to load (without path), or full path.")]
        string snapshotName)
    {
        var result = _dataManager.LoadSnapshot(snapshotName);
        return result;
    }

    [McpServerTool(Name = "reload_original")]
    [Description("Reloads the original source image, discarding all unsaved edits to the working copy.")]
    public string ReloadOriginal()
    {
        _dataManager.ReloadOriginal();
        var (w, h, _, _, _, _, _) = _dataManager.GetImageInfo();
        return $"Reloaded original image. Dimensions: {w}x{h}";
    }

    #endregion

    #region Transform Operations

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
            canvas.DrawBitmap(bmp, new SKRect(0, 0, w, h));
            return newBitmap;
        });

        var (w2, h2, _, _, _, _, _) = _dataManager.GetImageInfo();
        return $"Resized to {w2}x{h2}";
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
            canvas.DrawBitmap(bmp, -bmp.Width / 2f, -bmp.Height / 2f);
            return newBitmap;
        });

        return $"Rotated by {angle} degrees. New dimensions: {newW}x{newH}";
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

            canvas.DrawBitmap(bmp, 0, 0);
            return newBitmap;
        });

        return $"Flipped {direction}";
    }

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
            canvas.DrawBitmap(_dataManager.CloneCurrentBitmap(), 0, 0);
        });

        return $"Applied matrix transform: [{matrix}]";
    }

    #endregion

    #region Color Adjustments

    [McpServerTool(Name = "adjust_brightness")]
    [Description("Adjusts the brightness of the working copy.")]
    public string AdjustBrightness(
        [Description("Brightness adjustment (-1.0 to 1.0). Positive increases brightness, negative decreases.")]
        float brightness)
    {
        ApplyBrightnessContrast(brightness, 0);
        return $"Adjusted brightness by {brightness}";
    }

    [McpServerTool(Name = "adjust_contrast")]
    [Description("Adjusts the contrast of the working copy.")]
    public string AdjustContrast(
        [Description("Contrast adjustment (-1.0 to 1.0). Positive increases contrast, negative decreases.")]
        float contrast)
    {
        ApplyBrightnessContrast(0, contrast);
        return $"Adjusted contrast by {contrast}";
    }

    [McpServerTool(Name = "adjust_brightness_contrast")]
    [Description("Adjusts both brightness and contrast of the working copy simultaneously.")]
    public string AdjustBrightnessContrast(
        [Description("Brightness adjustment (-1.0 to 1.0).")]
        float brightness,
        [Description("Contrast adjustment (-1.0 to 1.0).")]
        float contrast)
    {
        ApplyBrightnessContrast(brightness, contrast);
        return $"Adjusted brightness by {brightness} and contrast by {contrast}";
    }

    private void ApplyBrightnessContrast(float brightness, float contrast)
    {
        var b = brightness;
        var c = contrast;
        var matrix = new float[]
        {
            c, 0, 0, 0, b,
            0, c, 0, 0, b,
            0, 0, c, 0, b,
            0, 0, 0, 1, 0
        };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });
    }

    [McpServerTool(Name = "adjust_saturation")]
    [Description("Adjusts the saturation of the working copy.")]
    public string AdjustSaturation(
        [Description("Saturation adjustment (-1.0 to 1.0). Positive increases saturation, negative decreases (toward grayscale).")]
        float saturation)
    {
        var s = saturation;
        var rW = 0.213f;
        var gW = 0.715f;
        var bW = 0.072f;

        var matrix = new float[]
        {
            rW + (1 - rW) * s, gW - gW * s,       bW - bW * s,       0, 0,
            rW - rW * s,       gW + (1 - gW) * s, bW - bW * s,       0, 0,
            rW - rW * s,       gW - gW * s,       bW + (1 - bW) * s, 0, 0,
            0,                 0,                 0,                 1, 0
        };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return $"Adjusted saturation by {saturation}";
    }

    [McpServerTool(Name = "adjust_hue")]
    [Description("Rotates the hue of all colors in the working copy.")]
    public string AdjustHue(
        [Description("Hue rotation in degrees (0-360).")]
        float hue)
    {
        // Use a color matrix for hue rotation
        var h = hue * Math.PI / 180.0;
        var cosH = Math.Cos(h);
        var sinH = Math.Sin(h);
        var rw = 0.213;
        var gw = 0.715;
        var bw = 0.072;

        var matrix = new float[]
        {
            (float)(rw + (1 - rw) * cosH - (1 - rw) * sinH), (float)(gw + (1 - gw) * cosH - (1 - gw) * sinH * -1), 0, 0f, 0f,
            0, 0, 0, 0f, 0f,
            0, 0, 0, 0f, 0f,
            0, 0, 0, 1f, 0f
        };

        // Simplified: just shift hue using a simpler matrix approach
        // For a proper hue rotation we'd need a full RGB->YUV->rotate->RGB pipeline
        // Using a simpler approximation here
        matrix = new float[]
        {
            (float)(cosH + bw + rw * (1 - cosH) - gw * sinH), (float)(bw + gw * (1 - cosH) + rw * sinH), 0, 0, 0,
            (float)(gw * (1 - cosH) + bw * sinH), (float)(cosH + rw + gw * (1 - cosH) - bw * sinH), 0, 0, 0,
            (float)(bw * (1 - cosH) - gw * sinH), (float)(gw * sinH + bw * (1 - cosH)), 0, 0, 0,
            0, 0, 0, 1, 0
        };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return $"Adjusted hue by {hue} degrees";
    }

    [McpServerTool(Name = "adjust_gamma")]
    [Description("Applies a gamma correction to the working copy.")]
    public string AdjustGamma(
        [Description("Gamma value (e.g. 2.2, 1.0, 0.5). Values > 1.0 darken midtones, < 1.0 brighten.")]
        float gamma)
    {
        if (gamma <= 0)
            throw new ArgumentException("Gamma must be positive.");

        var invGamma = 1.0f / gamma;
        var table = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            table[i] = (byte)Math.Clamp((int)(255 * Math.Pow(i / 255.0, invGamma)), 0, 255);
        }

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var color = bmp.GetPixel(x, y);
                    newBitmap.SetPixel(x, y, new SKColor(
                        table[color.Red],
                        table[color.Green],
                        table[color.Blue],
                        color.Alpha));
                }
            }
            return newBitmap;
        });

        return $"Applied gamma correction (gamma={gamma})";
    }

    [McpServerTool(Name = "convert_to_grayscale")]
    [Description("Converts the working copy to grayscale using standard luma weights.")]
    public string ConvertToGrayscale()
    {
        var rW = 0.2126f;
        var gW = 0.7152f;
        var bW = 0.0722f;

        var matrix = new float[]
        {
            rW, gW, bW, 0, 0,
            rW, gW, bW, 0, 0,
            rW, gW, bW, 0, 0,
            0, 0, 0, 1, 0
        };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return "Converted to grayscale";
    }

    [McpServerTool(Name = "invert_colors")]
    [Description("Inverts all colors in the working copy (photographic negative). The alpha channel is preserved.")]
    public string InvertColors()
    {
        var matrix = new float[]
        {
            -1,  0,  0, 0, 1,
             0, -1,  0, 0, 1,
             0,  0, -1, 0, 1,
             0,  0,  0, 1, 0
        };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return "Inverted colors";
    }

    [McpServerTool(Name = "apply_sepia")]
    [Description("Applies a sepia tone effect to the working copy.")]
    public string ApplySepia()
    {
        var matrix = new float[]
        {
            0.393f, 0.769f, 0.189f, 0, 0,
            0.349f, 0.689f, 0.168f, 0, 0,
            0.272f, 0.534f, 0.131f, 0, 0,
            0,      0,      0,      1, 0
        };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return "Applied sepia tone";
    }

    [McpServerTool(Name = "apply_vintage")]
    [Description("Applies a vintage photo filter to the working copy with warm tone shift.")]
    public string ApplyVintage()
    {
        var matrix = new float[]
        {
            0.45f, 0.35f, 0.20f, 0, 0,
            0.30f, 0.60f, 0.10f, 0, 0,
            0.20f, 0.35f, 0.45f, 0, 0,
            0,      0,      0,     1, 0
        };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return "Applied vintage filter";
    }

    [McpServerTool(Name = "apply_color_matrix")]
    [Description("Applies a custom 4x5 color matrix to the working copy. The matrix is provided as 20 comma-separated floats in row-major order: rows are R, G, B, A; each row is [coeffR, coeffG, coeffB, coeffA, offset].")]
    public string ApplyColorMatrix(
        [Description("Matrix values as a comma-separated string of 20 floats.")]
        string matrixValues)
    {
        var values = matrixValues.Split(',').Select(float.Parse).ToArray();
        if (values.Length != 20)
            throw new ArgumentException("Color matrix must have 20 values (4 rows x 5 columns).");

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(values);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return "Applied custom color matrix";
    }

    [McpServerTool(Name = "apply_threshold")]
    [Description("Applies a binary threshold to the working copy. Pixels with luma below the threshold become black, above become white.")]
    public string ApplyThreshold(
        [Description("Threshold value (0-255). Default: 128.")]
        int threshold = 128)
    {
        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var color = bmp.GetPixel(x, y);
                    var luma = (int)(0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue);
                    var value = (byte)(luma >= threshold ? 255 : 0);
                    newBitmap.SetPixel(x, y, new SKColor(value, value, value, color.Alpha));
                }
            }
            return newBitmap;
        });

        return $"Applied threshold (threshold={threshold})";
    }

    [McpServerTool(Name = "replace_color")]
    [Description("Replaces all pixels matching a target color (within tolerance) with a replacement color.")]
    public string ReplaceColor(
        [Description("Target color to replace as hex (e.g. '#FF0000' for red).")]
        string targetColor,
        [Description("Replacement color as hex (e.g. '#00FF00' for green).")]
        string replacementColor,
        [Description("Tolerance for color matching (0-255). Default: 0 (exact match).")]
        int tolerance = 0)
    {
        var target = ParseColor(targetColor, SKColors.White);
        var replacement = ParseColor(replacementColor, SKColors.Black);
        var tol = (byte)Math.Clamp(tolerance, 0, 255);

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var color = bmp.GetPixel(x, y);
                    if (ColorDistance(color, target) <= tol)
                    {
                        newBitmap.SetPixel(x, y, replacement);
                    }
                    else
                    {
                        newBitmap.SetPixel(x, y, color);
                    }
                }
            }
            return newBitmap;
        });

        return $"Replaced color {targetColor} with {replacementColor} (tolerance={tolerance})";
    }

    #endregion

    #region Blur & Filter Operations

    [McpServerTool(Name = "apply_blur_gaussian")]
    [Description("Applies a Gaussian blur to the working copy.")]
    public string ApplyGaussianBlur(
        [Description("Blur radius (sigma) in pixels. Higher values produce more blur.")]
        float sigma = 5)
    {
        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ImageFilter = SKImageFilter.CreateBlur(sigma, sigma);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return $"Applied Gaussian blur (sigma={sigma})";
    }

    [McpServerTool(Name = "apply_blur_box")]
    [Description("Applies a box blur to the working copy.")]
    public string ApplyBoxBlur(
        [Description("Blur radius (sigma) in pixels.")]
        float sigma = 5)
    {
        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return $"Applied box blur (sigma={sigma})";
    }

    [McpServerTool(Name = "apply_sharpen")]
    [Description("Applies a sharpening filter to the working copy using a 3x3 convolution kernel.")]
    public string ApplySharpen()
    {
        var kernel = new float[] { 0, -1, 0, -1, 5, -1, 0, -1, 0 };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
                new SKSizeI(3, 3), kernel, 1f, 0f, SKPointI.Empty, SKShaderTileMode.Clamp, true, null);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return "Applied sharpening";
    }

    [McpServerTool(Name = "apply_emboss")]
    [Description("Applies an emboss effect to the working copy.")]
    public string ApplyEmboss()
    {
        var kernel = new float[] { -1, 0, 0, 0, 1, 0, 0, 0, 0 };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
                new SKSizeI(3, 3), kernel, 1f, 0f, SKPointI.Empty, SKShaderTileMode.Clamp, true, null);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return "Applied emboss effect";
    }

    [McpServerTool(Name = "apply_edge_detection")]
    [Description("Applies edge detection to the working copy using a Sobel-like horizontal kernel.")]
    public string ApplyEdgeDetection()
    {
        var kernel = new float[] { -1, 0, 1, -2, 0, 2, -1, 0, 1 };

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            using var canvas = new SKCanvas(newBitmap);
            using var paint = new SKPaint();
            paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
                new SKSizeI(3, 3), kernel, 1f, 0f, SKPointI.Empty, SKShaderTileMode.Clamp, true, null);
            canvas.DrawBitmap(bmp, 0, 0, paint);
            return newBitmap;
        });

        return "Applied edge detection";
    }

    [McpServerTool(Name = "apply_pixelate")]
    [Description("Applies a pixelation effect to the working copy by dividing it into blocks and averaging colors.")]
    public string ApplyPixelate(
        [Description("Size of each pixelated block in pixels. Default: 10.")]
        int blockSize = 10)
    {
        if (blockSize < 1) blockSize = 1;

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            for (int y = 0; y < bmp.Height; y += blockSize)
            {
                for (int x = 0; x < bmp.Width; x += blockSize)
                {
                    var blockW = Math.Min(blockSize, bmp.Width - x);
                    var blockH = Math.Min(blockSize, bmp.Height - y);

                    // Compute average color of the block
                    var avgR = 0; var avgG = 0; var avgB = 0; var avgA = 0;
                    var count = 0;
                    for (int dy = 0; dy < blockH; dy++)
                    {
                        for (int dx = 0; dx < blockW; dx++)
                        {
                            var color = bmp.GetPixel(x + dx, y + dy);
                            avgR += color.Red;
                            avgG += color.Green;
                            avgB += color.Blue;
                            avgA += color.Alpha;
                            count++;
                        }
                    }

                    var blockColor = new SKColor(
                        (byte)(avgR / count),
                        (byte)(avgG / count),
                        (byte)(avgB / count),
                        (byte)(avgA / count));

                    for (int dy = 0; dy < blockH; dy++)
                    {
                        for (int dx = 0; dx < blockW; dx++)
                        {
                            newBitmap.SetPixel(x + dx, y + dy, blockColor);
                        }
                    }
                }
            }
            return newBitmap;
        });

        return $"Applied pixelation (block size={blockSize})";
    }

    [McpServerTool(Name = "apply_noise")]
    [Description("Adds random noise to the working copy.")]
    public string ApplyNoise(
        [Description("Noise intensity (0.0-1.0). Default: 0.1.")]
        float intensity = 0.1f,
        [Description("Random seed for reproducibility. Default: 0 (random).")]
        int seed = 0)
    {
        var random = seed == 0 ? new Random() : new Random(seed);
        var intensityByte = (int)(intensity * 255);

        _dataManager.ReplaceBitmap(bmp =>
        {
            var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var color = bmp.GetPixel(x, y);
                    var noise = random.Next(-intensityByte, intensityByte + 1);
                    newBitmap.SetPixel(x, y, new SKColor(
                        (byte)Math.Clamp(color.Red + noise, 0, 255),
                        (byte)Math.Clamp(color.Green + noise, 0, 255),
                        (byte)Math.Clamp(color.Blue + noise, 0, 255),
                        color.Alpha));
                }
            }
            return newBitmap;
        });

        return $"Applied noise (intensity={intensity}, seed={seed})";
    }

    #endregion

    #region Drawing Tools

    [McpServerTool(Name = "draw_rectangle")]
    [Description("Draws a rectangle on the working copy.")]
    public string DrawRectangle(
        [Description("X coordinate of the top-left corner.")]
        int x,
        [Description("Y coordinate of the top-left corner.")]
        int y,
        [Description("Width of the rectangle.")]
        int width,
        [Description("Height of the rectangle.")]
        int height,
        [Description("Fill color as hex (e.g. '#FF0000'). Default: black.")]
        string? fillColor = "#000000",
        [Description("Stroke color as hex. If null, no stroke is drawn. Default: none.")]
        string? strokeColor = null,
        [Description("Stroke width in pixels. Default: 0.")]
        float strokeWidth = 0,
        [Description("Fill style: fill, stroke, or stroke_and_fill. Default: fill.")]
        string? style = "fill")
    {
        var fill = ParseColor(fillColor, SKColors.Black);
        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = fill;
            paint.Style = style?.ToLowerInvariant() switch
            {
                "stroke" => SKPaintStyle.Stroke,
                "stroke_and_fill" => SKPaintStyle.StrokeAndFill,
                _ => SKPaintStyle.Fill
            };

            if (strokeColor is not null)
            {
                paint.Color = ParseColor(strokeColor, SKColors.Black);
                paint.StrokeWidth = strokeWidth;
                paint.Style = SKPaintStyle.StrokeAndFill;
            }

            canvas.DrawRect(x, y, width, height, paint);
        });

        return $"Drew rectangle at ({x},{y}) size {width}x{height}";
    }

    [McpServerTool(Name = "draw_circle")]
    [Description("Draws a circle on the working copy.")]
    public string DrawCircle(
        [Description("X coordinate of the center.")]
        int centerX,
        [Description("Y coordinate of the center.")]
        int centerY,
        [Description("Radius in pixels.")]
        int radius,
        [Description("Fill color as hex (e.g. '#FF0000'). Default: black.")]
        string? fillColor = "#000000",
        [Description("Stroke color as hex. If null, no stroke is drawn. Default: none.")]
        string? strokeColor = null,
        [Description("Stroke width in pixels. Default: 0.")]
        float strokeWidth = 0,
        [Description("Fill style: fill, stroke, or stroke_and_fill. Default: fill.")]
        string? style = "fill")
    {
        var fill = ParseColor(fillColor, SKColors.Black);
        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = fill;
            paint.Style = style?.ToLowerInvariant() switch
            {
                "stroke" => SKPaintStyle.Stroke,
                "stroke_and_fill" => SKPaintStyle.StrokeAndFill,
                _ => SKPaintStyle.Fill
            };

            if (strokeColor is not null)
            {
                paint.Color = ParseColor(strokeColor, SKColors.Black);
                paint.StrokeWidth = strokeWidth;
                paint.Style = SKPaintStyle.StrokeAndFill;
            }

            canvas.DrawCircle(centerX, centerY, radius, paint);
        });

        return $"Drew circle at ({centerX},{centerY}) radius {radius}";
    }

    [McpServerTool(Name = "draw_line")]
    [Description("Draws a line on the working copy.")]
    public string DrawLine(
        [Description("X coordinate of the start point.")]
        int startX,
        [Description("Y coordinate of the start point.")]
        int startY,
        [Description("X coordinate of the end point.")]
        int endX,
        [Description("Y coordinate of the end point.")]
        int endY,
        [Description("Stroke color as hex. Default: black.")]
        string? strokeColor = "#000000",
        [Description("Stroke width in pixels. Default: 2.")]
        float strokeWidth = 2)
    {
        var color = ParseColor(strokeColor, SKColors.Black);
        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = color;
            paint.StrokeWidth = strokeWidth;
            paint.Style = SKPaintStyle.Stroke;
            canvas.DrawLine(startX, startY, endX, endY, paint);
        });

        return $"Drew line from ({startX},{startY}) to ({endX},{endY})";
    }

    [McpServerTool(Name = "draw_text")]
    [Description("Draws text on the working copy using the specified font and paint settings.")]
    public string DrawText(
        [Description("The text string to draw.")]
        string text,
        [Description("X coordinate of the text position.")]
        int x,
        [Description("Y coordinate of the text baseline.")]
        int y,
        [Description("Font family name. Default: sans-serif.")]
        string fontFamily = "sans-serif",
        [Description("Font size in pixels. Default: 12.")]
        float fontSize = 12,
        [Description("Text color as hex. Default: black.")]
        string? textColor = "#000000",
        [Description("Text alignment: left, center, right. Default: left.")]
        string textAlign = "left")
    {
        var color = ParseColor(textColor, SKColors.Black);
        var align = textAlign.ToLowerInvariant() switch
        {
            "center" => SKTextAlign.Center,
            "right" => SKTextAlign.Right,
            _ => SKTextAlign.Left
        };

        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Color = color;
            using var font = new SKFont();
            font.Typeface = SKTypeface.FromFamilyName(fontFamily);
            font.Size = fontSize;
            canvas.DrawText(text, x, y, align, font, paint);
        });

        return $"Drew text '{text}' at ({x},{y}) with font '{fontFamily}' size {fontSize}";
    }

    [McpServerTool(Name = "set_pixel")]
    [Description("Sets the color of a single pixel at the specified coordinates.")]
    public string SetPixel(
        [Description("X coordinate of the pixel.")]
        int x,
        [Description("Y coordinate of the pixel.")]
        int y,
        [Description("Color as hex (e.g. '#FF0000' for red).")]
        string color)
    {
        var pixelColor = ParseColor(color, SKColors.Black);
        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.Color = pixelColor;
            paint.BlendMode = SKBlendMode.Src;
            canvas.DrawRect(x, y, 1, 1, paint);
        });

        return $"Set pixel at ({x},{y}) to {color}";
    }

    [McpServerTool(Name = "fill_region")]
    [Description("Fills a rectangular region with a solid color using the specified blend mode.")]
    public string FillRegion(
        [Description("X coordinate of the top-left corner of the region.")]
        int x,
        [Description("Y coordinate of the top-left corner of the region.")]
        int y,
        [Description("Width of the region to fill.")]
        int width,
        [Description("Height of the region to fill.")]
        int height,
        [Description("Fill color as hex (e.g. '#FF0000' for red, '#00000000' for transparent).")]
        string color,
        [Description("Blend mode: src, src_over, multiply, screen, overlay, etc. Default: src_over.")]
        string blendMode = "src_over")
    {
        var fillColor = ParseColor(color, SKColors.Black);
        var blend = ParseBlendMode(blendMode);
        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.Color = fillColor;
            paint.BlendMode = blend;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(x, y, width, height, paint);
        });

        return $"Filled region ({x},{y},{width},{height}) with {color} (blend: {blendMode})";
    }

    [McpServerTool(Name = "clear_region")]
    [Description("Clears a rectangular region of the working copy, filling it with a color (transparent by default).")]
    public string ClearRegion(
        [Description("X coordinate of the top-left corner of the region to clear.")]
        int x,
        [Description("Y coordinate of the top-left corner of the region to clear.")]
        int y,
        [Description("Width of the region to clear.")]
        int width,
        [Description("Height of the region to clear.")]
        int height,
        [Description("Fill color for cleared area as hex. Default: transparent.")]
        string? color = "00000000")
    {
        var fillColor = ParseColor(color, SKColors.Transparent);
        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.Color = fillColor;
            paint.BlendMode = SKBlendMode.Src;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(x, y, width, height, paint);
        });

        return $"Cleared region ({x},{y},{width},{height})";
    }

    [McpServerTool(Name = "overlay_image")]
    [Description("Draws another image onto the working copy at the specified position, with optional scaling and blend mode.")]
    public string OverlayImage(
        [Description("Path to the image to overlay.")]
        string overlayPath,
        [Description("X position to draw the overlay. Default: 0.")]
        int x = 0,
        [Description("Y position to draw the overlay. Default: 0.")]
        int y = 0,
        [Description("Scale factor for the overlay. Default: 1.0 (original size).")]
        float scale = 1.0f,
        [Description("Blend mode: src_over, multiply, screen, overlay, etc. Default: src_over.")]
        string blendMode = "src_over",
        [Description("Opacity 0.0-1.0. Default: 1.0 (fully opaque).")]
        float opacity = 1.0f)
    {
        if (!File.Exists(overlayPath))
            throw new FileNotFoundException($"Overlay image not found: {overlayPath}");

        using var overlay = SKBitmap.Decode(overlayPath);
        var blend = ParseBlendMode(blendMode);
        var destW = (int)(overlay.Width * scale);
        var destH = (int)(overlay.Height * scale);

        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.BlendMode = blend;
            var alpha = (byte)(opacity * 255);
            paint.Color = new SKColor(paint.Color.Red, paint.Color.Green, paint.Color.Blue, alpha);
            canvas.DrawBitmap(overlay, new SKRect(x, y, x + destW, y + destH), paint);
        });

        return $"Overlayed '{overlayPath}' at ({x},{y}) scale={scale} blend={blendMode} opacity={opacity}";
    }

    #endregion

    #region Utility

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

    #endregion

    #region Helpers

    private static SKColor ParseColor(string? hex, SKColor fallback)
    {
        if (hex is null) return fallback;

        hex = hex.TrimStart('#');

        if (hex.Length == 3)
        {
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        }

        if (hex.Length == 6)
        {
            if (uint.TryParse(hex, out var argb))
            {
                var r = (byte)((argb >> 16) & 0xFF);
                var g = (byte)((argb >> 8) & 0xFF);
                var b = (byte)(argb & 0xFF);
                return new SKColor(r, g, b, 255);
            }
        }
        else if (hex.Length == 8)
        {
            if (uint.TryParse(hex, out var argb))
            {
                var a = (byte)((argb >> 24) & 0xFF);
                var r = (byte)((argb >> 16) & 0xFF);
                var g = (byte)((argb >> 8) & 0xFF);
                var b = (byte)(argb & 0xFF);
                return new SKColor(r, g, b, a);
            }
        }

        var sc = hex.ToLowerInvariant();
        return sc switch
        {
            "black" => SKColors.Black,
            "white" => SKColors.White,
            "red" => SKColors.Red,
            "green" => SKColors.Green,
            "blue" => SKColors.Blue,
            "yellow" => SKColors.Yellow,
            "cyan" => SKColors.Cyan,
            "magenta" => SKColors.Magenta,
            "transparent" => SKColors.Transparent,
            "gray" or "grey" => SKColors.Gray,
            _ => fallback
        };
    }

    private static SKBlendMode ParseBlendMode(string mode)
    {
        var m = mode.ToLowerInvariant();
        return m switch
        {
            "src" => SKBlendMode.Src,
            "src_over" => SKBlendMode.SrcOver,
            "src_in" => SKBlendMode.SrcIn,
            "src_out" => SKBlendMode.SrcOut,
            "dst" => SKBlendMode.Dst,
            "dst_over" => SKBlendMode.DstOver,
            "dst_in" => SKBlendMode.DstIn,
            "dst_out" => SKBlendMode.DstOut,
            "multiply" => SKBlendMode.Multiply,
            "screen" => SKBlendMode.Screen,
            "overlay" => SKBlendMode.Overlay,
            "darken" => SKBlendMode.Darken,
            "lighten" => SKBlendMode.Lighten,
            "color_dodge" => SKBlendMode.ColorDodge,
            "color_burn" => SKBlendMode.ColorBurn,
            "hard_light" => SKBlendMode.HardLight,
            "soft_light" => SKBlendMode.SoftLight,
            "difference" => SKBlendMode.Difference,
            "exclusion" => SKBlendMode.Exclusion,
            "modulate" => SKBlendMode.Modulate,
            _ => SKBlendMode.SrcOver
        };
    }

    private static float ColorDistance(SKColor a, SKColor b)
    {
        var dr = (int)a.Red - (int)b.Red;
        var dg = (int)a.Green - (int)b.Green;
        var db = (int)a.Blue - (int)b.Blue;
        var da = (int)a.Alpha - (int)b.Alpha;
        return (float)Math.Sqrt(dr * dr + dg * dg + db * db + da * da);
    }

    #endregion
}
