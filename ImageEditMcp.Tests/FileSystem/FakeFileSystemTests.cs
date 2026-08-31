using global::ImageEditMcp.TestingUtilities.FileSystem;

namespace ImageEditMcp.Tests.FileSystem;

public class FakeFileSystemTests
{
    [Fact]
    public async Task AddFile_MakesFileReadableWithContent()
    {
        var fs = new FakeFileSystem();
        fs.AddFile("/data/hello.txt", "hello world");

        Assert.True(fs.FileExists("/data/hello.txt"));
        Assert.Equal("hello world", await fs.ReadAllTextAsync("/data/hello.txt", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void FileExists_WhenFileMissing_ReturnsFalse()
    {
        var fs = new FakeFileSystem();
        Assert.False(fs.FileExists("/nope.txt"));
    }

    [Fact]
    public void CreateDirectory_AddsHierarchy()
    {
        var fs = new FakeFileSystem();
        fs.CreateDirectory("/a/b/c");

        Assert.True(fs.DirectoryExists("/a"));
        Assert.True(fs.DirectoryExists("/a/b"));
        Assert.True(fs.DirectoryExists("/a/b/c"));
    }

    [Fact]
    public async Task OpenFileStream_Create_WritesContentOnDispose()
    {
        var fs = new FakeFileSystem();
        using (var stream = fs.OpenFileStream("/out/file.bin", FileMode.Create, FileAccess.Write))
        {
            stream.Write([1, 2, 3]);
        }

        Assert.Equal([1, 2, 3], await fs.ReadAllBytesAsync("/out/file.bin", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void OpenFileStream_WhenFileMissingAndOpen_Throws()
    {
        var fs = new FakeFileSystem();
        Assert.Throws<FileNotFoundException>(() => fs.OpenFileStream("/missing.bin", FileMode.Open, FileAccess.Read));
    }

    [Fact]
    public void CopyFile_WithoutOverwrite_ThrowsOnExistingDestination()
    {
        var fs = new FakeFileSystem();
        fs.AddFile("/a.txt", "one");
        fs.AddFile("/b.txt", "two");

        Assert.Throws<IOException>(() => fs.CopyFile("/a.txt", "/b.txt", overwrite: false));
    }

    [Fact]
    public void DeleteFile_RemovesFile()
    {
        var fs = new FakeFileSystem();
        fs.AddFile("/gone.txt", "x");
        fs.DeleteFile("/gone.txt");
        Assert.False(fs.FileExists("/gone.txt"));
    }

    [Fact]
    public void GetFiles_WithPattern_FiltersByName()
    {
        var fs = new FakeFileSystem();
        fs.AddFile("/dir/a.jpg", "1");
        fs.AddFile("/dir/b.png", "2");
        fs.AddFile("/dir/sub/c.jpg", "3");

        Assert.Equal(["/dir/a.jpg"], fs.GetFiles("/dir", "*.jpg"));
        Assert.Equal(2, fs.GetFiles("/dir", "*.jpg", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task MoveFile_RelocatesContent()
    {
        var fs = new FakeFileSystem();
        fs.AddFile("/src.txt", "payload");
        fs.MoveFile("/src.txt", "/dst.txt", overwrite: false);

        Assert.False(fs.FileExists("/src.txt"));
        Assert.Equal("payload", await fs.ReadAllTextAsync("/dst.txt", TestContext.Current.CancellationToken));
    }
}
