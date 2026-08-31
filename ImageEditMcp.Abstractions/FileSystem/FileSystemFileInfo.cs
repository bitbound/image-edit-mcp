namespace ImageEditMcp.Abstractions.FileSystem;

internal sealed class FileSystemFileInfo(FileInfo _fileInfo) : IFileSystemFile
{
  public FileAttributes Attributes => _fileInfo.Exists ? _fileInfo.Attributes : FileAttributes.Normal;

  public bool Exists => _fileInfo.Exists;

  public string FullName => _fileInfo.FullName;

  public DateTime LastWriteTime => _fileInfo.LastWriteTime;

  public long Length => _fileInfo.Exists ? _fileInfo.Length : 0;

  public string Name => _fileInfo.Name;
}
