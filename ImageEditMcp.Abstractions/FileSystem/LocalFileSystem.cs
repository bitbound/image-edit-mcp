namespace ImageEditMcp.Abstractions.FileSystem;

public class LocalFileSystem : IFileSystem
{
  public void CopyFile(string sourceFile, string destinationFile, bool overwrite)
    => File.Copy(sourceFile, destinationFile, overwrite);

  public void CreateDirectory(string directoryPath)
    => Directory.CreateDirectory(directoryPath);

  public Stream CreateFile(string filePath)
    => File.Create(filePath);

  public void DeleteDirectory(string directoryPath, bool recursive)
    => Directory.Delete(directoryPath, recursive);

  public void DeleteFile(string filePath)
    => File.Delete(filePath);

  public bool DirectoryExists(string directoryPath)
    => Directory.Exists(directoryPath);

  public bool FileExists(string path)
    => File.Exists(path);

  public string[] GetDirectories(string path)
    => Directory.GetDirectories(path);

  public IFileSystemDirectory GetDirectoryInfo(string directoryPath)
    => new FileSystemDirectoryInfo(new DirectoryInfo(directoryPath));

  public IFileSystemFile GetFileInfo(string filePath)
    => new FileSystemFileInfo(new FileInfo(filePath));

  public string[] GetFiles(string path)
    => Directory.GetFiles(path);

  public string[] GetFiles(string path, string searchPattern)
    => Directory.GetFiles(path, searchPattern);

  public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    => Directory.GetFiles(path, searchPattern, searchOption);

  public string JoinPaths(char separator, params string[] paths)
    => string.Join(separator.ToString(), paths.Where(p => !string.IsNullOrEmpty(p)));

  public void MoveDirectory(string sourceDirectory, string destinationDirectory)
    => Directory.Move(sourceDirectory, destinationDirectory);

  public void MoveFile(string sourceFile, string destinationFile, bool overwrite)
  {
    if (overwrite && File.Exists(destinationFile))
    {
      File.Delete(destinationFile);
    }

    File.Move(sourceFile, destinationFile, overwrite);
  }

  public Stream OpenFileStream(string path, FileMode mode, FileAccess access)
    => new FileStream(path, mode, access);

  public Stream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare fileShare)
    => new FileStream(path, mode, access, fileShare);

  public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    => File.ReadAllBytesAsync(path, cancellationToken);

  public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    => File.ReadAllTextAsync(path, cancellationToken);

  public Task WriteAllBytesAsync(string path, byte[] buffer, CancellationToken cancellationToken = default)
    => File.WriteAllBytesAsync(path, buffer, cancellationToken);

  public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    => File.WriteAllTextAsync(path, content, cancellationToken);
}
