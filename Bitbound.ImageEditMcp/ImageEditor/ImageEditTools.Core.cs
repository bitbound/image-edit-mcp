namespace Bitbound.ImageEditMcp.ImageEditor;

public sealed partial class ImageEditTools
{
  [McpServerTool(Name = "create_image")]
  [Description("Creates a blank working copy of the given size, filled with a color (transparent by default). No source file is required, so drawing can start from an empty canvas.")]
  public string CreateImage(
      [Description("Width of the new image in pixels.")]
        int width,
      [Description("Height of the new image in pixels.")]
        int height,
      [Description("Fill color as hex (e.g. '#FF0000' for red, '#00000000' for transparent). Default: transparent.")]
        string? color = "00000000")
  {
    var fillColor = ParseColor(color, SKColors.Transparent);
    var (w, h) = _dataManager.CreateImage(width, height, fillColor);
    return $"Created blank working copy.\n" +
           $"Dimensions: {w}x{h}\n" +
           $"Format: png\n" +
           $"Data directory: {_dataManager.DataDirectory}";
  }

  [McpServerTool(Name = "get_image_info")]
  [Description("Returns metadata about the currently loaded image including dimensions, format, source path, working copy path, and dirty state.")]
  public string GetImageInfo()
  {
    var (width, height, format, sourcePath, workingCopyPath, dirty, workingCopySize) = _dataManager.GetImageInfo();
    return $"Image Info:\n" +
           $"  Dimensions: {width}x{height}\n" +
           $"  Format: {format}\n" +
           $"  Source: {sourcePath ?? "none"}\n" +
           $"  Working Copy: {workingCopyPath ?? "none"} ({workingCopySize} bytes)\n" +
           $"  Has Unsaved Changes: {dirty}";
  }

  [McpServerTool(Name = "load_image")]
  [Description("Loads an image from the given file path and creates a working copy in the local data folder. All subsequent edits operate on this working copy until a new image is loaded.")]
  public string LoadImage(
      [Description("Absolute or relative path to the image file to load (PNG, JPEG, WEBP, BMP, GIF, TGA).")]
        string filePath)
  {
    var (width, height, format, sizeBytes) = _dataManager.LoadImage(filePath);
    return $"Loaded image '{filePath}' into working copy.\n" +
           $"Dimensions: {width}x{height}\n" +
           $"Format: {format}\n" +
           $"Size: {sizeBytes} bytes\n" +
           $"Data directory: {_dataManager.DataDirectory}";
  }

  [McpServerTool(Name = "load_snapshot")]
  [Description("Loads a previously saved snapshot, restoring the working copy to that state.")]
  public string LoadSnapshot(
      [Description("Name of the snapshot file to load (without path), or full path.")]
        string snapshotName)
  {
    var result = _dataManager.LoadSnapshot(snapshotName);
    return result;
  }

  [McpServerTool(Name = "read_image")]
  [Description("Reads the current working copy image and returns it as a base64-encoded PNG data URI. After any edit tool is called, this returns the most recent state of the image.")]
  public string ReadImage()
  {
    var base64 = _dataManager.GetImageDataBase64();
    return $"data:image/png;base64,{base64}";
  }

  [McpServerTool(Name = "reload_original")]
  [Description("Reloads the original source image, discarding all unsaved edits to the working copy.")]
  public string ReloadOriginal()
  {
    _dataManager.ReloadOriginal();
    var (w, h, _, _, _, _, _) = _dataManager.GetImageInfo();
    return $"Reloaded original image. Dimensions: {w}x{h}";
  }

  [McpServerTool(Name = "save_image")]
  [Description("Saves the current working copy to a new file path. The original loaded file is not modified.")]
  public string SaveImage(
      [Description("Output file path to save the image to (format determined by extension).")]
        string outputPath,
      [Description("Encoding quality 0-100 (mainly affects JPEG/WEBP). Default 90.")]
        int quality = 90)
  {
    var savedPath = _dataManager.SaveImage(outputPath, quality);
    return $"Image saved to: {savedPath}";
  }

  [McpServerTool(Name = "save_snapshot")]
  [Description("Saves a named snapshot of the current working copy state, allowing you to revert to it later with load_snapshot.")]
  public string SaveSnapshot(
      [Description("A name for the snapshot. If empty, uses a timestamp.")]
        string snapshotName = "")
  {
    var path = _dataManager.SaveSnapshot(snapshotName);
    return $"Snapshot saved to: {path}";
  }
}
