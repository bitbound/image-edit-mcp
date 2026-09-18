
namespace Bitbound.ImageEditMcp.ImageEditor;

public sealed partial class ImageEditTools
{
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
      canvas.DrawBitmap(bmp, 0, 0, SKSamplingOptions.Default, paint);
      return newBitmap;
    });

    return $"Applied box blur (sigma={sigma})";
  }

  [McpServerTool(Name = "apply_edge_detection")]
  [Description("Applies edge detection to the working copy using a Sobel-like horizontal kernel.")]
  public string ApplyEdgeDetection()
  {
    float[] kernel = [-1, 0, 1, -2, 0, 2, -1, 0, 1];

    _dataManager.ReplaceBitmap(bmp =>
    {
      var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
      using var canvas = new SKCanvas(newBitmap);
      using var paint = new SKPaint();
      paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
              new SKSizeI(3, 3), kernel, 1f, 0f, SKPointI.Empty, SKShaderTileMode.Clamp, true, null);
      canvas.DrawBitmap(bmp, 0, 0, SKSamplingOptions.Default, paint);
      return newBitmap;
    });

    return "Applied edge detection";
  }

  [McpServerTool(Name = "apply_emboss")]
  [Description("Applies an emboss effect to the working copy.")]
  public string ApplyEmboss()
  {
    float[] kernel = [-1, 0, 0, 0, 1, 0, 0, 0, 0];

    _dataManager.ReplaceBitmap(bmp =>
    {
      var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
      using var canvas = new SKCanvas(newBitmap);
      using var paint = new SKPaint();
      paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
              new SKSizeI(3, 3), kernel, 1f, 0f, SKPointI.Empty, SKShaderTileMode.Clamp, true, null);
      canvas.DrawBitmap(bmp, 0, 0, SKSamplingOptions.Default, paint);
      return newBitmap;
    });

    return "Applied emboss effect";
  }

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
      canvas.DrawBitmap(bmp, 0, 0, SKSamplingOptions.Default, paint);
      return newBitmap;
    });

    return $"Applied Gaussian blur (sigma={sigma})";
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

  [McpServerTool(Name = "apply_sharpen")]
  [Description("Applies a sharpening filter to the working copy using a 3x3 convolution kernel.")]
  public string ApplySharpen()
  {
    float[] kernel = [0, -1, 0, -1, 5, -1, 0, -1, 0];

    _dataManager.ReplaceBitmap(bmp =>
    {
      var newBitmap = new SKBitmap(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
      using var canvas = new SKCanvas(newBitmap);
      using var paint = new SKPaint();
      paint.ImageFilter = SKImageFilter.CreateMatrixConvolution(
              new SKSizeI(3, 3), kernel, 1f, 0f, SKPointI.Empty, SKShaderTileMode.Clamp, true, null);
      canvas.DrawBitmap(bmp, 0, 0, SKSamplingOptions.Default, paint);
      return newBitmap;
    });

    return "Applied sharpening";
  }
}
