namespace ImageEditMcp.Abstractions.FileSystem;

internal sealed class FileSystemDirectoryInfo(DirectoryInfo _directoryInfo) : IFileSystemDirectory
{
  public FileAttributes Attributes => _directoryInfo.Exists ? _directoryInfo.Attributes : FileAttributes.Directory;

  public DateTime CreationTime => _directoryInfo.CreationTime;

  public bool Exists => _directoryInfo.Exists;

  public string FullName => _directoryInfo.FullName;

  public DateTime LastWriteTime => _directoryInfo.LastWriteTime;

  public string Name => _directoryInfo.Name;

  public IFileSystemDirectory? Parent => _directoryInfo.Parent is null
    ? null
    : new FileSystemDirectoryInfo(_directoryInfo.Parent);
}
