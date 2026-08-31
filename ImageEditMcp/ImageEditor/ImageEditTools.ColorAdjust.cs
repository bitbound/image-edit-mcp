using System.ComponentModel;
using ModelContextProtocol.Server;
using SkiaSharp;

namespace ImageEditMcp;

public sealed partial class ImageEditTools
{
    [McpServerTool(Name = "adjust_brightness")]
    [Description("Adjusts the brightness of the working copy.")]
    public string AdjustBrightness(
        [Description("Brightness adjustment (-1.0 to 1.0). Positive increases brightness, negative decreases.")]
        float brightness)
    {
        ApplyBrightnessContrast(brightness, 0);
        return $"Adjusted brightness by {brightness}";
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

    [McpServerTool(Name = "adjust_contrast")]
    [Description("Adjusts the contrast of the working copy.")]
    public string AdjustContrast(
        [Description("Contrast adjustment (-1.0 to 1.0). Positive increases contrast, negative decreases.")]
        float contrast)
    {
        ApplyBrightnessContrast(0, contrast);
        return $"Adjusted contrast by {contrast}";
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
}
