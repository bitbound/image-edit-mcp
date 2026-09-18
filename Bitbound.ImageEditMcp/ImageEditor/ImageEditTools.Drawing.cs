using System.ComponentModel;
using ModelContextProtocol.Server;
using SkiaSharp;

namespace Bitbound.ImageEditMcp.ImageEditor;

public sealed partial class ImageEditTools
{
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
        if (!_dataManager.FileSystem.FileExists(overlayPath))
            throw new FileNotFoundException($"Overlay image not found: {overlayPath}");

        using var overlayStream = _dataManager.FileSystem.OpenFileStream(overlayPath, FileMode.Open, FileAccess.Read);
        using var overlay = SKBitmap.Decode(overlayStream);
        var blend = ParseBlendMode(blendMode);
        var destW = (int)(overlay.Width * scale);
        var destH = (int)(overlay.Height * scale);

        _dataManager.ApplyEdit(canvas =>
        {
            using var paint = new SKPaint();
            paint.BlendMode = blend;
            var alpha = (byte)(opacity * 255);
            paint.Color = new SKColor(paint.Color.Red, paint.Color.Green, paint.Color.Blue, alpha);
            canvas.DrawBitmap(overlay, new SKRect(x, y, x + destW, y + destH), SKSamplingOptions.Default, paint);
        });

        return $"Overlayed '{overlayPath}' at ({x},{y}) scale={scale} blend={blendMode} opacity={opacity}";
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
}
