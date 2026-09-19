namespace Bitbound.ImageEditMcp.ImageEditor;

/// <summary>
/// MCP tools that expose SkiaSharp image editing APIs.
/// Each tool operates on a working copy loaded by <c>load_image</c>.
/// </summary>
[McpServerToolType]
public sealed partial class ImageEditTools(ImageDataManager dataManager)
{
  private readonly ImageDataManager _dataManager = dataManager;

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

    var value = hex.TrimStart('#');

    var named = value.ToLowerInvariant() switch
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
      _ => (SKColor?)null
    };

    if (named is not null)
    {
      return named.Value;
    }

    if (value.Length == 3)
    {
      value = $"{value[0]}{value[0]}{value[1]}{value[1]}{value[2]}{value[2]}";
    }

    if (value.Length != 6 && value.Length != 8)
    {
      return fallback;
    }

    if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
    {
      return fallback;
    }

    var r = (byte)((argb >> 16) & 0xFF);
    var g = (byte)((argb >> 8) & 0xFF);
    var b = (byte)(argb & 0xFF);
    var a = value.Length == 8 ? (byte)((argb >> 24) & 0xFF) : (byte)255;
    return new SKColor(r, g, b, a);
  }
}
