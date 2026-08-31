namespace ImageEditMcp.TestingUtilities.FileSystem;

internal sealed class FakeFileSystemFileInfo(string _normalizedPath, FakeFileSystem _fileSystem) : Abstractions.FileSystem.IFileSystemFile
{
    public FileAttributes Attributes => FileAttributes.Normal;

    public bool Exists => _fileSystem.GetFileContent(_normalizedPath) is not null;

    public string FullName => _normalizedPath;

    public DateTime LastWriteTime => _fileSystem.GetLastWriteTime(_normalizedPath);

    public long Length
    {
        get
        {
            var content = _fileSystem.GetFileContent(_normalizedPath);
            return content?.LongLength ?? 0;
        }
    }

    public string Name
    {
        get
        {
            var idx = _normalizedPath.LastIndexOf('/');
            return idx < 0 ? _normalizedPath : _normalizedPath[(idx + 1)..];
        }
    }
}
