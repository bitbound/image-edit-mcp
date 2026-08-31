namespace ImageEditMcp.TestingUtilities.FileSystem;

internal sealed class FakeFileSystemDirectoryInfo(string _normalizedPath, FakeFileSystem _fileSystem) : Abstractions.FileSystem.IFileSystemDirectory
{
    public FileAttributes Attributes => FileAttributes.Directory;

    public DateTime CreationTime => _fileSystem.GetDirectoryCreationTime(_normalizedPath);

    public bool Exists => _fileSystem.DirectoryExistsInternal(_normalizedPath);

    public string FullName => _normalizedPath;

    public DateTime LastWriteTime => CreationTime;

    public string Name
    {
        get
        {
            var idx = _normalizedPath.LastIndexOf('/');
            return idx < 0 ? _normalizedPath : _normalizedPath[(idx + 1)..];
        }
    }

    public Abstractions.FileSystem.IFileSystemDirectory? Parent
    {
        get
        {
            var parent = _fileSystem.GetNormalizedParent(_normalizedPath);
            return parent is null ? null : new FakeFileSystemDirectoryInfo(parent, _fileSystem);
        }
    }
}
