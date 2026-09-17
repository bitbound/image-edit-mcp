# Image Edit MCP

A .NET Model Context Protocol (MCP) server that exposes SkiaSharp image editing tools over stdio.

## Overview

This MCP server allows an agent (e.g. GitHub Copilot, OpenCode, etc.) to load an image from a file path, apply a wide range of SkiaSharp-powered edits on a working copy stored in a local data folder, read back the current state at any time, and save the result to a new file.

## Requirements

- .NET 10 SDK
- Linux/macOS/Windows (SkiaSharp native assets are included per-platform)

## Usage with GitHub Copilot

Add the following to your GitHub Copilot config (`mcp.json`):

```json
{
  "mcpServers": {
		"image-edit-mcp": {
			"type": "stdio",
			"command": "dnx",
			"args": [
				"-y",
				"Bitbound.ImageEditMcp"
			]
		}
  }
}
```

## Building

```bash
dotnet build -c Release
```

The output binary is at `bin/Release/net10.0/ImageEditMcp.dll`.


## How It Works

1. **`load_image`** loads an image from a file path and creates a working copy in the data folder (`<config-dir>/image-edit-mcp/`).
2. All subsequent edit tools operate on this working copy in memory.
3. **`read_image`** returns the current state as a base64-encoded PNG data URI.
4. **`save_image`** saves the working copy to a new file path (the original loaded file is never modified).
5. **`save_snapshot`** / **`load_snapshot`** allow you to checkpoint and restore intermediate states.

## Available Tools

### Core Operations

| Tool | Description |
|------|-------------|
| `load_image` | Load an image from a file path into a working copy |
| `read_image` | Read the current working copy as a base64 PNG data URI |
| `save_image` | Save the working copy to a new file path |
| `get_image_info` | Get metadata (dimensions, format, dirty state) |
| `save_snapshot` | Save a named snapshot of the current state |
| `load_snapshot` | Restore from a previously saved snapshot |
| `reload_original` | Discard all edits and reload the original image |

### Transform Operations

| Tool | Description |
|------|-------------|
| `resize_image` | Resize to specific dimensions (preserves aspect ratio if one dim is 0) |
| `scale_image` | Scale by a uniform factor |
| `crop_image` | Crop to a rectangular region |
| `rotate_image` | Rotate by an angle (with configurable fill color) |
| `flip_image` | Flip horizontally or vertically |
| `translate_image` | Shift the image by an offset |
| `apply_matrix_transform` | Apply a custom 3x3 matrix transform |
| `apply_shear` | Apply a shearing/skewing transformation |

### Color Adjustments

| Tool | Description |
|------|-------------|
| `adjust_brightness` | Adjust brightness (-1.0 to 1.0) |
| `adjust_contrast` | Adjust contrast (-1.0 to 1.0) |
| `adjust_brightness_contrast` | Adjust both simultaneously |
| `adjust_saturation` | Adjust saturation (-1.0 to 1.0) |
| `adjust_hue` | Rotate hue by degrees |
| `adjust_gamma` | Apply gamma correction |
| `convert_to_grayscale` | Convert to greyscale |
| `invert_colors` | Invert all colors (photographic negative) |
| `apply_sepia` | Apply sepia tone |
| `apply_vintage` | Apply vintage/warm tone filter |
| `apply_color_matrix` | Apply a custom 4x5 color matrix |
| `apply_threshold` | Apply binary threshold |
| `replace_color` | Replace colors within tolerance |

### Blur & Filter Operations

| Tool | Description |
|------|-------------|
| `apply_blur_gaussian` | Apply Gaussian blur |
| `apply_blur_box` | Apply box blur |
| `apply_sharpen` | Apply sharpening via convolution |
| `apply_emboss` | Apply emboss effect via convolution |
| `apply_edge_detection` | Apply Sobel edge detection |
| `apply_pixelate` | Pixelate regions |
| `apply_noise` | Add random noise |

### Drawing Tools

| Tool | Description |
|------|-------------|
| `draw_rectangle` | Draw a rectangle (fill/stroke) |
| `draw_circle` | Draw a circle (fill/stroke) |
| `draw_line` | Draw a line |
| `draw_text` | Draw text with font customization |
| `set_pixel` | Set a single pixel color |
| `fill_region` | Fill a rectangular region with blend mode |
| `clear_region` | Clear a region to a color |
| `overlay_image` | Overlay another image with blend mode and opacity |

### Utility

| Tool | Description |
|------|-------------|
| `trim_image` | Trim border pixels matching corner colors |
| `create_thumbnail` | Create a thumbnail (resize to fit within dimensions) |

## Architecture

```
ImageEditMcp/
├── Program.cs                          # MCP server entry point (stdio transport)
├── ImageEditor/
│   ├── ImageDataManager.cs             # Working copy lifecycle (load/save/snapshot)
│   └── ImageEditTools.cs               # All MCP tools (43 tools)
├── ImageEditMcp.csproj
```

The `ImageDataManager` is a singleton that manages:
- The in-memory working `SKBitmap`
- The source file path
- A working copy path in the data folder
- A dirty flag tracking unsaved changes

The `ImageEditTools` class contains all 43 MCP tools, each annotated with `[McpServerTool]` and parameter descriptions via `[Description]`.

### Data Directory

Working copies and snapshots are stored in:
- **Linux**: `$HOME/.config/image-edit-mcp/`
- **macOS**: `~/Library/Application Support/image-edit-mcp/`
- **Windows**: `%APPDATA%\image-edit-mcp\`

## License

[MIT](LICENSE) © Bitbound.
