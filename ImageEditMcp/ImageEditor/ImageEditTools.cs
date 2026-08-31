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
public sealed partial class ImageEditTools
{
    private readonly ImageDataManager _dataManager;

    public ImageEditTools(ImageDataManager dataManager)
    {
        _dataManager = dataManager;
    }

    private static float ColorDistance(SKColor a, SKColor b)
    {
        var dr = (int)a.Red - (int)b.Red;
        var dg = (int)a.Green - (int)b.Green;
        var db = (int)a.Blue - (int)b.Blue;
        var da = (int)a.Alpha - (int)b.Alpha;
        return (float)Math.Sqrt(dr * dr + dg * dg + db * db + da * da);
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
}
