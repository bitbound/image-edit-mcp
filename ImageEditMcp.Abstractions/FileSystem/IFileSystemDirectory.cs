namespace ImageEditMcp.Abstractions.FileSystem;

public interface IFileSystemDirectory
{
  FileAttributes Attributes { get; }
  DateTime CreationTime { get; }
  bool Exists { get; }
  string FullName { get; }
  DateTime LastWriteTime { get; }
  string Name { get; }
  IFileSystemDirectory? Parent { get; }
}
