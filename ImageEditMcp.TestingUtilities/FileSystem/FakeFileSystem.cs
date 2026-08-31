using System.Text;
using System.Text.RegularExpressions;

namespace ImageEditMcp.TestingUtilities.FileSystem;

public class FakeFileSystem : Abstractions.FileSystem.IFileSystem
{
    private readonly char _directorySeparator;
    private readonly Dictionary<string, FakeDirectoryEntry> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FakeFileEntry> _files = new(StringComparer.Ordinal);

    public FakeFileSystem(char directorySeparator = '/')
    {
        _directorySeparator = directorySeparator;
    }

    public void AddDirectory(string directoryPath)
    {
        var normalized = NormalizePath(directoryPath);
        EnsureDirectoryHierarchy(normalized);
    }

    public void AddFile(string filePath, string content = "")
        => AddFile(filePath, Encoding.UTF8.GetBytes(content));

    public void AddFile(string filePath, byte[] content)
    {
        var normalized = NormalizePath(filePath);
        var parent = GetParentPath(normalized);
        if (parent is not null)
        {
            EnsureDirectoryHierarchy(parent);
        }

        _files[normalized] = new FakeFileEntry(normalized, (byte[])content.Clone(), DateTime.UtcNow);
    }

    public IReadOnlyCollection<string> ListedDirectories => _directories.Keys.ToList();

    public IReadOnlyCollection<string> ListedFiles => _files.Keys.ToList();

    public void CopyFile(string sourceFile, string destinationFile, bool overwrite)
    {
        var source = NormalizePath(sourceFile);
        var destination = NormalizePath(destinationFile);

        if (!_files.TryGetValue(source, out var entry))
        {
            throw new FileNotFoundException($"Fake file not found: {sourceFile}");
        }

        if (_files.ContainsKey(destination) && !overwrite)
        {
            throw new IOException($"Fake file already exists: {destinationFile}");
        }

        _files[destination] = new FakeFileEntry(destination, (byte[])entry.Content.Clone(), DateTime.UtcNow);
    }

    public void CreateDirectory(string directoryPath)
        => EnsureDirectoryHierarchy(NormalizePath(directoryPath));

    public Stream CreateFile(string filePath)
    {
        var normalized = NormalizePath(filePath);
        var parent = GetParentPath(normalized);
        if (parent is not null)
        {
            EnsureDirectoryHierarchy(parent);
        }

        var entry = new FakeFileEntry(normalized, [], DateTime.UtcNow);
        _files[normalized] = entry;
        return new FakeFileSystemStream(entry, writable: true);
    }

    public void DeleteDirectory(string directoryPath, bool recursive)
    {
        var normalized = NormalizePath(directoryPath);
        if (!_directories.ContainsKey(normalized))
        {
            throw new DirectoryNotFoundException($"Fake directory not found: {directoryPath}");
        }

        var childDirs = _directories.Keys
            .Where(k => IsChildOf(k, normalized) && GetParentPath(k) == normalized)
            .ToList();

        var containedFiles = _files.Keys
            .Where(k => GetParentPath(k) == normalized)
            .ToList();

        if (!recursive && (childDirs.Count > 0 || containedFiles.Count > 0))
        {
            throw new IOException($"Fake directory is not empty: {directoryPath}");
        }

        foreach (var fileKey in containedFiles)
        {
            _files.Remove(fileKey);
        }

        _directories.Remove(normalized);
    }

    public void DeleteFile(string filePath)
    {
        var normalized = NormalizePath(filePath);
        if (!_files.Remove(normalized))
        {
            throw new FileNotFoundException($"Fake file not found: {filePath}");
        }
    }

    public bool DirectoryExists(string directoryPath)
        => _directories.ContainsKey(NormalizePath(directoryPath));

    public bool FileExists(string path)
        => _files.ContainsKey(NormalizePath(path));

    public string[] GetDirectories(string path)
    {
        var normalized = NormalizePath(path);
        if (!_directories.ContainsKey(normalized))
        {
            return [];
        }

        return _directories.Keys
            .Where(k => GetParentPath(k) == normalized)
            .ToArray();
    }

    public Abstractions.FileSystem.IFileSystemDirectory GetDirectoryInfo(string directoryPath)
        => new FakeFileSystemDirectoryInfo(NormalizePath(directoryPath), this);

    public Abstractions.FileSystem.IFileSystemFile GetFileInfo(string filePath)
        => new FakeFileSystemFileInfo(NormalizePath(filePath), this);

    public string[] GetFiles(string path)
    {
        var normalized = NormalizePath(path);
        return _files.Keys.Where(k => GetParentPath(k) == normalized).ToArray();
    }

