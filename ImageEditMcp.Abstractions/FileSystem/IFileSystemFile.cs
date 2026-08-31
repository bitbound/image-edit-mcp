namespace ImageEditMcp.Abstractions.FileSystem;

public interface IFileSystemFile
{
  FileAttributes Attributes { get; }
  bool Exists { get; }
  string FullName { get; }
  DateTime LastWriteTime { get; }
  long Length { get; }
  string Name { get; }
}
