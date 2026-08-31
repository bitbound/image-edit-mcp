namespace ImageEditMcp.Abstractions.FileSystem;

public interface IFileSystem
{
  void CopyFile(string sourceFile, string destinationFile, bool overwrite);
  void CreateDirectory(string directoryPath);
  Stream CreateFile(string filePath);
  void DeleteDirectory(string directoryPath, bool recursive);
  void DeleteFile(string filePath);
  bool DirectoryExists(string directoryPath);
  bool FileExists(string path);
  string[] GetDirectories(string path);
  IFileSystemDirectory GetDirectoryInfo(string directoryPath);
  IFileSystemFile GetFileInfo(string filePath);
  string[] GetFiles(string path);
  string[] GetFiles(string path, string searchPattern);
  string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
  string JoinPaths(char separator, params string[] paths);
  void MoveDirectory(string sourceDirectory, string destinationDirectory);
  void MoveFile(string sourceFile, string destinationFile, bool overwrite);
  Stream OpenFileStream(string path, FileMode mode, FileAccess access);
  Stream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare fileShare);
  Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
  Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
  Task WriteAllBytesAsync(string path, byte[] buffer, CancellationToken cancellationToken = default);
  Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
}