    public string[] GetFiles(string path, string searchPattern)
        => GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        var normalized = NormalizePath(path);
        var regexPattern = SearchPatternToRegex(searchPattern);
        return _files.Keys
            .Where(k => IsUnderDirectory(k, normalized, searchOption))
            .Where(k => Regex.IsMatch(GetFileName(k), regexPattern, RegexOptions.IgnoreCase))
            .ToArray();
    }

    public string JoinPaths(char separator, params string[] paths)
        => string.Join(separator.ToString(), paths.Where(p => !string.IsNullOrEmpty(p)));

    public void MoveDirectory(string sourceDirectory, string destinationDirectory)
    {
        var source = NormalizePath(sourceDirectory);
        var destination = NormalizePath(destinationDirectory);

        if (!_directories.ContainsKey(source))
        {
            throw new DirectoryNotFoundException($"Fake directory not found: {sourceDirectory}");
        }

        if (_directories.ContainsKey(destination))
        {
            throw new IOException($"Fake directory already exists: {destinationDirectory}");
        }

        foreach (var key in _directories.Keys.Where(k => IsUnderDirectory(k, source, SearchOption.AllDirectories)).ToList())
        {
            var newKey = destination + key[source.Length..];
            _directories[newKey] = new FakeDirectoryEntry(newKey);
            _directories.Remove(key);
        }

        foreach (var key in _files.Keys.Where(k => IsUnderDirectory(k, source, SearchOption.AllDirectories)).ToList())
        {
            var newKey = destination + key[source.Length..];
            _files[newKey] = new FakeFileEntry(newKey, _files[key].Content, _files[key].LastWriteTime);
            _files.Remove(key);
        }

        _directories.Remove(source);
    }

    public void MoveFile(string sourceFile, string destinationFile, bool overwrite)
    {
        CopyFile(sourceFile, destinationFile, overwrite);
        DeleteFile(sourceFile);
    }

    public Stream OpenFileStream(string path, FileMode mode, FileAccess access)
        => OpenFileStream(path, mode, access, FileShare.None);

    public Stream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare fileShare)
    {
        var normalized = NormalizePath(path);
        var exists = _files.ContainsKey(normalized);

        if (!exists)
        {
            if (mode == FileMode.Open || mode == FileMode.Truncate)
            {
                throw new FileNotFoundException($"Fake file not found: {path}");
            }

            if (access == FileAccess.Read)
            {
                throw new FileNotFoundException($"Fake file not found: {path}");
            }

            var parent = GetParentPath(normalized);
            if (parent is not null && !_directories.ContainsKey(parent))
            {
                EnsureDirectoryHierarchy(parent);
            }

            _files[normalized] = new FakeFileEntry(normalized, [], DateTime.UtcNow);
        }
        else
        {
            if (mode == FileMode.CreateNew)
            {
                throw new IOException($"Fake file already exists: {path}");
            }

            if (mode is FileMode.Create or FileMode.Truncate)
            {
                _files[normalized].Content = [];
            }
        }

        return new FakeFileSystemStream(_files[normalized], writable: access != FileAccess.Read);
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizePath(path);
        if (!_files.TryGetValue(normalized, out var entry))
        {
            throw new FileNotFoundException($"Fake file not found: {path}");
        }

        return Task.FromResult((byte[])entry.Content.Clone());
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(Encoding.UTF8.GetString(ReadAllBytesAsync(path, cancellationToken).GetAwaiter().GetResult()));

    public Task WriteAllBytesAsync(string path, byte[] buffer, CancellationToken cancellationToken = default)
    {
        AddFile(path, buffer);
        return Task.CompletedTask;
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        AddFile(path, content);
        return Task.CompletedTask;
    }

    internal byte[]? GetFileContent(string normalizedPath)
        => _files.TryGetValue(normalizedPath, out var entry) ? entry.Content : null;

    internal DateTime GetLastWriteTime(string normalizedPath)
        => _files.TryGetValue(normalizedPath, out var entry) ? entry.LastWriteTime : default;

    internal DateTime GetDirectoryCreationTime(string normalizedPath)
        => _directories.TryGetValue(normalizedPath, out var entry) ? entry.CreationTime : default;

    internal bool DirectoryExistsInternal(string normalizedPath)
        => _directories.ContainsKey(normalizedPath);

    internal string? GetNormalizedParent(string normalizedPath)
        => GetParentPath(normalizedPath);

    private void EnsureDirectoryHierarchy(string normalizedPath)
    {
        if (normalizedPath.Length == 0 || _directories.ContainsKey(normalizedPath))
        {
            return;
        }

        var parent = GetParentPath(normalizedPath);
        if (parent is not null)
        {
            EnsureDirectoryHierarchy(parent);
        }

        _directories[normalizedPath] = new FakeDirectoryEntry(normalizedPath);
    }

    private string NormalizePath(string path)
    {
        var withSeparator = path.Replace('\\', _directorySeparator);
        if (withSeparator.Length > 1 && withSeparator.EndsWith(_directorySeparator))
        {
            withSeparator = withSeparator.TrimEnd(_directorySeparator);
        }

        return withSeparator;
    }

    private static string? GetParentPath(string normalizedPath)
    {
        var idx = normalizedPath.LastIndexOf('/');
        return idx <= 0 ? null : normalizedPath[..idx];
    }

    private static string GetFileName(string normalizedPath)
    {
        var idx = normalizedPath.LastIndexOf('/');
        return idx < 0 ? normalizedPath : normalizedPath[(idx + 1)..];
    }

    private static bool IsChildOf(string candidate, string parent)
        => candidate.StartsWith(parent + '/', StringComparison.Ordinal);

    private static bool IsUnderDirectory(string candidate, string parent, SearchOption option)
        => option == SearchOption.AllDirectories
            ? IsChildOf(candidate, parent)
            : GetParentPath(candidate) == parent;

    private static string SearchPatternToRegex(string searchPattern)
    {
        var escaped = Regex.Escape(searchPattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");
        return $"^{escaped}$";
    }
}

internal sealed class FakeFileEntry(string fullPath, byte[] content, DateTime lastWriteTime)
{
    public string FullPath { get; set; } = fullPath;
    public byte[] Content { get; set; } = content;
    public DateTime LastWriteTime { get; set; } = lastWriteTime;
}

internal sealed record FakeDirectoryEntry(string FullPath)
{
    public DateTime CreationTime { get; init; } = DateTime.UtcNow;
}
