using System.Text;
using Bitbound.ImageEditMcp.ImageEditor;
using Bitbound.SystemAbstractions.TestUtilities.FileSystem;
using SkiaSharp;

namespace Bitbound.ImageEditMcp.Tests;

/// <summary>
/// Tests for <see cref="ImageDataManager"/>. Use an in-memory <see cref="FakeFileSystem"/>
/// with forward-slash paths rooted under the test process's current directory so the
/// manager's <see cref="Path.GetFullPath(string)"/> calls round-trip cleanly.
/// </summary>
public sealed class ImageDataManagerTests : IDisposable
{
    private static readonly string Root = Environment.CurrentDirectory.Replace('\\', '/');
    private static readonly string DataDirectory = $"{Root}/data/image-edit-mcp";
    private static readonly string SourceDirectory = $"{Root}/sources";

    private readonly FakeFileSystem _fileSystem;
    private readonly ImageDataManager _sut;

    public ImageDataManagerTests()
    {
        _fileSystem = new FakeFileSystem('/');
        _fileSystem.AddDirectory(DataDirectory);
        _fileSystem.AddDirectory(SourceDirectory);
        _sut = new ImageDataManager(_fileSystem, DataDirectory);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    private static byte[] CreatePngBytes(int width = 4, int height = 4, byte r = 255, byte g = 0, byte b = 0)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(new SKColor(r, g, b, 255));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private string SeedSourceImage(string fileName, byte[]? content = null)
    {
        var path = $"{SourceDirectory}/{fileName}";
        _fileSystem.AddFile(path, content ?? CreatePngBytes());
        return path;
    }

    [Fact]
    public void Constructor_CreatesDataDirectory_WhenItDoesNotExist()
    {
        var freshFileSystem = new FakeFileSystem('/');
        var missing = $"{Root}/elsewhere/data";

        _ = new ImageDataManager(freshFileSystem, missing);

        Assert.True(freshFileSystem.DirectoryExists(missing));
    }

    [Fact]
    public void LoadImage_WithMissingFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => _sut.LoadImage($"{SourceDirectory}/missing.png"));
    }

    [Fact]
    public void LoadImage_WithValidPng_DecodesIntoWorkingCopy()
    {
        var sourcePath = SeedSourceImage("photo.png");

        var (width, height, format, _) = _sut.LoadImage(sourcePath);

        Assert.Equal(4, width);
        Assert.Equal(4, height);
        Assert.Equal("png", format);
        Assert.True(_sut.HasImage);
        Assert.False(_sut.GetImageInfo().Dirty);
        Assert.Equal(Path.GetFullPath(sourcePath), _sut.GetImageInfo().SourcePath);
    }

    [Fact]
    public void LoadImage_CreatesWorkingCopyInDataDirectory()
    {
        var sourcePath = SeedSourceImage("photo.png");

        _sut.LoadImage(sourcePath);

        var workingCopyPath = $"{DataDirectory}/photo.png";
        Assert.True(_fileSystem.FileExists(workingCopyPath));
    }

    [Fact]
    public void ApplyEdit_WhenNoImageLoaded_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.ApplyEdit(canvas => { }));
    }

    [Fact]
    public void ApplyEdit_MarksBitmapAsDirty()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));

        _sut.ApplyEdit(canvas => canvas.Clear(new SKColor(0, 128, 0, 255)));

        Assert.True(_sut.GetImageInfo().Dirty);
    }

    [Fact]
    public void ReplaceBitmap_MarksBitmapAsDirty()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));

        _sut.ReplaceBitmap(bitmap =>
        {
            var clone = bitmap.Copy();
            return clone;
        });

        Assert.True(_sut.GetImageInfo().Dirty);
    }

    [Fact]
    public void SaveImage_WhenNoImageLoaded_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.SaveImage($"{Root}/output/result.png"));
    }

    [Fact]
    public void SaveImage_WritesToOutputPath_AndClearsDirty()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));
        _sut.ApplyEdit(canvas => canvas.Clear(new SKColor(0, 0, 255, 255)));
        var outputPath = $"{Root}/output/result.png";
        var expectedPath = Path.GetFullPath(outputPath);

        var returned = _sut.SaveImage(outputPath, 90);

        Assert.Equal(expectedPath, returned);
        Assert.True(_fileSystem.FileExists(outputPath));
        Assert.False(_sut.GetImageInfo().Dirty);
    }

    [Fact]
    public void SaveImage_CreatesMissingParentDirectory()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));
        var outputPath = $"{Root}/nested/dirs/result.png";

        _sut.SaveImage(outputPath);

        Assert.True(_fileSystem.DirectoryExists($"{Root}/nested/dirs"));
    }

    [Fact]
    public void ReloadOriginal_WhenNoImageLoaded_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.ReloadOriginal());
    }

    [Fact]
    public void ReloadOriginal_RestoresWorkingCopyFromSource()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));
        _sut.ApplyEdit(canvas => canvas.Clear(new SKColor(0, 0, 0, 255)));

        _sut.ReloadOriginal();

        Assert.False(_sut.GetImageInfo().Dirty);
    }

    [Fact]
    public void SaveSnapshot_CreatesFileInSnapshotsSubdirectory()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));

        var returned = _sut.SaveSnapshot("checkpoint-1");

        Assert.EndsWith($"{Path.DirectorySeparatorChar}snapshots{Path.DirectorySeparatorChar}checkpoint-1.png", returned);
        Assert.True(_fileSystem.FileExists(returned));
        Assert.True(_fileSystem.FileExists($"{DataDirectory}/snapshots/checkpoint-1.png"));
    }

    [Fact]
    public void SaveSnapshot_WithEmptyName_UsesTimestamp()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));

        var returned = _sut.SaveSnapshot();

        Assert.EndsWith(".png", returned);
        Assert.True(_fileSystem.FileExists(returned));
    }

    [Fact]
    public void LoadSnapshot_WithKnownName_RestoresIntoWorkingCopy()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));
        _sut.SaveSnapshot("checkpoint-1");

        var message = _sut.LoadSnapshot("checkpoint-1");

        Assert.Contains("checkpoint-1", message);
        Assert.True(_sut.HasImage);
        Assert.False(_sut.GetImageInfo().Dirty);
    }

    [Fact]
    public void LoadSnapshot_WithoutExtension_FindsPngVariant()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));
        _sut.SaveSnapshot("checkpoint-2");

        var message = _sut.LoadSnapshot("checkpoint-2");

        Assert.Contains("checkpoint-2", message);
        Assert.True(_sut.HasImage);
    }

    [Fact]
    public void LoadSnapshot_WithUnknownName_ThrowsFileNotFoundException()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));

        Assert.Throws<FileNotFoundException>(() => _sut.LoadSnapshot("does-not-exist"));
    }

    [Fact]
    public void GetImageDataBase64_WhenNoImageLoaded_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.GetImageDataBase64());
    }

    [Fact]
    public void GetImageDataBase64_ReturnsDecodablePng()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));

        var base64 = _sut.GetImageDataBase64();
        var bytes = Convert.FromBase64String(base64);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.Equal(4, bitmap.Width);
        Assert.Equal(4, bitmap.Height);
    }

    [Fact]
    public void CloneCurrentBitmap_WhenNoImageLoaded_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.CloneCurrentBitmap());
    }

    [Fact]
    public void CloneCurrentBitmap_ReturnsIndependentCopy()
    {
        _sut.LoadImage(SeedSourceImage("photo.png"));

        using var clone = _sut.CloneCurrentBitmap();
        Assert.Equal(_sut.GetImageInfo().Width, clone.Width);
        Assert.Equal(_sut.GetImageInfo().Height, clone.Height);
    }

    [Fact]
    public void GetImageInfo_BeforeLoad_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.GetImageInfo());
    }
}

/// <summary>
/// Sanity check that the test project actually runs tests (xUnit v3 + Microsoft.Testing.Platform).
/// </summary>
public sealed class SanityTests
{
    [Fact]
    public void True_IsTrue()
    {
        Assert.True(true);
    }

    [Fact]
    public void StringBuilder_AppendsText()
    {
        var sb = new StringBuilder();
        sb.Append("hello").Append(' ').Append("world");
        Assert.Equal("hello world", sb.ToString());
    }
}