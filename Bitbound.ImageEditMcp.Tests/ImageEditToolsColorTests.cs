using Bitbound.ImageEditMcp.ImageEditor;
using Bitbound.SystemAbstractions.TestUtilities.FileSystem;
using SkiaSharp;

namespace Bitbound.ImageEditMcp.Tests;

/// <summary>
/// Tests for color parsing across the drawing tools. The tools are constructed directly
/// against an <see cref="ImageDataManager"/> backed by a <see cref="FakeFileSystem"/>, so the
/// hex and named-color contract is pinned without touching real disk.
/// </summary>
public sealed class ImageEditToolsColorTests : IDisposable
{
  private static readonly string Root = Environment.CurrentDirectory.Replace('\\', '/');
  private static readonly string DataDirectory = $"{Root}/data/image-edit-mcp-tools";

  private readonly FakeFileSystem _fileSystem;
  private readonly ImageDataManager _dataManager;
  private readonly ImageEditTools _sut;

  public ImageEditToolsColorTests()
  {
    _fileSystem = new FakeFileSystem('/');
    _fileSystem.AddDirectory(DataDirectory);
    _dataManager = new ImageDataManager(_fileSystem, DataDirectory);
    _sut = new ImageEditTools(_dataManager);
    _dataManager.CreateImage(8, 8, SKColors.Transparent);
  }

  public void Dispose()
  {
    _dataManager.Dispose();
  }

  private SKColor PixelAt(int x, int y)
  {
    using var clone = _dataManager.CloneCurrentBitmap();
    return clone.GetPixel(x, y);
  }

  private SKColor PixelInSavedFile(int x, int y)
  {
    var outputPath = $"{Root}/output/painted.png";
    _sut.SaveImage(outputPath);

    using var stream = _fileSystem.OpenFileStream(outputPath, FileMode.Open, FileAccess.Read);
    using var bitmap = SKBitmap.Decode(stream);
    return bitmap.GetPixel(x, y);
  }

  [Theory]
  [InlineData("#FF0000", 255, 0, 0, 255)]
  [InlineData("FF0000", 255, 0, 0, 255)]
  [InlineData("#00FF00", 0, 255, 0, 255)]
  [InlineData("#0000FF", 0, 0, 255, 255)]
  [InlineData("#123456", 18, 52, 86, 255)]
  [InlineData("#abcdef", 171, 205, 239, 255)]
  [InlineData("#F00", 255, 0, 0, 255)]
  [InlineData("#0F0", 0, 255, 0, 255)]
  public void FillRegion_WithHexColor_PaintsThatColor(string color, byte r, byte g, byte b, byte a)
  {
    _sut.FillRegion(0, 0, 8, 8, color, "src");

    Assert.Equal(new SKColor(r, g, b, a), PixelAt(4, 4));
  }

  [Theory]
  [InlineData("#80FF0000", 255, 0, 0, 128)]
  [InlineData("FFFF0000", 255, 0, 0, 255)]
  [InlineData("00FF0000", 0, 0, 0, 0)]
  public void FillRegion_WithEightDigitHexColor_PaintsThatColorWithAlpha(string color, byte r, byte g, byte b, byte a)
  {
    _sut.FillRegion(0, 0, 8, 8, color, "src");

    var pixel = PixelAt(4, 4);
    Assert.Equal(r, pixel.Red);
    Assert.Equal(a, pixel.Alpha);
    Assert.Equal(g, pixel.Green);
    Assert.Equal(b, pixel.Blue);
  }

  [Theory]
  [InlineData("red", 255, 0, 0)]
  [InlineData("blue", 0, 0, 255)]
  [InlineData("green", 0, 128, 0)]
  [InlineData("white", 255, 255, 255)]
  [InlineData("gray", 128, 128, 128)]
  [InlineData("RED", 255, 0, 0)]
  public void FillRegion_WithNamedColor_PaintsThatColor(string color, byte r, byte g, byte b)
  {
    _sut.FillRegion(0, 0, 8, 8, color, "src");

    var pixel = PixelAt(4, 4);
    Assert.Equal(255, pixel.Alpha);
    Assert.Equal(r, pixel.Red);
    Assert.Equal(g, pixel.Green);
    Assert.Equal(b, pixel.Blue);
  }

  [Fact]
  public void FillRegion_WithUnparsableColor_PaintsBlackFallback()
  {
    _sut.FillRegion(0, 0, 8, 8, "not-a-color", "src");

    Assert.Equal(new SKColor(0, 0, 0, 255), PixelAt(4, 4));
  }

  [Fact]
  public void DrawRectangle_WithHexFillColor_PaintsHexColor()
  {
    _sut.DrawRectangle(2, 2, 4, 4, "#FF0000");

    Assert.Equal(new SKColor(255, 0, 0, 255), PixelAt(3, 3));
  }

  [Fact]
  public void DrawCircle_WithHexFillColor_PaintsHexColor()
  {
    _sut.DrawCircle(4, 4, 3, "#00FF00");

    Assert.Equal(new SKColor(0, 255, 0, 255), PixelAt(4, 4));
  }

  [Fact]
  public void DrawCircle_WithHexStrokeColor_PaintsHexStroke()
  {
    _sut.DrawCircle(4, 4, 3, "#00000000", "#0000FF", 2, "stroke");

    Assert.Equal(255, PixelAt(1, 4).Blue);
  }

  [Fact]
  public void CreateImage_WithHexColor_FillsWithHexColor()
  {
    _sut.CreateImage(4, 4, "#FF8800");

    Assert.Equal(new SKColor(255, 136, 0, 255), PixelAt(2, 2));
  }

  [Fact]
  public void ClearRegion_WithDefaultColor_ClearsToTransparent()
  {
    _sut.FillRegion(0, 0, 8, 8, "#FF0000", "src");

    _sut.ClearRegion(0, 0, 8, 8);

    Assert.Equal(new SKColor(0, 0, 0, 0), PixelAt(4, 4));
  }

  [Fact]
  public void SaveImage_AfterHexFill_WritesPngCarryingThatColor()
  {
    _sut.FillRegion(0, 0, 8, 8, "#FF0000", "src");

    Assert.Equal(new SKColor(255, 0, 0, 255), PixelInSavedFile(4, 4));
  }
}
