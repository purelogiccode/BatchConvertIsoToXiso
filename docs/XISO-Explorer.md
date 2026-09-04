# XISO Explorer

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [**XISO Explorer**](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | |

---

The **XISO Explorer** tab provides a native file browser for Xbox and Xbox 360 ISO images. It parses the XDVDFS filesystem directly from the image, so you can inspect archives without extracting anything.

## Opening an Image

1. Switch to the **Explorer** tab.
2. Click **Open ISO** (or type/paste a path into the file box) and select an Xbox ISO file.
3. The root of the image appears in the file list.

Both standard XISO files and Redump ISOs are supported — the explorer automatically locates the game partition.

## Browsing

| Action | Result |
|:---|:---|
| Double-click a folder | Enter the folder and list its contents |
| **Up** button | Move to the parent directory |
| Path label | Shows the current path inside the image |

The list displays three columns:

| Column | Contents |
|:---|:---|
| **Name** | File or folder name |
| **Size** | Size in bytes (human-readable) |
| **Type** | File or directory |

## Opening Files

**Double-click any file** to extract it to a temporary location and open it with its default associated application (for example, a `.xbe` viewer or a text editor). The temporary copy is cleaned up by the application's normal temp-folder housekeeping.

## Drag-and-Drop Extraction

Drag one or more files (or whole folders) from the explorer list onto:

- A Windows Explorer window,
- The Desktop, or
- Any folder.

The selected items are extracted from the ISO to the drop target. This is the quickest way to pull individual files or directories out of an image without extracting the entire disc.

## Notes and Limitations

- The explorer operates read-only; it never modifies the source ISO.
- Very deep directory trees are handled with an iterative traversal (no recursion limits).
- Images that fail XDVDFS validation are rejected with a clear error rather than showing unreliable content.
- The explorer shares the same parsing code as the conversion and testing engines (see [Architecture](Architecture.md)), so what you see here reflects exactly what the other views process.
