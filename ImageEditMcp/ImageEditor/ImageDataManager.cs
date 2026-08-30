using SkiaSharp;

namespace ImageEditMcp.ImageEditor;

/// <summary>
/// Manages the lifecycle of a working copy image in a local data folder.
/// </summary>
public sealed class ImageDataManager : IDisposable
{
    private readonly string _dataDirectory;

    private SKBitmap? _bitmap;
    private string? _sourcePath;
    private string? _workingCopyPath;
    private bool _dirty;

    public ImageDataManager()
    {
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "image-edit-mcp");
        Directory.CreateDirectory(_dataDirectory);
    }

    /// <summary>
    /// Loads an image from the given file path and creates a working copy in the data folder.
    /// </summary>
    public (int Width, int Height, string Format, long SizeBytes) LoadImage(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Image file not found: {filePath}");
        }

        var normalizedPath = Path.GetFullPath(filePath);
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";
        var format = NormalizeFormat(ext);

        using var stream = File.OpenRead(normalizedPath);
        _bitmap = SKBitmap.Decode(stream);
        _sourcePath = normalizedPath;

        var fileName = Path.GetFileName(normalizedPath);
        _workingCopyPath = Path.Combine(_dataDirectory, fileName);

        SaveBitmapFile(_bitmap, _workingCopyPath, format, 100);
        _dirty = false;

        return (_bitmap.Width, _bitmap.Height, format, new FileInfo(normalizedPath).Length);
    }

    /// <summary>
    /// Gets information about the current working image.
    /// </summary>
    public (int Width, int Height, string Format, string? SourcePath, string? WorkingCopyPath, bool Dirty, long WorkingCopySizeBytes) GetImageInfo()
    {
        if (_bitmap is null)
        {
            throw new InvalidOperationException("No image is currently loaded. Call load_image first.");
        }

        var format = "png";
        if (_sourcePath is not null)
        {
            format = NormalizeFormat(Path.GetExtension(_sourcePath) ?? ".png");
        }

        var workingCopySize = _workingCopyPath is not null && File.Exists(_workingCopyPath)
            ? new FileInfo(_workingCopyPath).Length
            : 0L;

        return (_bitmap.Width, _bitmap.Height, format, _sourcePath, _workingCopyPath, _dirty, workingCopySize);
    }

    /// <summary>
    /// Returns the current working copy as a base64-encoded PNG string.
    /// </summary>
    public string GetImageDataBase64()
    {
        if (_bitmap is null)
        {
            throw new InvalidOperationException("No image is currently loaded. Call load_image first.");
        }

        using var data = _bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }

    /// <summary>
    /// Saves the current working copy to the specified output path.
    /// </summary>
    public string SaveImage(string outputPath, int quality = 100)
    {
        if (_bitmap is null)
        {
            throw new InvalidOperationException("No image is currently loaded. Call load_image first.");
        }

        var fullPath = Path.GetFullPath(outputPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var ext = Path.GetExtension(fullPath)?.ToLowerInvariant() ?? ".png";
        var format = NormalizeFormat(ext);
        SaveBitmapFile(_bitmap, fullPath, format, quality);
        _dirty = false;

        return fullPath;
    }

    /// <summary>
    /// Saves a snapshot of the current working copy state.
    /// </summary>
    public string SaveSnapshot(string snapshotName = "")
    {
        if (_bitmap is null)
        {
            throw new InvalidOperationException("No image is currently loaded. Call load_image first.");
        }

        if (string.IsNullOrWhiteSpace(snapshotName))
        {
            snapshotName = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        }

        var snapshotDir = Path.Combine(_dataDirectory, "snapshots");
        Directory.CreateDirectory(snapshotDir);

        var ext = Path.GetExtension(_workingCopyPath ?? "image.png") ?? ".png";
        var snapshotPath = Path.Combine(snapshotDir, $"{snapshotName}{ext}");

        var format = NormalizeFormat(ext);
        SaveBitmapFile(_bitmap, snapshotPath, format, 100);

        return snapshotPath;
    }

    /// <summary>
    /// Loads a snapshot file into the working copy, replacing the current image.
    /// </summary>
    public string LoadSnapshot(string snapshotName)
    {
        var path = snapshotName;
        if (!File.Exists(path))
        {
            var snapshotsDir = Path.Combine(_dataDirectory, "snapshots");
            var altPath = Path.Combine(snapshotsDir, snapshotName);
            if (File.Exists(altPath)) { path = altPath; }
            else { throw new FileNotFoundException($"Snapshot not found: {snapshotName}"); }
        }

        var normalizedPath = Path.GetFullPath(path);
        var ext = Path.GetExtension(normalizedPath)?.ToLowerInvariant() ?? ".png";
        var format = NormalizeFormat(ext);

        using var stream = File.OpenRead(normalizedPath);
        var newBitmap = SKBitmap.Decode(stream);

        _bitmap?.Dispose();
        _bitmap = newBitmap;

        var fileName = Path.GetFileName(_sourcePath ?? "image.png");
        _workingCopyPath = Path.Combine(_dataDirectory, fileName);
        SaveBitmapFile(_bitmap, _workingCopyPath, format, 100);
        _dirty = false;

        return $"Snapshot '{normalizedPath}' loaded into working copy.\n" +
               $"Dimensions: {_bitmap.Width}x{_bitmap.Height}";
    }

    /// <summary>
    /// Reloads the original source image, discarding any unsaved edits.
    /// </summary>
    public void ReloadOriginal()
    {
        if (_sourcePath is null)
        {
            throw new InvalidOperationException("No source image to reload.");
        }

        var format = NormalizeFormat(Path.GetExtension(_sourcePath) ?? ".png");
        using var stream = File.OpenRead(_sourcePath);
        var newBitmap = SKBitmap.Decode(stream);
        _bitmap?.Dispose();
        _bitmap = newBitmap;
        SaveBitmapFile(_bitmap, _workingCopyPath!, format, 100);
        _dirty = false;
    }

    /// <summary>
    /// Applies an in-place edit using a canvas drawn on the current bitmap.
    /// </summary>
    public void ApplyEdit(Action<SKCanvas> drawAction)
    {
        if (_bitmap is null)
        {
            throw new InvalidOperationException("No image is currently loaded. Call load_image first.");
        }

        using var canvas = new SKCanvas(_bitmap);
        drawAction(canvas);
        _dirty = true;
    }

    /// <summary>
    /// Replaces the current bitmap with a new one produced by transform.
    /// The old bitmap is disposed.
    /// </summary>
    public void ReplaceBitmap(Func<SKBitmap, SKBitmap> transform)
    {
        if (_bitmap is null)
        {
            throw new InvalidOperationException("No image is currently loaded. Call load_image first.");
        }

        var newBitmap = transform(_bitmap);
        _bitmap.Dispose();
        _bitmap = newBitmap;
        _dirty = true;
    }

    /// <summary>
    /// Creates a clone of the current working bitmap.
    /// </summary>
    public SKBitmap CloneCurrentBitmap()
    {
        if (_bitmap is null)
        {
            throw new InvalidOperationException("No image is currently loaded. Call load_image first.");
        }

        return _bitmap.Copy();
    }

    /// <summary>
    /// Checks if a bitmap is currently loaded.
    /// </summary>
    public bool HasImage => _bitmap is not null;

    public string DataDirectory => _dataDirectory;

    private static void SaveBitmapFile(SKBitmap bitmap, string filePath, string format, int quality)
    {
        var encodedFormat = GetEncodedFormat(format);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(encodedFormat, quality);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    private static string NormalizeFormat(string ext) =>
        ext.ToLowerInvariant() switch
        {
            ".png" => "png",
            ".jpg" or ".jpeg" => "jpeg",
            ".webp" => "webp",
            ".bmp" => "bmp",
            ".gif" => "gif",
            ".avif" => "avif",
            _ => "png"
        };

    private static SKEncodedImageFormat GetEncodedFormat(string format) =>
        format.ToLowerInvariant() switch
        {
            "png" => SKEncodedImageFormat.Png,
            "jpeg" => SKEncodedImageFormat.Jpeg,
            "webp" => SKEncodedImageFormat.Webp,
            "bmp" => SKEncodedImageFormat.Bmp,
            "gif" => SKEncodedImageFormat.Gif,
            "avif" => SKEncodedImageFormat.Avif,
            _ => SKEncodedImageFormat.Png
        };

    public void Dispose()
    {
        _bitmap?.Dispose();
    }
}
